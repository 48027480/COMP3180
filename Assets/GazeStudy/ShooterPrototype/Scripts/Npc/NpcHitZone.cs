using System.Globalization;
using UnityEngine;

public enum HitZone { Head, Body, Legs }

public class NpcHitZone : Shootable
{
    [SerializeField] NpcHealth health;
    [SerializeField] HitZone zone = HitZone.Body;
    [SerializeField] float damage = 33.34f;
    [SerializeField] FloatingText textPrefab;
    [SerializeField] Color textColor = Color.yellow;

    void Awake()
    {
        disappearOnShot = false;   // the NPC disappears through NpcHealth, not this component
        if (health == null) health = GetComponentInParent<NpcHealth>();
    }

    protected override void HandleShot(ShotInfo info)
    {
        if (health == null || health.IsDead) return;

        health.ApplyDamage(damage);

        if (textPrefab != null)
        {
            string label = zone == HitZone.Head
                ? "HEADSHOT"
                : damage.ToString("0.##", CultureInfo.InvariantCulture) + "HP";

            var ft = Instantiate(textPrefab, info.point, Quaternion.identity);  // not parented, survives NPC death
            ft.Show(label, textColor);
        }
    }
}
