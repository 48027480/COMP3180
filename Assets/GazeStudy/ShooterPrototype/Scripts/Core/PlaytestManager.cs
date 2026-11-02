using COMP3180.GazeStudy;
using UnityEngine;

public enum TestState { Idle, Running, Ended }

/// Stepping on an area's stand marker starts a timed test there and stops the others. At zero the
/// result is logged and saved, and only Play again restarts that area. A test cut short (another
/// marker, the reset pad, quitting) is saved too, marked as not completed.
public class PlaytestManager : MonoBehaviour
{
    [SerializeField] Transform head;
    [SerializeField] PlaytestArea[] areas;   // order: Area1, Area2, Area3
    [SerializeField] WorldButton playAgainButton;
    [SerializeField] float markerRadius = 0.6f;
    [SerializeField] float testDuration = 60f;
    [SerializeField] Vector3 playAgainOffset = new Vector3(0f, 1.6f, 3f); // from the stand point, in the area's space

    int current = -1;

    public TestState State { get; private set; }
    public bool IsRunning => State == TestState.Running;
    public int Shots { get; private set; }
    public int Hits { get; private set; }
    public float TimeLeft { get; private set; }
    public float Accuracy => Shots > 0 ? (Hits / (float)Shots) * 100f : 0f;

    void OnEnable()
    {
        GameEvents.ShotFired += OnShot;
        GameEvents.TargetHit += OnHit;
        playAgainButton.Pressed += PlayAgain;
    }

    void OnDisable()
    {
        GameEvents.ShotFired -= OnShot;
        GameEvents.TargetHit -= OnHit;
        playAgainButton.Pressed -= PlayAgain;
    }

    void OnApplicationQuit()
    {
        if (IsRunning) FinishTest(false);
    }

    void OnShot() { if (IsRunning) Shots++; }
    void OnHit(Shootable _) { if (IsRunning) Hits++; }

    void Update()
    {
        for (int i = 0; i < areas.Length; i++)
        {
            Vector3 offset = head.position - areas[i].StandPoint.position;
            offset.y = 0f;
            if (offset.magnitude <= markerRadius) Select(i);
        }

        if (!IsRunning) return;

        TimeLeft = Mathf.Max(0f, TimeLeft - Time.deltaTime);
        if (TimeLeft <= 0f) FinishTest(true);
    }

    public void Select(int index)
    {
        if (areas == null || index < 0 || index >= areas.Length || index == current) return;

        if (IsRunning) FinishTest(false);
        StartTest(index);
    }

    public void StopAll()
    {
        if (IsRunning) FinishTest(false);
        foreach (var area in areas) area.Deactivate();
        current = -1;
        State = TestState.Idle;
        playAgainButton.gameObject.SetActive(false);
        GameEvents.RaisePlaytestChanged(-1, "");
    }

    void PlayAgain()
    {
        if (State == TestState.Ended) StartTest(current);
    }

    void StartTest(int index)
    {
        for (int i = 0; i < areas.Length; i++)
            if (i != index) areas[i].Deactivate();

        current = index;
        Shots = 0;
        Hits = 0;
        TimeLeft = testDuration;
        State = TestState.Running;
        playAgainButton.gameObject.SetActive(false);
        areas[index].Activate();
        GameEvents.RaisePlaytestChanged(index, areas[index].DisplayName);
    }

    void FinishTest(bool completed)
    {
        State = TestState.Ended;
        PlaytestArea area = areas[current];
        area.Deactivate();

        float duration = testDuration - TimeLeft;
        Debug.Log($"[Playtest] {area.DisplayName}: Shots {Shots}, Hits {Hits}, Accuracy {Accuracy:0.##}%, Duration {duration:0.##}s, Completed {completed}");
        string path = TestResultsCsv.Save(area.DisplayName, Shots, Hits, Accuracy, duration, completed);
        Debug.Log("[Playtest] Saved to " + path);

        Transform stand = area.StandPoint;
        playAgainButton.transform.SetPositionAndRotation(stand.TransformPoint(playAgainOffset), stand.rotation);
        playAgainButton.gameObject.SetActive(true);
    }
}
