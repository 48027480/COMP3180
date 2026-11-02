using COMP3180.GazeStudy;
using UnityEngine;

/// Fires along the playground's aim ray (eye tracker, controller or camera) while the gun is equipped
/// inside the prototype zone.
public class HitscanWeapon : MonoBehaviour
{
    [SerializeField] GazePickupSelector selector;
    [SerializeField] GrabInput input;
    [SerializeField] float range = 200f;
    [SerializeField] LayerMask hitMask = Physics.DefaultRaycastLayers; // the ray starts inside the player's own collider
    [SerializeField] float fireCooldown = 0.12f;
    [SerializeField] bool automatic = false;
    [SerializeField] bool debugRay = true;

    float nextFireTime;

    void Update()
    {
        if (!selector.GunEquipped || !selector.InPrototype || !selector.HasRay) return;
        if (selector.HoveredButton != null) return; // the same press works the button instead

        bool wantsFire = (automatic ? input.FireHeld : input.FirePressedThisFrame) || selector.AssistWink;
        if (wantsFire && Time.time >= nextFireTime)
        {
            nextFireTime = Time.time + fireCooldown;
            Fire(selector.CurrentRay);
        }
    }

    void Fire(Ray ray)
    {
        GameEvents.RaiseShotFired();

        if (Physics.Raycast(ray, out RaycastHit hit, range, hitMask, QueryTriggerInteraction.Ignore))
        {
            if (debugRay) Debug.DrawLine(ray.origin, hit.point, Color.green, 0.2f);

            var shootable = hit.collider.GetComponentInParent<Shootable>();
            if (shootable != null)
            {
                shootable.OnShot(new ShotInfo
                {
                    point = hit.point,
                    direction = ray.direction,
                    distance = hit.distance
                });
            }
        }
        else if (debugRay)
        {
            Debug.DrawRay(ray.origin, ray.direction * range, Color.red, 0.2f);
        }
    }
}
