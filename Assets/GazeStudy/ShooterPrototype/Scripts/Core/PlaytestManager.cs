using UnityEngine;

/// Stepping on an area's stand marker starts that area and stops the others.
public class PlaytestManager : MonoBehaviour
{
    [SerializeField] Transform head;
    [SerializeField] PlaytestArea[] areas;   // order: Area1, Area2, Area3
    [SerializeField] float markerRadius = 0.6f;

    int current = -1;

    void Update()
    {
        for (int i = 0; i < areas.Length; i++)
        {
            Vector3 offset = head.position - areas[i].StandPoint.position;
            offset.y = 0f;
            if (offset.magnitude <= markerRadius) Select(i);
        }
    }

    public void Select(int index)
    {
        if (areas == null || index < 0 || index >= areas.Length || index == current) return;

        for (int i = 0; i < areas.Length; i++)
            if (i != index) areas[i].Deactivate();

        current = index;
        areas[index].Activate();
        GameEvents.RaisePlaytestChanged(index, areas[index].DisplayName);
    }

    public void StopAll()
    {
        foreach (var area in areas) area.Deactivate();
        current = -1;
        GameEvents.RaisePlaytestChanged(-1, "");
    }
}
