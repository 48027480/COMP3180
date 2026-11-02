using System;
using UnityEngine;

public class NpcHealth : MonoBehaviour
{
    [SerializeField] float maxHealth = 100f;

    public float Current { get; private set; }
    public bool IsDead { get; private set; }

    public event Action Died;
    public event Action Removed;   // fired on death AND when walked off the map (OnDestroy)

    void Awake() => Current = maxHealth;

    public void ApplyDamage(float amount)
    {
        if (IsDead) return;

        Current = Mathf.Max(0f, Current - amount);
        if (Current <= 0.001f)
        {
            Current = 0f;
            IsDead = true;
            Died?.Invoke();
            Destroy(gameObject);   // NPC disappears from the map
        }
    }

    void OnDestroy() => Removed?.Invoke();
}
