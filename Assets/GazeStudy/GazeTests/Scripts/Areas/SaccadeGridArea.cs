using UnityEngine;

/// Playground 4. A grid of dots where one is lit at a time. Looking at the lit dot counts as a hit,
/// then the light jumps to a random dot. A dot that isn't reached before its interval ends is a miss.
/// Logs latency, landing error and overshoot for every jump.
public class SaccadeGridArea : GazeTestArea
{
    static readonly Color DimColor = new Color(0.25f, 0.25f, 0.3f);
    static readonly Color LitColor = new Color(1f, 0.85f, 0.1f);
    static readonly Color HitColor = new Color(0.2f, 0.9f, 0.3f);

    [SerializeField] int columns = 5;
    [SerializeField] int rows = 4;
    [SerializeField] float spacing = 0.9f;
    [SerializeField] float dotSize = 0.25f;
    [SerializeField] float distance = 6f;
    [SerializeField] float raise = 0.3f;
    [SerializeField] float[] intervals = { 1f, 1.5f, 2f };
    [SerializeField] float settleSeconds = 0.25f; // watched after landing, to catch overshoot

    GazeTarget[] dots;
    GazeTarget lit;
    Vector3 from;
    float litTime, interval, landedTime, latency, maxAlong;
    bool landed;
    int trials, hits;
    float latencySum, overshootSum;

    protected override GazeTarget Focus => lit;

    protected override void Begin()
    {
        trials = hits = 0;
        latencySum = overshootSum = 0f;
        dots = new GazeTarget[columns * rows];
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < columns; c++)
            {
                int i = r * columns + c;
                Vector3 position = Point((c - (columns - 1) * 0.5f) * spacing, (r - (rows - 1) * 0.5f) * spacing + raise, distance);
                dots[i] = Spawn("Dot" + i, PrimitiveType.Sphere, position, Vector3.one * dotSize, DimColor);
                dots[i].Index = i;
            }
        }
        lit = null;
        from = Point(0f, raise, distance);
        Jump();
    }

    void Jump()
    {
        if (lit != null)
        {
            from = lit.transform.position;
            lit.SetColor(DimColor);
        }

        GazeTarget next;
        do next = dots[Random.Range(0, dots.Length)];
        while (next == lit);

        lit = next;
        lit.SetColor(LitColor);
        litTime = Time.time;
        interval = intervals[Random.Range(0, intervals.Length)];
        landed = false;
        maxAlong = float.MinValue;
    }

    protected override void Tick()
    {
        Vector3 to = lit.transform.position;
        Vector3 jump = to - from;

        // How far the gaze has travelled past the dot, along the line of the jump.
        if (HasAim && jump.sqrMagnitude > 0.0001f && new Plane(Forward, to).Raycast(Aim, out float enter))
            maxAlong = Mathf.Max(maxAlong, Vector3.Dot(Aim.GetPoint(enter) - from, jump.normalized) - jump.magnitude);

        if (!landed)
        {
            if (Looked == lit)
            {
                landed = true;
                landedTime = Time.time;
                latency = Time.time - litTime;
                lit.SetColor(HitColor);
            }
            else if (Time.time - litTime >= interval) Finish(false);
        }
        else if (Time.time - landedTime >= settleSeconds) Finish(true);
    }

    void Finish(bool hit)
    {
        Vector3 to = lit.transform.position;
        Vector3 local = Quaternion.Inverse(Facing) * (to - from);
        float amplitude = Vector3.Angle(from - Origin, to - Origin);
        float direction = Mathf.Atan2(local.y, local.x) * Mathf.Rad2Deg;
        float overshoot = hit ? Mathf.Atan2(Mathf.Max(0f, maxAlong), Vector3.Distance(Origin, to)) * Mathf.Rad2Deg : -1f;

        trials++;
        if (hit)
        {
            hits++;
            latencySum += latency;
            overshootSum += overshoot;
        }

        Trial(hit);
        Log("Saccade", lit.Id, to, hit, hit ? latency : -1f,
            $"dotSize={F(dotSize)};amplitudeDeg={F(amplitude)};directionDeg={F(direction)};interval={F(interval)}",
            hit ? $"overshootDeg={F(overshoot)}" : "");
        Jump();
    }

    protected override string Summary() =>
        $"trials={trials};hits={hits};meanLatency={F(hits > 0 ? latencySum / hits : 0f)};meanOvershootDeg={F(hits > 0 ? overshootSum / hits : 0f)}";
}
