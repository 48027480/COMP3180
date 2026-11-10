using UnityEngine;

/// Playground 8. The player keeps their gaze on a centre cross while brief flashes appear at 10, 20 and
/// 40 degrees away in different directions. A flash is detected by pressing grab or by looking at where
/// it was, within the response window. The cross also changes colour now and then: counting those
/// changes is the second task, and the true count is in the Summary row.
public class PeripheralAwarenessArea : GazeTestArea
{
    const int Directions = 8;
    static readonly Color FlashColor = Color.white;
    static readonly Color[] CrossColors =
    {
        new Color(0.95f, 0.95f, 0.95f), new Color(0.95f, 0.3f, 0.3f), new Color(0.3f, 0.85f, 0.4f), new Color(0.3f, 0.55f, 0.95f)
    };

    [SerializeField] float distance = 6f;
    [SerializeField] float[] eccentricities = { 10f, 20f, 40f };
    [SerializeField] float flashSeconds = 0.2f;
    [SerializeField] float flashSize = 0.3f;
    [SerializeField] float responseWindow = 1.5f;
    [SerializeField] Vector2 flashEvery = new Vector2(1.5f, 3f);
    [SerializeField] Vector2 colourChangeEvery = new Vector2(3f, 7f);
    [SerializeField] float lookTolerance = 6f;
    [SerializeField] float minHeight = 0.3f; // flashes below this would be in the floor

    GazeTarget centre, upright, flash;
    Vector3 flashPosition;
    float flashTime, nextFlash, nextColourChange, eccentricity, direction, centreErrorAtOnset;
    bool pending;
    int crossColor, colourChanges, falseAlarms;
    int[] shown, detected;

    protected override GazeTarget Focus => centre;

    protected override void Begin()
    {
        shown = new int[eccentricities.Length];
        detected = new int[eccentricities.Length];
        colourChanges = falseAlarms = crossColor = 0;
        pending = false;
        flash = null;

        Vector3 middle = Point(0f, 0f, distance);
        centre = Spawn("Centre", PrimitiveType.Cube, middle, new Vector3(0.4f, 0.08f, 0.08f), CrossColors[0]);
        upright = Spawn("Centre", PrimitiveType.Cube, middle, new Vector3(0.08f, 0.4f, 0.08f), CrossColors[0]);
        Label("Keep looking at the cross and count its colour changes.\nPress grab, or look, when you see a flash.", Point(0f, -0.8f, distance), 2f);

        nextFlash = Time.time + Random.Range(flashEvery.x, flashEvery.y);
        nextColourChange = Time.time + Random.Range(colourChangeEvery.x, colourChangeEvery.y);
    }

    protected override void Tick()
    {
        if (Time.time >= nextColourChange)
        {
            crossColor = (crossColor + Random.Range(1, CrossColors.Length)) % CrossColors.Length;
            centre.SetColor(CrossColors[crossColor]);
            upright.SetColor(CrossColors[crossColor]);
            colourChanges++;
            nextColourChange = Time.time + Random.Range(colourChangeEvery.x, colourChangeEvery.y);
            Log("ColourChange", centre.Id, centre.transform.position, null, -1f, "", $"count={colourChanges}");
        }

        if (!pending)
        {
            if (Confirm)
            {
                falseAlarms++;
                Log("FalseAlarm", centre.Id, centre.transform.position, false);
            }
            if (Time.time >= nextFlash) StartFlash();
            return;
        }

        float reaction = Time.time - flashTime;
        if (flash != null && reaction >= flashSeconds)
        {
            Destroy(flash.gameObject);
            flash = null;
        }

        if (Confirm) Resolve(true, "Button", reaction);
        else if (HasAim && AngleTo(flashPosition) <= lookTolerance) Resolve(true, "Look", reaction);
        else if (reaction >= responseWindow) Resolve(false, "None", -1f);
    }

    void StartFlash()
    {
        int level = 0;
        for (int attempt = 0; attempt < 20; attempt++)
        {
            level = Random.Range(0, eccentricities.Length);
            eccentricity = eccentricities[level];
            direction = Random.Range(0, Directions) * (360f / Directions);
            // Tilt away from straight ahead, then spin around the line of sight. 0 is right, 90 is up.
            Vector3 local = Quaternion.AngleAxis(direction, Vector3.forward) * (Quaternion.AngleAxis(eccentricity, Vector3.up) * Vector3.forward);
            flashPosition = Origin + Facing * local * distance;
            if (flashPosition.y >= minHeight) break;
        }

        shown[level]++;
        centreErrorAtOnset = AngleTo(centre.transform.position);
        flash = Spawn($"Flash_{F(eccentricity)}_{F(direction)}", PrimitiveType.Sphere, flashPosition, Vector3.one * flashSize, FlashColor);
        flash.Index = level;
        flashTime = Time.time;
        pending = true;
    }

    void Resolve(bool seen, string response, float reaction)
    {
        int level = System.Array.IndexOf(eccentricities, eccentricity);
        if (seen) detected[level]++;

        Trial(seen);
        Log("Flash", $"Flash_{F(eccentricity)}_{F(direction)}", flashPosition, seen, reaction,
            $"eccentricityDeg={F(eccentricity)};directionDeg={F(direction)};flashSeconds={F(flashSeconds)};size={F(flashSize)}",
            $"response={response};centreErrorAtOnsetDeg={F(centreErrorAtOnset)}");

        if (flash != null) Destroy(flash.gameObject);
        flash = null;
        pending = false;
        nextFlash = Time.time + Random.Range(flashEvery.x, flashEvery.y);
    }

    protected override string Summary()
    {
        string result = $"colourChanges={colourChanges};falseAlarms={falseAlarms}";
        for (int i = 0; i < eccentricities.Length; i++)
            result += $";detected{F(eccentricities[i])}deg={detected[i]}/{shown[i]}";
        return result;
    }
}
