using System.Text;
using UnityEngine;

/// Playground 7. A curved wall of similar objects hides one that is different. The player finds it and
/// confirms by holding their gaze on it or by pressing grab. The number of objects, how similar the odd
/// one is and where it sits (out to the periphery) change every trial. Logs search time, the order the
/// objects were looked at, and revisits.
public class VisualSearchArea : GazeTestArea
{
    const int MaxPathLength = 400;
    static readonly string[] SimilarityNames = { "Easy", "Medium", "Hard" };
    static readonly Color DistractorColor = new Color(0.2f, 0.45f, 0.9f);
    static readonly Color EasyColor = new Color(0.9f, 0.2f, 0.2f);
    static readonly Color HardColor = new Color(0.2f, 0.62f, 0.9f);
    static readonly Color WrongColor = new Color(0.15f, 0.15f, 0.18f);

    [SerializeField] float radius = 6f;
    [SerializeField] float objectSize = 0.35f;
    [SerializeField] float dwellSeconds = 1f;
    [SerializeField] float yawSpan = 45f;
    [SerializeField] Vector2 pitchRange = new Vector2(-8f, 28f);

    static readonly Vector2Int[] Grids = { new Vector2Int(4, 3), new Vector2Int(6, 4), new Vector2Int(8, 5) };

    readonly StringBuilder scanPath = new StringBuilder();
    GazeTarget odd;
    int[] visits;
    int setSize, similarity, found, wrongPicks;
    float trialStart, oddEccentricity, searchSum;

    protected override void Begin()
    {
        found = wrongPicks = 0;
        searchSum = 0f;
        NewTrial();
    }

    void NewTrial()
    {
        ClearStimuli();
        Label("Find the odd one out. Hold your gaze on it, or press grab.", Direction(0f, pitchRange.y + 9f, radius), 2.5f);

        Vector2Int grid = Grids[Random.Range(0, Grids.Length)];
        setSize = grid.x * grid.y;
        similarity = Random.Range(0, SimilarityNames.Length);
        int oddIndex = Random.Range(0, setSize);
        visits = new int[setSize];
        scanPath.Clear();

        for (int r = 0; r < grid.y; r++)
        {
            for (int c = 0; c < grid.x; c++)
            {
                int i = r * grid.x + c;
                bool isOdd = i == oddIndex;
                // Easy: another colour and shape. Medium: another shape. Hard: a slightly different colour.
                PrimitiveType shape = isOdd && similarity < 2 ? PrimitiveType.Cube : PrimitiveType.Sphere;
                Color color = !isOdd ? DistractorColor : similarity == 0 ? EasyColor : similarity == 1 ? DistractorColor : HardColor;
                float yaw = Mathf.Lerp(-yawSpan, yawSpan, c / (float)(grid.x - 1));
                float pitch = Mathf.Lerp(pitchRange.x, pitchRange.y, r / (float)(grid.y - 1));

                GazeTarget item = Spawn("Item" + i, shape, Direction(yaw, pitch, radius), Vector3.one * objectSize, color);
                item.Index = i;
                if (isOdd) odd = item;
            }
        }

        oddEccentricity = Vector3.Angle(Forward, odd.transform.position - Origin);
        trialStart = Time.time;
    }

    protected override void OnDwellStart(GazeTarget target)
    {
        visits[target.Index]++;
        if (scanPath.Length < MaxPathLength) scanPath.Append(target.Index).Append('>');
    }

    protected override void Tick()
    {
        GazeTarget picked = TrackDwell(dwellSeconds);
        string confirm = "Dwell";
        if (picked == null && Confirm && Looked != null)
        {
            picked = Looked;
            confirm = "Button";
            SpendDwell();
        }
        if (picked == null) return;

        bool correct = picked == odd;
        int looked = 0, revisits = 0;
        foreach (int v in visits)
        {
            if (v > 0) looked++;
            revisits += Mathf.Max(0, v - 1);
        }

        float searchTime = Time.time - trialStart;
        Trial(correct);
        Log(correct ? "Found" : "WrongPick", picked.Id, picked.transform.position, correct, searchTime,
            $"setSize={setSize};similarity={SimilarityNames[similarity]};oddEccentricityDeg={F(oddEccentricity)};size={F(objectSize)}",
            $"confirm={confirm};objectsLooked={looked};revisits={revisits};scanPath={scanPath}");

        if (!correct)
        {
            wrongPicks++;
            picked.SetColor(WrongColor);
            return;
        }

        found++;
        searchSum += searchTime;
        NewTrial();
    }

    protected override string Summary() =>
        $"found={found};wrongPicks={wrongPicks};meanSearchSeconds={F(found > 0 ? searchSum / found : 0f)}";
}
