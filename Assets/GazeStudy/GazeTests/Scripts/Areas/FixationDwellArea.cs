using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// Playground 6. Targets sit around the player and one is the goal. Holding the gaze on any target for
/// the dwell time activates it, and it fills while gazed at and resets if the gaze leaves. Activating a
/// target that isn't the goal is a false trigger (the Midas touch problem). Dwell time, target size and
/// spacing all vary.
public class FixationDwellArea : GazeTestArea
{
    const int Columns = 5, Rows = 2;
    static readonly Color IdleColor = new Color(0.4f, 0.42f, 0.48f);
    static readonly Color GoalColor = new Color(0.1f, 0.75f, 0.95f);

    [SerializeField] float radius = 5f;
    [SerializeField] float[] dwellTimes = { 0.5f, 1f, 2f };
    [SerializeField] float[] sizes = { 0.3f, 0.5f, 0.8f };
    [SerializeField] float[] spacingsDeg = { 24f, 12f };
    [SerializeField] int activationsPerLayout = 4;
    [SerializeField] float abortAfter = 0.15f; // shorter looks at the goal are just the gaze passing over

    readonly List<GazeTarget> targets = new List<GazeTarget>();
    TextMeshPro label;
    GazeTarget goal;
    float dwellTime, goalSince, spacing;
    int layout, activations, falseTriggers, aborts, goalAborts;

    protected override GazeTarget Focus => goal;

    protected override void Begin()
    {
        layout = activations = falseTriggers = aborts = 0;
        targets.Clear();
        BuildLayout();
    }

    void BuildLayout()
    {
        ClearStimuli();
        targets.Clear();
        goal = null;
        label = Label("", Origin, 2f);
        spacing = spacingsDeg[layout % spacingsDeg.Length];

        for (int r = 0; r < Rows; r++)
        {
            for (int c = 0; c < Columns; c++)
            {
                float size = sizes[Random.Range(0, sizes.Length)];
                Vector3 position = Direction((c - (Columns - 1) * 0.5f) * spacing, 4f + (r - 0.5f) * spacing, radius);
                GazeTarget target = Spawn("T" + targets.Count, PrimitiveType.Sphere, position, Vector3.one * size, IdleColor);
                target.Index = targets.Count;
                targets.Add(target);
            }
        }
        NextGoal();
    }

    void NextGoal()
    {
        GazeTarget next;
        do next = targets[Random.Range(0, targets.Count)];
        while (next == goal);

        if (goal != null) goal.SetColor(IdleColor);
        goal = next;
        goal.SetColor(GoalColor);
        goalAborts = 0;
        dwellTime = dwellTimes[Random.Range(0, dwellTimes.Length)];
        goalSince = Time.time;
        label.text = $"Hold your gaze here for {dwellTime:0.#}s";
        label.transform.position = goal.transform.position + Vector3.up * (goal.Size * 0.5f + 0.4f);
    }

    protected override void Tick()
    {
        GazeTarget fired = TrackDwell(dwellTime);
        if (fired == null) return;

        bool correct = fired == goal;
        Trial(correct);
        Log(correct ? "Activate" : "FalseTrigger", fired.Id, fired.transform.position, correct,
            correct ? Time.time - goalSince : -1f, Difficulty(fired), $"abortsOnGoal={goalAborts}");

        if (!correct)
        {
            falseTriggers++;
            return;
        }

        activations++;
        if (activations % activationsPerLayout == 0)
        {
            layout++;
            BuildLayout();
        }
        else NextGoal();
    }

    protected override void OnDwellEnd(GazeTarget target, float heldSeconds, bool fired)
    {
        base.OnDwellEnd(target, heldSeconds, fired);
        if (fired || target != goal || heldSeconds < abortAfter) return;

        aborts++;
        goalAborts++;
        Log("DwellAborted", target.Id, target.transform.position, false, -1f, Difficulty(target), $"heldSeconds={F(heldSeconds)}");
    }

    string Difficulty(GazeTarget target) => $"dwell={F(dwellTime)};size={F(target.Size)};spacingDeg={F(spacing)}";

    protected override string Summary()
    {
        int attempts = activations + aborts;
        return $"activations={activations};falseTriggers={falseTriggers};abortedDwells={aborts};" +
               $"dwellSuccessPercent={F(attempts > 0 ? 100f * activations / attempts : 0f)}";
    }
}
