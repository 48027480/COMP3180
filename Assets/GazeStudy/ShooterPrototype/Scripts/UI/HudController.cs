using TMPro;
using UnityEngine;
using UnityEngine.XR;

public class HudController : MonoBehaviour
{
    [SerializeField] TMP_Text modeLabel;
    [SerializeField] TMP_Text statsLabel;
    [SerializeField] TMP_Text headsetLabel;   // overlay canvases don't show in the headset

    int shots, hits;
    string mode = "";

    void OnEnable()
    {
        GameEvents.ShotFired += OnShot;
        GameEvents.TargetHit += OnHit;
        GameEvents.PlaytestChanged += OnModeChanged;
    }

    void OnDisable()
    {
        GameEvents.ShotFired -= OnShot;
        GameEvents.TargetHit -= OnHit;
        GameEvents.PlaytestChanged -= OnModeChanged;
    }

    void OnShot() { shots++; Refresh(); }
    void OnHit(Shootable _) { hits++; Refresh(); }

    void OnModeChanged(int index, string name)
    {
        shots = 0; hits = 0;
        mode = index >= 0 ? $"[{index + 1}] {name}" : "";
        modeLabel.text = mode;
        if (index >= 0) Refresh();
        else { statsLabel.text = ""; headsetLabel.text = ""; }
    }

    void Refresh()
    {
        float acc = shots > 0 ? (hits / (float)shots) * 100f : 0f;
        statsLabel.text = $"Shots: {shots}\nHits: {hits}\nAccuracy: {acc:0}%";
        headsetLabel.text = XRSettings.isDeviceActive ? $"{mode}\nShots: {shots}   Hits: {hits}   Accuracy: {acc:0}%" : "";
    }
}
