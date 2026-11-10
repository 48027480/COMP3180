using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

/// Playground 5. One orb moves along a circle, a figure-8 and a sine wave in turn. Its speed changes
/// suddenly and it goes invisible for a moment now and then. Time with the gaze on the orb fills a ring.
/// Every gaze sample is logged with the gaze and orb speeds, so pursuit gain can be worked out later.
public class SmoothPursuitArea : GazeTestArea
{
    const int RingSegments = 48;
    static readonly string[] PathNames = { "Circle", "Figure8", "Sine" };
    static readonly Color OffColor = new Color(0.95f, 0.5f, 0.1f);
    static readonly Color OnColor = new Color(0.2f, 0.9f, 0.3f);

    [SerializeField] float distance = 6f;
    [SerializeField] float orbSize = 0.4f;
    [SerializeField] float pathRadius = 1.8f;
    [SerializeField] float raise = 0.4f;
    [SerializeField] float lapsPerSecond = 0.12f;
    [SerializeField] float secondsPerPath = 12f;
    [SerializeField] float[] speeds = { 0.6f, 1f, 1.6f };
    [SerializeField] Vector2 speedChangeEvery = new Vector2(3f, 5f);
    [SerializeField] Vector2 hideEvery = new Vector2(5f, 8f);
    [SerializeField] float hideSeconds = 0.5f;
    [SerializeField] float ringSeconds = 3f;

    public override string AttemptsLabel => "Samples";
    public override string HitsLabel => "On target";

    GazeTarget orb;
    LineRenderer ring;
    TextMeshPro label;
    int path;
    float phase, speed, nextSpeedChange, nextHide, hideUntil, ringTime;
    float gazeSpeed, orbSpeed, errorSum, gainSum, onTime;
    int samples, onSamples, gainSamples, rings;
    Vector3 lastGaze, lastOrb;
    bool hadAim, hasLast;

    protected override GazeTarget Focus => orb;

    protected override void Begin()
    {
        phase = ringTime = errorSum = gainSum = onTime = 0f;
        samples = onSamples = gainSamples = rings = 0;
        path = 0;
        speed = 1f;
        hadAim = hasLast = false;
        hideUntil = 0f;
        nextSpeedChange = Time.time + Random.Range(speedChangeEvery.x, speedChangeEvery.y);
        nextHide = Time.time + Random.Range(hideEvery.x, hideEvery.y);

        orb = Spawn("Orb", PrimitiveType.Sphere, PathPoint(), Vector3.one * orbSize, OffColor);
        label = Label("", Point(0f, raise + pathRadius + 1f, distance), 3f);

        ring = new GameObject("Ring").AddComponent<LineRenderer>();
        ring.transform.SetParent(targetsContainer, false);
        ring.useWorldSpace = true;
        ring.startWidth = ring.endWidth = 0.05f;
        ring.sharedMaterial = TargetMaterial;
        ring.shadowCastingMode = ShadowCastingMode.Off;
    }

    protected override void Tick()
    {
        float dt = Time.deltaTime;

        if (Time.time >= nextSpeedChange)
        {
            speed = speeds[Random.Range(0, speeds.Length)];
            nextSpeedChange = Time.time + Random.Range(speedChangeEvery.x, speedChangeEvery.y);
            Log("SpeedChange", orb.Id, orb.transform.position, null, -1f, SampleDifficulty());
        }
        if (Time.time >= nextHide)
        {
            hideUntil = Time.time + hideSeconds;
            nextHide = Time.time + Random.Range(hideEvery.x, hideEvery.y);
            Log("Disappear", orb.Id, orb.transform.position, null, -1f, SampleDifficulty());
        }

        int newPath = (int)(Elapsed / secondsPerPath) % PathNames.Length;
        if (newPath != path)
        {
            path = newPath;
            hasLast = false; // the orb jumps to the new path, which isn't real movement
            Log("PathChange", orb.Id, orb.transform.position, null, -1f, SampleDifficulty());
        }

        phase += lapsPerSecond * speed * dt;
        Vector3 position = PathPoint();
        orb.transform.position = position;
        orb.Visible = Time.time >= hideUntil;

        Vector3 orbDirection = (position - Origin).normalized;
        if (hasLast && dt > 0f)
        {
            orbSpeed = Vector3.Angle(lastOrb, orbDirection) / dt;
            gazeSpeed = HasAim && hadAim ? Vector3.Angle(lastGaze, Aim.direction) / dt : 0f;
        }
        lastOrb = orbDirection;
        lastGaze = Aim.direction;
        hadAim = HasAim;
        hasLast = true;

        bool on = Looked == orb;
        orb.SetColor(on ? OnColor : OffColor);
        if (on)
        {
            onTime += dt;
            ringTime += dt;
            if (ringTime >= ringSeconds)
            {
                ringTime -= ringSeconds;
                rings++;
            }
        }
        DrawRing(position, ringTime / ringSeconds);
        label.text = $"Keep your gaze on the orb\nTime on target: {onTime:0.0}s   Rings: {rings}";
    }

    Vector3 PathPoint()
    {
        float a = phase * Mathf.PI * 2f;
        float x, y;
        switch (path)
        {
            case 0: x = Mathf.Cos(a); y = Mathf.Sin(a); break;
            case 1: x = Mathf.Sin(a); y = 0.6f * Mathf.Sin(2f * a); break;
            default: x = 1.4f * Mathf.Sin(a); y = 0.6f * Mathf.Sin(3f * a); break;
        }
        return Point(x * pathRadius, y * pathRadius + raise, distance);
    }

    void DrawRing(Vector3 centre, float filled)
    {
        int count = Mathf.CeilToInt(filled * RingSegments) + 1;
        ring.enabled = count >= 2;
        if (!ring.enabled) return;

        float radius = orbSize * 0.5f + 0.12f;
        ring.positionCount = count;
        for (int i = 0; i < count; i++)
        {
            float a = Mathf.Min(i / (float)RingSegments, filled) * Mathf.PI * 2f;
            ring.SetPosition(i, centre + Facing * new Vector3(Mathf.Sin(a), Mathf.Cos(a), 0f) * radius);
        }
    }

    protected override void OnSample()
    {
        bool on = Looked == orb;
        Trial(on);
        samples++;
        if (on) onSamples++;
        if (HasAim) errorSum += AngleTo(orb.transform.position);
        if (HasAim && orbSpeed > 1f)
        {
            gainSum += gazeSpeed / orbSpeed;
            gainSamples++;
        }
    }

    protected override string SampleDifficulty() =>
        $"path={PathNames[path]};speed={F(speed)};visible={(Time.time >= hideUntil ? 1 : 0)};size={F(orbSize)}";

    protected override string SampleMeasures() => $"gazeDegPerSec={F(gazeSpeed)};orbDegPerSec={F(orbSpeed)}";

    protected override string Summary() =>
        $"timeOnTarget={F(onTime)};onTargetPercent={F(samples > 0 ? 100f * onSamples / samples : 0f)};" +
        $"meanErrorDeg={F(samples > 0 ? errorSum / samples : 0f)};meanGain={F(gainSamples > 0 ? gainSum / gainSamples : 0f)};rings={rings}";
}
