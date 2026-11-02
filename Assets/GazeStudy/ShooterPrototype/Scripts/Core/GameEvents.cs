using System;
using UnityEngine;

public static class GameEvents
{
    public static event Action ShotFired;
    public static event Action<Shootable> TargetHit;
    public static event Action<int, string> PlaytestChanged; // index (0-based, -1 for none), display name

    public static void RaiseShotFired() => ShotFired?.Invoke();
    public static void RaiseTargetHit(Shootable s) => TargetHit?.Invoke(s);
    public static void RaisePlaytestChanged(int index, string name) => PlaytestChanged?.Invoke(index, name);

    // Needed when Enter Play Mode Options has Domain Reload disabled.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        ShotFired = null;
        TargetHit = null;
        PlaytestChanged = null;
    }
}
