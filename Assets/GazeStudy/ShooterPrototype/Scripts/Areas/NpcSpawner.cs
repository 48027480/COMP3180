using UnityEngine;

public class NpcSpawner : MonoBehaviour
{
    [SerializeField] GameObject npcPrefab;            // NPC
    [SerializeField] Transform endPoint;              // side B
    [SerializeField] Vector2 spawnIntervalRange = new Vector2(2f, 4f);
    [SerializeField] Vector2 speedRange = new Vector2(1.5f, 3f);
    [SerializeField] int maxAliveFromThisSpawner = 4;

    Transform container;
    bool running;
    float nextSpawnTime;
    int aliveCount;

    public void Begin(Transform npcContainer)
    {
        container = npcContainer;
        running = true;
        nextSpawnTime = Time.time + 0.5f;
    }

    public void Stop() => running = false;

    void Update()
    {
        if (!running || Time.time < nextSpawnTime) return;
        nextSpawnTime = Time.time + Random.Range(spawnIntervalRange.x, spawnIntervalRange.y);
        if (aliveCount >= maxAliveFromThisSpawner) return;
        Spawn();
    }

    void Spawn()
    {
        var go = Instantiate(npcPrefab, transform.position, Quaternion.identity, container);
        if (go.TryGetComponent(out NpcWalker walker))
            walker.Init(endPoint.position, Random.Range(speedRange.x, speedRange.y));

        aliveCount++;
        if (go.TryGetComponent(out NpcHealth hp))
            hp.Removed += () => aliveCount--;
    }
}
