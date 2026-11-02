using UnityEngine;

public class NpcArea : PlaytestArea
{
    [SerializeField] NpcSpawner[] spawners;   // SpawnerLeft, SpawnerRight

    protected override void OnActivated()
    {
        foreach (var s in spawners) s.Begin(targetsContainer);
    }

    protected override void OnDeactivated()
    {
        foreach (var s in spawners) s.Stop();
    }
}
