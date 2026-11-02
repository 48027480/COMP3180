using UnityEngine;

public abstract class PlaytestArea : MonoBehaviour
{
    [SerializeField] string displayName = "Playtest";
    [SerializeField] Transform standPoint;
    [SerializeField] protected Transform targetsContainer; // MUST have scale (1,1,1)
    [SerializeField] BoxCollider border;                   // invisible, marks where the area ends

    public string DisplayName => displayName;
    public Transform StandPoint => standPoint;
    public bool Contains(Vector3 point) => border.bounds.Contains(point);
    public bool IsRunning { get; private set; }

    public void Activate()
    {
        ClearTargets();
        IsRunning = true;
        SetSignVisible(false);
        OnActivated();
    }

    public void Deactivate()
    {
        IsRunning = false;
        OnDeactivated();
        ClearTargets();
        SetSignVisible(true);
    }

    void SetSignVisible(bool visible)
    {
        Transform sign = transform.Find("Sign");
        if (sign != null) sign.gameObject.SetActive(visible);
    }

    protected abstract void OnActivated();
    protected abstract void OnDeactivated();

    void ClearTargets()
    {
        if (targetsContainer == null) return;
        for (int i = targetsContainer.childCount - 1; i >= 0; i--)
            Destroy(targetsContainer.GetChild(i).gameObject);
    }

    protected static Vector3 RandomPointInBox(BoxCollider box)
    {
        Vector3 h = box.size * 0.5f;
        Vector3 local = box.center + new Vector3(
            Random.Range(-h.x, h.x),
            Random.Range(-h.y, h.y),
            Random.Range(-h.z, h.z));
        return box.transform.TransformPoint(local);
    }
}
