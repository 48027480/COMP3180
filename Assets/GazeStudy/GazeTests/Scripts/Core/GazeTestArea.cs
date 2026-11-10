using System.Globalization;
using COMP3180.GazeStudy;
using TMPro;
using UnityEngine;

/// Base for the gaze playgrounds. It reads the playground's aim ray (eye tracker when it works, head
/// or controller otherwise), finds the GazeTarget being looked at, tracks dwell, and logs every event
/// and a steady stream of gaze samples to the gaze log. Stimuli are laid out around where the head
/// was when the test started, facing into the bay.
[DefaultExecutionOrder(10)] // after the selector has this frame's ray
public abstract class GazeTestArea : PlaytestArea
{
    [SerializeField] protected GazePickupSelector selector;
    [SerializeField] GrabInput grab;
    [SerializeField] protected Transform head;
    [SerializeField] Material targetMaterial;
    [SerializeField] float testSeconds = 45f;
    [SerializeField] float sampleInterval = 0.05f;
    [SerializeField] float aimDistance = 60f;

    public override float Duration => testSeconds;
    public override string AttemptsLabel => "Trials";
    public override string HitsLabel => "Correct";

    protected Vector3 Origin { get; private set; }
    protected Quaternion Facing { get; private set; }
    protected Vector3 Forward => Facing * Vector3.forward;
    protected bool HasAim { get; private set; }
    protected Ray Aim { get; private set; }
    protected GazeTarget Looked { get; private set; }
    protected bool Confirm => grab.GrabPressedThisFrame || selector.AssistWink;
    protected float Elapsed => Time.time - startTime;
    protected Material TargetMaterial => targetMaterial;

    /// The target the player should be looking at now, if any. Samples are measured against it.
    protected virtual GazeTarget Focus => null;

    float startTime, nextSample, dwell;
    bool began, dwellSpent;
    GazeTarget dwellTarget;

    protected abstract void Begin();
    protected abstract void Tick();
    protected abstract string Summary();
    protected virtual void End() { }
    protected virtual void OnSample() { }
    protected virtual string SampleDifficulty() => "";
    protected virtual string SampleMeasures() => "";

    protected sealed override void OnActivated()
    {
        Origin = head.position;
        Facing = Quaternion.LookRotation(Vector3.ProjectOnPlane(transform.forward, Vector3.up));
        startTime = Time.time;
        nextSample = 0f;
        began = true;
        HasAim = false;
        Looked = null;
        ResetDwell();
        Log("Start", "", null, null);
        Begin();
    }

    // The manager also deactivates areas that never started.
    protected sealed override void OnDeactivated()
    {
        if (!began) return;
        began = false;
        StopAllCoroutines();
        Log("Summary", "", null, null, Elapsed, "", Summary());
        End();
        GazeLogCsv.Flush();
    }

    void Update()
    {
        if (!IsRunning) return;

        HasAim = selector.HasRay;
        Aim = selector.CurrentRay;
        Looked = null;
        if (HasAim && Physics.Raycast(Aim, out RaycastHit hit, aimDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            Looked = hit.collider.GetComponentInParent<GazeTarget>();

        Tick();

        if (Time.time < nextSample) return;
        nextSample = Time.time + sampleInterval;
        GazeTarget focus = Focus;
        string looked = Looked != null ? "looked=" + Looked.Id : "looked=";
        string extra = SampleMeasures();
        if (focus != null) Log("Sample", focus.Id, focus.transform.position, Looked == focus, -1f, SampleDifficulty(), extra == "" ? looked : looked + ";" + extra);
        else Log("Sample", "", null, null, -1f, SampleDifficulty(), extra == "" ? looked : looked + ";" + extra);
        OnSample();
    }

    // ---------------------------------------------------------------- logging

    protected void Log(string eventName, string targetId, Vector3? target, bool? hit, float reaction = -1f, string difficulty = "", string measures = "")
    {
        float error = target.HasValue ? AngleTo(target.Value) : -1f;
        GazeLogCsv.Add(DisplayName, eventName, selector.AimSource, Elapsed, HasAim ? Aim : (Ray?)null,
            targetId, target, hit, reaction, error, difficulty, measures);
    }

    /// Counts one trial towards the stats on the HUD and in the test results file.
    protected void Trial(bool success)
    {
        GameEvents.RaiseShotFired();
        if (success) GameEvents.RaiseTargetHit(null);
    }

    protected static string F(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    // ---------------------------------------------------------------- geometry

    /// Degrees between the aim ray and a point, or -1 with no ray.
    protected float AngleTo(Vector3 point) => HasAim ? Vector3.Angle(Aim.direction, point - Aim.origin) : -1f;

    /// A point given as right, up and forward of where the head was when the test started.
    protected Vector3 Point(float right, float up, float forward) => Origin + Facing * new Vector3(right, up, forward);

    /// A point at a distance from the head, yaw degrees to the right and pitch degrees up.
    protected Vector3 Direction(float yaw, float pitch, float radius) =>
        Origin + Facing * (Quaternion.Euler(-pitch, yaw, 0f) * Vector3.forward) * radius;

    // ---------------------------------------------------------------- stimuli

    protected GazeTarget Spawn(string id, PrimitiveType type, Vector3 position, Vector3 scale, Color color)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = id;
        go.transform.SetParent(targetsContainer, false);
        go.transform.SetPositionAndRotation(position, Facing);
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = targetMaterial;
        var target = go.AddComponent<GazeTarget>();
        target.Init(id, color);
        return target;
    }

    protected TextMeshPro Label(string text, Vector3 position, float size)
    {
        var label = new GameObject("Label", typeof(RectTransform)).AddComponent<TextMeshPro>();
        label.transform.SetParent(targetsContainer, false);
        label.transform.SetPositionAndRotation(position, Facing);
        label.rectTransform.sizeDelta = new Vector2(8f, 1f);
        label.fontSize = size;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.text = text;
        return label;
    }

    protected void ClearStimuli()
    {
        for (int i = targetsContainer.childCount - 1; i >= 0; i--) Destroy(targetsContainer.GetChild(i).gameObject);
        ResetDwell();
    }

    // ---------------------------------------------------------------- dwell

    /// Call every frame. Returns the looked-at target on the frame it has been held for the given time.
    /// It can't fire again until the gaze has left it.
    protected GazeTarget TrackDwell(float seconds)
    {
        if (Looked != dwellTarget)
        {
            if (dwellTarget != null) OnDwellEnd(dwellTarget, dwell, dwellSpent);
            dwellTarget = Looked;
            dwell = 0f;
            dwellSpent = false;
            if (dwellTarget != null) OnDwellStart(dwellTarget);
        }
        if (dwellTarget == null || dwellSpent) return null;

        dwell += Time.deltaTime;
        dwellTarget.Fill(Mathf.Clamp01(dwell / seconds));
        if (dwell < seconds) return null;

        dwellSpent = true;
        return dwellTarget;
    }

    /// Marks the looked-at target as used, for a selection made with a button instead of dwell.
    protected void SpendDwell() => dwellSpent = true;

    /// Forgets the dwell without callbacks, for when the targets have been replaced.
    protected void ResetDwell()
    {
        dwellTarget = null;
        dwell = 0f;
        dwellSpent = false;
    }

    protected virtual void OnDwellStart(GazeTarget target) { }
    protected virtual void OnDwellEnd(GazeTarget target, float heldSeconds, bool fired) => target.Fill(0f);
}
