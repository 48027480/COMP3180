using UnityEngine;

public class AimTrainerArea : PlaytestArea
{
    [SerializeField] GameObject targetPrefab;          // Target_Sphere
    [SerializeField] BoxCollider spawnVolume;
    [SerializeField] int maxAlive = 8;
    [SerializeField] Vector2 diameterRange = new Vector2(0.3f, 1.4f);
    [SerializeField] float respawnDelay = 0.25f;
    [SerializeField] int placementAttempts = 12;

    float nextSpawnTime;

    protected override void OnActivated() => nextSpawnTime = Time.time;
    protected override void OnDeactivated() { }

    void Update()
    {
        if (!IsRunning) return;
        if (targetsContainer.childCount >= maxAlive || Time.time < nextSpawnTime) return;
        if (TrySpawn()) nextSpawnTime = Time.time + respawnDelay;
    }

    bool TrySpawn()
    {
        float diameter = Random.Range(diameterRange.x, diameterRange.y);
        for (int i = 0; i < placementAttempts; i++)
        {
            Vector3 p = RandomPointInBox(spawnVolume);
            if (Overlaps(p, diameter)) continue;

            var go = Instantiate(targetPrefab, p, Quaternion.identity, targetsContainer);
            go.transform.localScale = Vector3.one * diameter;
            return true;
        }
        return false;
    }

    bool Overlaps(Vector3 pos, float diameter)
    {
        foreach (Transform t in targetsContainer)
            if (Vector3.Distance(t.position, pos) < (t.lossyScale.x + diameter) * 0.5f)
                return true;
        return false;
    }
}
