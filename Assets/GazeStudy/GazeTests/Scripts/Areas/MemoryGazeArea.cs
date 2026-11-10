using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// Playground 10, in two halves.
/// Sequence: objects flash in an order, then the player looks at them in the same order (dwell or grab).
///   A right answer makes the next sequence one longer and adds purple flashes to ignore.
/// Guard: a guard patrols and the player must keep watching it. It turns yellow as a warning and then
///   red while it looks back. While it is red, the gaze must be on the safe zone or the player is seen.
public class MemoryGazeArea : GazeTestArea
{
    enum GuardState { Patrol, Warning, Looking }

    static readonly Color IdleColor = new Color(0.4f, 0.42f, 0.48f);
    static readonly Color FlashColor = new Color(1f, 0.85f, 0.1f);
    static readonly Color DistractColor = new Color(0.65f, 0.25f, 0.85f);
    static readonly Color SafeColor = new Color(0.2f, 0.7f, 0.35f);
    static readonly Color WarningColor = new Color(1f, 0.85f, 0.1f);
    static readonly Color LookingColor = new Color(0.9f, 0.15f, 0.15f);

    [Header("Sequence")]
    [SerializeField] int objects = 6;
    [SerializeField] float radius = 5f;
    [SerializeField] float objectSize = 0.6f;
    [SerializeField] float spacingDeg = 18f;
    [SerializeField] int startLength = 3;
    [SerializeField] float flashSeconds = 0.6f;
    [SerializeField] float gapSeconds = 0.25f;
    [SerializeField] float dwellSeconds = 0.5f;

    [Header("Guard")]
    [SerializeField] float guardDistance = 7f;
    [SerializeField] float guardRange = 3f;
    [SerializeField] float guardStartSpeed = 1.5f;
    [SerializeField] float guardSpeedStep = 0.2f;
    [SerializeField] Vector2 patrolSeconds = new Vector2(3f, 6f);
    [SerializeField] float warningSeconds = 0.7f;
    [SerializeField] Vector2 lookSeconds = new Vector2(1.5f, 2.5f);

    readonly List<GazeTarget> items = new List<GazeTarget>();
    readonly List<int> sequence = new List<int>();
    TextMeshPro prompt;
    bool guardMode, showing;
    int length, step, distractions, sequencesRight, sequencesWrong, longest;
    float lastPick;

    GazeTarget guard, safe;
    GuardState state;
    float guardX, guardDirection, guardSpeed, stateStart, stateEnd, lookDuration, reachedSafe, patrolTime, watchedTime;
    bool seen;
    int looks, timesSeen;

    protected override GazeTarget Focus => !guardMode ? null : state == GuardState.Patrol ? guard : safe;

    protected override void Begin()
    {
        guardMode = false;
        length = startLength;
        sequencesRight = sequencesWrong = longest = looks = timesSeen = 0;
        patrolTime = watchedTime = 0f;

        items.Clear();
        prompt = Label("", Direction(0f, 22f, radius), 2.5f);
        for (int i = 0; i < objects; i++)
        {
            GazeTarget item = Spawn("Object" + i, PrimitiveType.Sphere, Direction((i - (objects - 1) * 0.5f) * spacingDeg, 5f, radius), Vector3.one * objectSize, IdleColor);
            item.Index = i;
            items.Add(item);
        }
        NewSequence();
    }

    protected override void Tick()
    {
        if (!guardMode && Elapsed >= Duration * 0.5f) StartGuard();

        if (guardMode) TickGuard();
        else if (!showing) TickRecall();
    }

    // ---------------------------------------------------------------- sequence

    void NewSequence()
    {
        sequence.Clear();
        while (sequence.Count < length)
        {
            int next = Random.Range(0, objects);
            if (sequence.Count == 0 || sequence[sequence.Count - 1] != next) sequence.Add(next);
        }
        distractions = Mathf.Max(0, length - startLength);
        StartCoroutine(Show());
    }

    IEnumerator Show()
    {
        showing = true;
        yield return new WaitForSeconds(0.8f);
        prompt.text = distractions > 0 ? $"Watch the yellow sequence ({length}). Ignore purple." : $"Watch the yellow sequence ({length})";
        yield return new WaitForSeconds(0.6f);

        int left = distractions;
        for (int i = 0; i < sequence.Count; i++)
        {
            GazeTarget item = items[sequence[i]];
            GazeTarget distractor = null;
            if (left > 0 && Random.value < distractions / (float)sequence.Count * 1.5f)
            {
                int other;
                do other = Random.Range(0, objects);
                while (other == sequence[i]);
                distractor = items[other];
                distractor.SetColor(DistractColor);
                left--;
            }

            item.SetColor(FlashColor);
            yield return new WaitForSeconds(flashSeconds);
            item.SetColor(IdleColor);
            if (distractor != null) distractor.SetColor(IdleColor);
            yield return new WaitForSeconds(gapSeconds);
        }

        step = 0;
        lastPick = Time.time;
        ResetDwell();
        prompt.text = "Your turn: look at them in the same order";
        showing = false;
    }

    void TickRecall()
    {
        GazeTarget picked = TrackDwell(dwellSeconds);
        if (picked == null && Confirm && Looked != null)
        {
            picked = Looked;
            SpendDwell();
        }
        if (picked == null) return;

        bool correct = picked.Index == sequence[step];
        Log("Recall", picked.Id, picked.transform.position, correct, Time.time - lastPick,
            $"length={length};distractions={distractions};dwell={F(dwellSeconds)}", $"step={step};expected=Object{sequence[step]}");
        lastPick = Time.time;

        if (correct && ++step < sequence.Count) return;

        Trial(correct);
        if (correct)
        {
            sequencesRight++;
            longest = Mathf.Max(longest, length);
            length++;
            prompt.text = "Correct";
        }
        else
        {
            sequencesWrong++;
            prompt.text = "Wrong order";
        }
        NewSequence();
    }

    // ---------------------------------------------------------------- guard

    void StartGuard()
    {
        StopAllCoroutines();
        ClearStimuli();
        items.Clear();
        guardMode = true;
        showing = false;

        Label("Watch the guard. When it turns RED, look at the SAFE ZONE.", Point(0f, 2.2f, guardDistance), 2.5f);
        safe = Spawn("SafeZone", PrimitiveType.Cube, Point(0f, 1.2f, guardDistance - 1f), new Vector3(1.2f, 0.6f, 0.05f), SafeColor);
        Label("SAFE ZONE", Point(0f, 1.2f, guardDistance - 1.05f), 2f);
        guardX = 0f;
        guard = Spawn("Guard", PrimitiveType.Capsule, GuardPosition(), new Vector3(0.6f, 0.9f, 0.6f), IdleColor);
        guardDirection = 1f;
        guardSpeed = guardStartSpeed;
        Log("GuardModeStart", guard.Id, guard.transform.position, null);
        StartPatrol();
    }

    void StartPatrol()
    {
        state = GuardState.Patrol;
        guard.SetColor(IdleColor);
        stateEnd = Time.time + Random.Range(patrolSeconds.x, patrolSeconds.y);
    }

    Vector3 GuardPosition()
    {
        Vector3 position = Point(guardX, 0f, guardDistance);
        position.y = 0.9f; // standing on the floor
        return position;
    }

    void TickGuard()
    {
        float dt = Time.deltaTime;
        guardX += guardDirection * guardSpeed * dt;
        if (Mathf.Abs(guardX) > guardRange)
        {
            guardX = Mathf.Sign(guardX) * guardRange;
            guardDirection = -guardDirection;
        }
        guard.transform.position = GuardPosition();

        bool onSafe = Looked == safe;
        switch (state)
        {
            case GuardState.Patrol:
                patrolTime += dt;
                if (Looked == guard) watchedTime += dt;
                if (Time.time < stateEnd) break;

                state = GuardState.Warning;
                guard.SetColor(WarningColor);
                stateStart = Time.time;
                stateEnd = Time.time + warningSeconds;
                reachedSafe = -1f;
                break;

            case GuardState.Warning:
                if (onSafe && reachedSafe < 0f) reachedSafe = Time.time - stateStart;
                if (Time.time < stateEnd) break;

                state = GuardState.Looking;
                guard.SetColor(LookingColor);
                lookDuration = Random.Range(lookSeconds.x, lookSeconds.y);
                stateEnd = Time.time + lookDuration;
                seen = false;
                break;

            case GuardState.Looking:
                if (onSafe && reachedSafe < 0f) reachedSafe = Time.time - stateStart;
                if (!onSafe) seen = true;
                if (Time.time < stateEnd) break;

                looks++;
                if (seen) timesSeen++;
                Trial(!seen);
                // The reaction time runs from the warning to the gaze first reaching the safe zone.
                Log("GuardLook", guard.Id, guard.transform.position, !seen, reachedSafe,
                    $"warningSeconds={F(warningSeconds)};lookSeconds={F(lookDuration)};guardSpeed={F(guardSpeed)}",
                    $"watchPercent={F(WatchPercent())}");
                if (!seen) guardSpeed += guardSpeedStep;
                StartPatrol();
                break;
        }
    }

    float WatchPercent() => patrolTime > 0f ? 100f * watchedTime / patrolTime : 0f;

    protected override string Summary() =>
        $"sequencesRight={sequencesRight};sequencesWrong={sequencesWrong};longestSequence={longest};" +
        $"guardLooks={looks};timesSeen={timesSeen};guardWatchPercent={F(WatchPercent())}";
}
