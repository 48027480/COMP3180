using UnityEngine;

public class FloatingTargetArea : PlaytestArea
{
    [SerializeField] GameObject targetPrefab;          // FloatingTarget_Sphere
    [SerializeField] BoxCollider spawnVolume;           // ground footprint
    [SerializeField] int maxAlive = 12;
    [SerializeField] float spawnInterval = 0.6f;
    [SerializeField] Vector2 diameterRange = new Vector2(0.5f, 1.5f);
    [SerializeField] Vector2 riseSpeedRange = new Vector2(2f, 6f);
    [SerializeField] float maxHeight = 25f;             // meters above spawn height

    float nextSpawnTime;

    protected override void OnActivated() => nextSpawnTime = Time.time;
    protected override void OnDeactivated() { }

    void Update()
    {
        if (!IsRunning) return;
        if (targetsContainer.childCount >= maxAlive || Time.time < nextSpawnTime) return;
        nextSpawnTime = Time.time + spawnInterval;
        Spawn();
    }

    void Spawn()
    {
        Vector3 p = RandomPointInBox(spawnVolume);
        p.y = spawnVolume.bounds.min.y;

        var go = Instantiate(targetPrefab, p, Quaternion.identity, targetsContainer);
        go.transform.localScale = Vector3.one * Random.Range(diameterRange.x, diameterRange.y);

        if (go.TryGetComponent(out FloatingTarget ft))
            ft.Init(Random.Range(riseSpeedRange.x, riseSpeedRange.y), p.y + maxHeight);
    }
}
