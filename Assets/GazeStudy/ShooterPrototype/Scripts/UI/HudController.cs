using TMPro;
using UnityEngine;
using UnityEngine.XR;

public class HudController : MonoBehaviour
{
    const float FadeSeconds = 0.5f;

    [SerializeField] PlaytestManager playtests;
    [SerializeField] TMP_Text modeLabel;
    [SerializeField] TMP_Text statsLabel;
    [SerializeField] TMP_Text headsetLabel;   // overlay canvases don't show in the headset

    string mode = "";
    float visibility = 1f;

    void OnEnable() => GameEvents.PlaytestChanged += OnModeChanged;
    void OnDisable() => GameEvents.PlaytestChanged -= OnModeChanged;

    void OnModeChanged(int index, string name) => mode = index >= 0 ? $"[{index + 1}] {name}" : "";

    void Update()
    {
        if (playtests.State == TestState.Idle)
        {
            modeLabel.text = "";
            statsLabel.text = "";
            headsetLabel.text = "";
            return;
        }

        string title = playtests.IsRunning ? mode : mode + " - finished";
        PlaytestArea area = playtests.CurrentArea;
        string stats = $"{area.AttemptsLabel}: {playtests.Shots}\n{area.HitsLabel}: {playtests.Hits}\nAccuracy: {playtests.Accuracy:0}%\nTimer: {Mathf.CeilToInt(playtests.TimeLeft)}s";
        modeLabel.text = title;
        statsLabel.text = stats;
        headsetLabel.text = XRSettings.isDeviceActive ? title + "\n" + stats : "";

        visibility = Mathf.MoveTowards(visibility, playtests.PlayerInArea ? 1f : 0f, Time.deltaTime / FadeSeconds);
        modeLabel.alpha = visibility;
        statsLabel.alpha = visibility;
        headsetLabel.alpha = visibility;
    }
}
