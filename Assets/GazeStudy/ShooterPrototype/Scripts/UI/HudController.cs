using TMPro;
using UnityEngine;
using UnityEngine.XR;

public class HudController : MonoBehaviour
{
    [SerializeField] PlaytestManager playtests;
    [SerializeField] TMP_Text modeLabel;
    [SerializeField] TMP_Text statsLabel;
    [SerializeField] TMP_Text headsetLabel;   // overlay canvases don't show in the headset

    string mode = "";

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
        string stats = $"Shots: {playtests.Shots}\nHits: {playtests.Hits}\nAccuracy: {playtests.Accuracy:0}%\nTimer: {Mathf.CeilToInt(playtests.TimeLeft)}s";
        modeLabel.text = title;
        statsLabel.text = stats;
        headsetLabel.text = XRSettings.isDeviceActive ? title + "\n" + stats : "";
    }
}
