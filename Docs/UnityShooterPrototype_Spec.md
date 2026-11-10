# Unity 3D Hitscan Shooter Prototype — Build Spec

**Target:** Unity `6000.3.7f1`, 3D (URP) template.
**Audience:** an AI agent (Cowork) that will create every asset, script, prefab, and scene object, then wire all Inspector references.
**Rule for the agent:** follow this document literally. Names, paths, and field names are exact. After building, run the **Acceptance Checklist** (section 9).

---

## 1. Project setup

| Item | Value |
|---|---|
| Template | 3D (URP) |
| Packages | Input System (`com.unity.inputsystem`), uGUI / TextMeshPro (`com.unity.ugui`) |
| Project Settings > Player > Active Input Handling | `Input System Package (New)` (or `Both`) |
| TextMeshPro | Import "TMP Essential Resources" once |
| Scene | `Assets/Scenes/Prototype.unity` |
| Scale | 1 unit = 1 meter |
| Gravity / physics | Not used for gameplay. All movement is via `Transform`. No Rigidbodies anywhere |

Folder layout:

```
Assets/
  Scenes/Prototype.unity
  Materials/        M_Target, M_Floating, M_Floor, M_Wall, M_NpcHead, M_NpcBody, M_NpcLegs
  Prefabs/          Target_Sphere, FloatingTarget_Sphere, NPC, FloatingText
  Scripts/
    Core/           GameEvents.cs, PlaytestManager.cs, PlaytestArea.cs
    Player/         PlayerLook.cs, HitscanWeapon.cs
    Shooting/       Shootable.cs
    Areas/          AimTrainerArea.cs, FloatingTargetArea.cs, FloatingTarget.cs, NpcArea.cs, NpcSpawner.cs
    Npc/            NpcHealth.cs, NpcHitZone.cs, NpcWalker.cs
    UI/             FloatingText.cs, HudController.cs
```

---

## 2. Design summary

- **Player** stands still at a per-area `StandPoint`. Mouse look only (no walking). Left click fires a **hitscan** ray from the screen center.
- **`Shootable`** is the base component. If the ray hits a collider that has a `Shootable` (on itself or a parent), `OnShot` is invoked. Default behavior: the object is destroyed (disappears).
- **Three playtest areas** sit side by side. Only one is active at a time. Keys **1 / 2 / 3** switch area; the player is teleported to that area's `StandPoint` and the other areas' targets are cleared.
  1. **AimTrainerArea** — static spheres with random diameter and random position (including depth) inside a box volume. Shot target disappears, a new one spawns.
  2. **FloatingTargetArea** — spheres spawn at ground level inside a footprint and rise upward. At `maxHeight` they disappear. Shooting also removes them.
  3. **NpcArea** — two `NpcSpawner`s (left and right). NPCs walk from side A to side B. Each NPC has Head, Body, Legs hit zones sharing one HP pool of 100.

### NPC damage rules (exact)

| Zone | Damage per hit | Floating text | Hits to kill |
|---|---|---|---|
| Head | 100 | `HEADSHOT` | 1 |
| Body | 33.34 | `33.34HP` | 3 (100 - 3x33.34 = -0.02) |
| Legs | 20 | `20HP` | 5 |

Damage from different zones stacks on the same HP pool. At HP <= 0.001 the NPC is destroyed (disappears from the map).

---

## 3. Scene hierarchy (exact names, local positions relative to parent)

World layout: three areas along the X axis, each 30 x 30 m. Each area root faces +Z (players look toward +Z).

```
Prototype (scene)
├── Directional Light
├── Global Volume                       (optional, URP default profile)
├── GameManager                         [PlaytestManager]
├── Player                              [PlayerLook, HitscanWeapon]  pos (0,0,0), NO collider, NO rigidbody
│   └── CameraPivot                     pos (0,1.7,0)
│       └── Main Camera                 tag MainCamera, Camera + AudioListener, FOV 80
├── UI
│   ├── Canvas                          Screen Space - Overlay, CanvasScaler (Scale With Screen Size, ref 1920x1080)  [HudController on Canvas]
│   │   ├── Crosshair                   Image, 6x6 px white dot, anchored center
│   │   ├── ModeLabel                   TextMeshPro - Text (UI), anchored top-center, size 36
│   │   └── StatsLabel                  TextMeshPro - Text (UI), anchored top-left, size 28
├── Area1_AimTrainer                    [AimTrainerArea]  pos (-40,0,0)
│   ├── Floor                           Cube scale (30,0.2,30) pos (0,-0.1,0)  M_Floor
│   ├── BackWall                        Cube scale (30,14,0.5) pos (0,7,22)    M_Wall
│   ├── StandPoint                      empty, pos (0,0,-12), rot (0,0,0)
│   ├── StandMarker                     Cylinder scale (1.2,0.02,1.2) pos (0,0.01,-12) M_Floor tinted
│   ├── SpawnVolume                     empty + BoxCollider (isTrigger=true) center (0,4,10) size (22,8,16)
│   └── Targets                         empty container (scale MUST be 1,1,1)
├── Area2_Floating                      [FloatingTargetArea]  pos (0,0,0)
│   ├── Floor                           Cube scale (30,0.2,30) pos (0,-0.1,0)
│   ├── StandPoint                      empty, pos (0,0,-12), rot (0,0,0)
│   ├── StandMarker                     same as Area1
│   ├── SpawnVolume                     empty + BoxCollider (isTrigger=true) center (0,0.05,12) size (24,0.1,10)
│   └── Targets                         empty container (scale 1,1,1)
└── Area3_Npc                           [NpcArea]  pos (40,0,0)
    ├── Floor                           Cube scale (30,0.2,30) pos (0,-0.1,0)
    ├── BackWall                        Cube scale (30,6,0.5) pos (0,3,14)
    ├── StandPoint                      empty, pos (0,0,-10), rot (0,0,0)
    ├── StandMarker                     same as Area1
    ├── SpawnerLeft                     [NpcSpawner]  pos (-14,0,6)    (spawns on left, walks to right)
    │   └── EndPoint                    empty, pos (28,0,0) local to SpawnerLeft  (= world-local (14,0,6))
    ├── SpawnerRight                    [NpcSpawner]  pos (14,0,9)     (spawns on right, walks to left)
    │   └── EndPoint                    empty, pos (-28,0,0) local to SpawnerRight (= (-14,0,9))
    └── Targets                         empty container (scale 1,1,1)
```

Notes:
- `SpawnVolume` boxes only define spawn bounds; keep `isTrigger = true`. The weapon ignores triggers.
- Floors/walls keep their default BoxColliders so misses stop at geometry.
- Area roots start **active**. `PlaytestManager` deactivates behavior (not GameObjects) at runtime via `Activate()` / `Deactivate()`.

---

## 4. Prefabs (create in `Assets/Prefabs/`)

### 4.1 `Target_Sphere` (Area 1)
- Root: Sphere primitive, scale (1,1,1), `SphereCollider`, material `M_Target` (red).
- Components: `Shootable` (default settings, `disappearOnShot = true`).

### 4.2 `FloatingTarget_Sphere` (Area 2)
- Root: Sphere primitive, scale (1,1,1), `SphereCollider`, material `M_Floating` (cyan).
- Components: `Shootable`, `FloatingTarget`.

### 4.3 `NPC` (Area 3) — total height about 1.7 m, model faces +Z
```
NPC                      [NpcHealth, NpcWalker]   (no collider on root)
├── Legs                 Cube  scale (0.45,0.8,0.25)  pos (0,0.4,0)    M_NpcLegs   BoxCollider  [NpcHitZone: zone=Legs, damage=20,    health=NPC]
├── Body                 Cube  scale (0.55,0.6,0.28)  pos (0,1.1,0)    M_NpcBody   BoxCollider  [NpcHitZone: zone=Body, damage=33.34, health=NPC]
└── Head                 Sphere scale (0.3,0.3,0.3)   pos (0,1.55,0)   M_NpcHead   SphereCollider [NpcHitZone: zone=Head, damage=100,   health=NPC]
```
Each `NpcHitZone` gets `textPrefab = FloatingText` and a `textColor` (Head red, Body yellow, Legs orange).
Legs, Body, Head colliders must not overlap each other.

### 4.4 `FloatingText`
- GameObject `FloatingText` with **TextMeshPro - Text** (the 3D world-space one, `TextMeshPro` component, not the UI one).
- Font size about 6, alignment center, no wrapping, bold.
- Add `FloatingText` script; assign `label` to its own TMP component.

---

## 5. Scripts

All scripts: no namespace, folder per section 1. Uses the **new Input System** (`UnityEngine.InputSystem`). Use only APIs valid in Unity 6.

### 5.1 `Core/GameEvents.cs`
```csharp
using System;
using UnityEngine;

public static class GameEvents
{
    public static event Action ShotFired;
    public static event Action<Shootable> TargetHit;
    public static event Action<int, string> PlaytestChanged; // index (0-based), display name

    public static void RaiseShotFired() => ShotFired?.Invoke();
    public static void RaiseTargetHit(Shootable s) => TargetHit?.Invoke(s);
    public static void RaisePlaytestChanged(int index, string name) => PlaytestChanged?.Invoke(index, name);

    // Needed when Enter Play Mode Options has Domain Reload disabled.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        ShotFired = null;
        TargetHit = null;
        PlaytestChanged = null;
    }
}
```

### 5.2 `Core/PlaytestArea.cs`
```csharp
using UnityEngine;

public abstract class PlaytestArea : MonoBehaviour
{
    [SerializeField] string displayName = "Playtest";
    [SerializeField] Transform standPoint;
    [SerializeField] protected Transform targetsContainer; // MUST have scale (1,1,1)

    public string DisplayName => displayName;
    public Transform StandPoint => standPoint;
    public bool IsRunning { get; private set; }

    public void Activate()
    {
        ClearTargets();
        IsRunning = true;
        OnActivated();
    }

    public void Deactivate()
    {
        IsRunning = false;
        OnDeactivated();
        ClearTargets();
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
```

### 5.3 `Core/PlaytestManager.cs`
```csharp
using UnityEngine;
using UnityEngine.InputSystem;

public class PlaytestManager : MonoBehaviour
{
    [SerializeField] PlayerLook player;
    [SerializeField] PlaytestArea[] areas;   // order: Area1, Area2, Area3
    [SerializeField] int startIndex = 0;

    int current = -1;

    void Start() => Select(startIndex);

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;
        if (kb.digit1Key.wasPressedThisFrame) Select(0);
        else if (kb.digit2Key.wasPressedThisFrame) Select(1);
        else if (kb.digit3Key.wasPressedThisFrame) Select(2);
    }

    public void Select(int index)
    {
        if (areas == null || index < 0 || index >= areas.Length || index == current) return;

        for (int i = 0; i < areas.Length; i++)
            if (i != index) areas[i].Deactivate();

        current = index;
        areas[index].Activate();
        player.TeleportTo(areas[index].StandPoint);
        GameEvents.RaisePlaytestChanged(index, areas[index].DisplayName);
    }
}
```

### 5.4 `Player/PlayerLook.cs`
```csharp
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerLook : MonoBehaviour
{
    [SerializeField] Transform cameraPivot;
    [SerializeField] float sensitivity = 0.1f;   // degrees per pixel of mouse delta
    [SerializeField] float minPitch = -85f;
    [SerializeField] float maxPitch = 85f;

    float yaw, pitch;

    void Start() => SetCursorLocked(true);

    void Update()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;

        var kb = Keyboard.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame) SetCursorLocked(false);

        if (Cursor.lockState != CursorLockMode.Locked)
        {
            if (mouse.leftButton.wasPressedThisFrame) SetCursorLocked(true);
            return;
        }

        Vector2 d = mouse.delta.ReadValue() * sensitivity;
        yaw += d.x;
        pitch = Mathf.Clamp(pitch - d.y, minPitch, maxPitch);
        Apply();
    }

    public void TeleportTo(Transform stand)
    {
        transform.position = stand.position;
        yaw = stand.eulerAngles.y;
        pitch = 0f;
        Apply();
    }

    void Apply()
    {
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    static void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}
```

### 5.5 `Shooting/Shootable.cs`
```csharp
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
```

### 5.6 `Player/HitscanWeapon.cs`
```csharp
using UnityEngine;
using UnityEngine.InputSystem;

public class HitscanWeapon : MonoBehaviour
{
    [SerializeField] Camera cam;
    [SerializeField] float range = 200f;
    [SerializeField] LayerMask hitMask = ~0;
    [SerializeField] float fireCooldown = 0.12f;
    [SerializeField] bool automatic = false;
    [SerializeField] bool debugRay = true;

    float nextFireTime;

    void Update()
    {
        var mouse = Mouse.current;
        if (mouse == null || Cursor.lockState != CursorLockMode.Locked) return;

        bool wantsFire = automatic ? mouse.leftButton.isPressed : mouse.leftButton.wasPressedThisFrame;
        if (wantsFire && Time.time >= nextFireTime)
        {
            nextFireTime = Time.time + fireCooldown;
            Fire();
        }
    }

    void Fire()
    {
        GameEvents.RaiseShotFired();

        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
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
```

### 5.7 `Areas/AimTrainerArea.cs` (Playtest 1)
```csharp
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
```

### 5.8 `Areas/FloatingTargetArea.cs` (Playtest 2)
```csharp
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
```

### 5.9 `Areas/FloatingTarget.cs`
```csharp
using UnityEngine;

public class FloatingTarget : MonoBehaviour
{
    float speed = 3f;
    float maxY = 25f;

    public void Init(float riseSpeed, float maxWorldY)
    {
        speed = riseSpeed;
        maxY = maxWorldY;
    }

    void Update()
    {
        transform.position += Vector3.up * (speed * Time.deltaTime);
        if (transform.position.y >= maxY) Destroy(gameObject);
    }
}
```

### 5.10 `Areas/NpcArea.cs` (Playtest 3 controller)
```csharp
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
```

### 5.11 `Areas/NpcSpawner.cs`
```csharp
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
```

### 5.12 `Npc/NpcHealth.cs`
```csharp
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
```

### 5.13 `Npc/NpcHitZone.cs`
```csharp
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
```

### 5.14 `Npc/NpcWalker.cs`
```csharp
using UnityEngine;

public class NpcWalker : MonoBehaviour
{
    Vector3 destination;
    float speed = 2f;

    public void Init(Vector3 dest, float moveSpeed)
    {
        destination = new Vector3(dest.x, transform.position.y, dest.z);
        speed = moveSpeed;
        Face();
    }

    void Update()
    {
        Vector3 to = destination - transform.position;
        to.y = 0f;
        float step = speed * Time.deltaTime;

        if (to.magnitude <= step)
        {
            Destroy(gameObject);   // reached side B
            return;
        }
        transform.position += to.normalized * step;
    }

    void Face()
    {
        Vector3 to = destination - transform.position;
        to.y = 0f;
        if (to.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(to.normalized);
    }
}
```

### 5.15 `UI/FloatingText.cs`
```csharp
using TMPro;
using UnityEngine;

public class FloatingText : MonoBehaviour
{
    [SerializeField] TMP_Text label;
    [SerializeField] float lifetime = 0.9f;
    [SerializeField] float riseSpeed = 1.2f;
    [SerializeField] float baseScale = 0.1f;
    [SerializeField] float distanceScaleFactor = 0.05f;   // keeps text readable at long range

    Camera cam;
    float age;

    void Awake()
    {
        cam = Camera.main;
        if (label == null) label = GetComponent<TMP_Text>();
    }

    public void Show(string text, Color color)
    {
        if (label == null) label = GetComponent<TMP_Text>();
        label.text = text;
        label.color = color;
    }

    void LateUpdate()
    {
        age += Time.deltaTime;
        if (age >= lifetime) { Destroy(gameObject); return; }

        transform.position += Vector3.up * (riseSpeed * Time.deltaTime);

        if (cam != null)
        {
            transform.rotation = cam.transform.rotation;   // billboard
            float dist = Vector3.Distance(cam.transform.position, transform.position);
            transform.localScale = Vector3.one * (baseScale + dist * distanceScaleFactor * baseScale * 10f);
        }

        Color c = label.color;
        c.a = 1f - (age / lifetime);
        label.color = c;
    }
}
```

### 5.16 `UI/HudController.cs`
```csharp
using TMPro;
using UnityEngine;

public class HudController : MonoBehaviour
{
    [SerializeField] TMP_Text modeLabel;
    [SerializeField] TMP_Text statsLabel;

    int shots, hits;

    void OnEnable()
    {
        GameEvents.ShotFired += OnShot;
        GameEvents.TargetHit += OnHit;
        GameEvents.PlaytestChanged += OnModeChanged;
    }

    void OnDisable()
    {
        GameEvents.ShotFired -= OnShot;
        GameEvents.TargetHit -= OnHit;
        GameEvents.PlaytestChanged -= OnModeChanged;
    }

    void OnShot() { shots++; Refresh(); }
    void OnHit(Shootable _) { hits++; Refresh(); }

    void OnModeChanged(int index, string name)
    {
        shots = 0; hits = 0;
        modeLabel.text = $"[{index + 1}] {name}   (keys 1/2/3 to switch, Esc to free cursor)";
        Refresh();
    }

    void Refresh()
    {
        float acc = shots > 0 ? (hits / (float)shots) * 100f : 0f;
        statsLabel.text = $"Shots: {shots}\nHits: {hits}\nAccuracy: {acc:0}%";
    }
}
```

---

## 6. Inspector wiring table (agent must assign every field)

| GameObject / Prefab | Component | Field | Value |
|---|---|---|---|
| GameManager | PlaytestManager | player | `Player` (PlayerLook) |
| | | areas | [0] Area1_AimTrainer, [1] Area2_Floating, [2] Area3_Npc |
| | | startIndex | 0 |
| Player | PlayerLook | cameraPivot | `Player/CameraPivot` |
| Player | HitscanWeapon | cam | `Main Camera` |
| Canvas | HudController | modeLabel | `ModeLabel` |
| | | statsLabel | `StatsLabel` |
| Area1_AimTrainer | AimTrainerArea | displayName | `Aim Trainer` |
| | | standPoint | `Area1_AimTrainer/StandPoint` |
| | | targetsContainer | `Area1_AimTrainer/Targets` |
| | | targetPrefab | `Prefabs/Target_Sphere` |
| | | spawnVolume | `Area1_AimTrainer/SpawnVolume` (BoxCollider) |
| Area2_Floating | FloatingTargetArea | displayName | `Floating Targets` |
| | | standPoint | `Area2_Floating/StandPoint` |
| | | targetsContainer | `Area2_Floating/Targets` |
| | | targetPrefab | `Prefabs/FloatingTarget_Sphere` |
| | | spawnVolume | `Area2_Floating/SpawnVolume` (BoxCollider) |
| Area3_Npc | NpcArea | displayName | `NPC Range` |
| | | standPoint | `Area3_Npc/StandPoint` |
| | | targetsContainer | `Area3_Npc/Targets` |
| | | spawners | [0] SpawnerLeft, [1] SpawnerRight |
| SpawnerLeft | NpcSpawner | npcPrefab | `Prefabs/NPC` |
| | | endPoint | `SpawnerLeft/EndPoint` |
| SpawnerRight | NpcSpawner | npcPrefab | `Prefabs/NPC` |
| | | endPoint | `SpawnerRight/EndPoint` |
| NPC prefab root | NpcHealth | maxHealth | 100 |
| NPC/Head | NpcHitZone | health / zone / damage | NPC root / Head / 100, textPrefab = FloatingText, color red |
| NPC/Body | NpcHitZone | health / zone / damage | NPC root / Body / 33.34, textPrefab = FloatingText, color yellow |
| NPC/Legs | NpcHitZone | health / zone / damage | NPC root / Legs / 20, textPrefab = FloatingText, color orange |
| FloatingText prefab | FloatingText | label | its own TextMeshPro component |

Tag the camera `MainCamera` (FloatingText relies on `Camera.main`).

---

## 7. Materials (URP/Lit, simple colors)

`M_Target` red, `M_Floating` cyan, `M_Floor` dark grey, `M_Wall` mid grey, `M_NpcHead` skin tone, `M_NpcBody` blue, `M_NpcLegs` dark blue.

---

## 8. Build order for the agent

1. Create project folders, import packages, set Active Input Handling.
2. Create materials, then the four prefabs (section 4), then all scripts (section 5). Wait for compilation to finish.
3. Build the scene hierarchy exactly as in section 3.
4. Add components and assign every field from section 6.
5. Save scene, add it to Build Settings.
6. Enter Play Mode and run the acceptance checklist.

---

## 9. Acceptance checklist

- [ ] No compile errors or console warnings on Play.
- [ ] Cursor locks on start; Esc frees it; click relocks.
- [ ] Crosshair at screen center; ray hits exactly under it (debug line visible in Scene view).
- [ ] Keys 1/2/3 teleport the player to each StandPoint and clear the other areas' targets.
- [ ] **Area 1:** up to 8 spheres of varied size and depth; no overlapping spawns; shot sphere disappears and a new one appears shortly after.
- [ ] **Area 2:** spheres spawn at ground level, rise, and vanish at maxHeight; shooting one removes it.
- [ ] **Area 3:** NPCs spawn on both sides, walk to the opposite side, and are removed on arrival.
- [ ] Head hit: red `HEADSHOT` text, NPC disappears instantly.
- [ ] Body hit: `33.34HP` text; NPC disappears on the 3rd body hit.
- [ ] Legs hit: `20HP` text; NPC disappears on the 5th legs hit.
- [ ] Mixed hits share one HP pool (e.g. 1 body + 3 legs = 93.34, then 1 more leg kills).
- [ ] Shots at walls/floor do nothing; HUD shows shots, hits, accuracy and resets on area switch.

---

## 10. Extension ideas (do not build now)

Object pooling, ammo/reload, hit markers and sounds, score per zone, difficulty sliders for spawn rate and size, recoil, NPC walk animation, results screen per playtest.
