using System;
using UnityEngine;

public struct ShotInfo
{
    public Vector3 point;
    public Vector3 direction;
    public float distance;
}

/// Anything with this component (on the hit collider's object or a parent) reacts to hitscan shots.
/// Default reaction: disappear. Subclasses override HandleShot.
public class Shootable : MonoBehaviour
{
    [SerializeField] protected bool disappearOnShot = true;

    public event Action<Shootable> Shot;

    public void OnShot(ShotInfo info)
    {
        Shot?.Invoke(this);
        GameEvents.RaiseTargetHit(this);
        HandleShot(info);
    }

    protected virtual void HandleShot(ShotInfo info)
    {
        if (disappearOnShot) Disappear();
    }

    public void Disappear() => Destroy(gameObject);
}
