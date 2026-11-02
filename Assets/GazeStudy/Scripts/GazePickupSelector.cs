using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

namespace COMP3180.GazeStudy
{
    /// <summary>
    /// Playground selection: aim at an object and hold grab to drag it along the ray. Releasing drops it.
    ///   Gaze ON  -> aim with the eye tracker (head if no eye device; camera on desktop)
    ///   Gaze OFF -> aim with the right controller laser (no aiming on desktop)
    /// Toggle: Ctrl on the keyboard, or the right controller's A button.
    /// The nearest collider blocks the ray, so stands and objects in front hide what is behind them.
    /// With the gun equipped inside the prototype zone nothing can be grabbed, as the button fires instead.
    /// Eye-gaze-assist (three winks, see WinkInput) aims with the eyes and adds winks to the buttons: a wink
    /// picks up what is looked at, the next wink drops it, and with the gun a wink fires.
    /// </summary>
    public class GazePickupSelector : MonoBehaviour
    {
        const float RayLengthWithoutHit = 6f;
        const float DragClearance = 0.15f;
        const float DragSmoothing = 15f;
        const float ReticleMinSize = 0.03f;
        const float ReticleSizePerMetre = 0.012f;

        [Header("Pose sources")]
        [SerializeField] Transform eyeGazePose;
        [SerializeField] Transform controllerPose;
        [SerializeField] Transform headPose;

        [Header("Hold points")]
        [SerializeField] Transform controllerHoldPoint;
        [SerializeField] Transform desktopHoldPoint;

        [Header("Input and visuals")]
        [SerializeField] GrabInput grab;
        [SerializeField] Collider prototypeZone;
        [SerializeField] float shootingDistance = 200f;
        [SerializeField] LineRenderer controllerLine;
        [SerializeField] Transform reticle;
        [SerializeField] TMP_Text statusText;
        [SerializeField] TMP_Text headsetStatusText;
        [SerializeField] float maxDistance = 15f;
        [SerializeField] bool gazeEnabled = true;

        public bool GazeEnabled => gazeEnabled;
        public PickupObject Hovered { get; private set; }
        public PickupObject Held { get; private set; }
        public WorldButton HoveredButton { get; private set; }
        public bool GunEquipped => gun != null;
        public bool InPrototype => prototypeZone != null && prototypeZone.bounds.Contains(headPose.position);
        public bool HasRay { get; private set; }
        public Ray CurrentRay { get; private set; }
        public WinkInput Winks { get; private set; }
        public bool AssistWink => Winks != null && Winks.Winked;

        InputAction toggleAction;
        bool heldByWink, gazeBeforeAssist;
        float nextDeviceCheck;
        bool eyeDeviceAvailable;
        string aimSource = "";
        PickupObject gun;
        float dragDistance;
        Vector3 dragOffset;

        void Awake()
        {
            Winks = gameObject.AddComponent<WinkInput>();
            Winks.AssistChanged += OnAssistChanged;
        }

        void OnAssistChanged()
        {
            if (Winks.AssistOn) gazeBeforeAssist = gazeEnabled;
            gazeEnabled = Winks.AssistOn || gazeBeforeAssist;
            if (!Winks.AssistOn && Held != null && heldByWink) ReleaseHeld();
        }

        void OnEnable()
        {
            toggleAction =new InputAction("ToggleGaze", InputActionType.Button, "<XRController>{RightHand}/{PrimaryButton}");
            toggleAction.Enable();
        }

        void OnDisable()
        {
            toggleAction.Dispose();
            SetHovered(null);
        }

        void Update()
        {
            if (Time.unscaledTime >= nextDeviceCheck)
            {
                nextDeviceCheck = Time.unscaledTime + 0.5f;
                eyeDeviceAvailable = SelectionRaySource.DetectEyeGazeDevice();
            }

            var kb = Keyboard.current;
            bool ctrl = kb != null && (kb.leftCtrlKey.wasPressedThisFrame || kb.rightCtrlKey.wasPressedThisFrame);
            bool desktop = !XRSettings.isDeviceActive;
            if (ctrl || (!desktop && toggleAction.WasPressedThisFrame())) ToggleGaze(); // the simulator presses controller buttons on desktop

            bool hasRay = TryGetRay(desktop, out Ray ray);
            HasRay = hasRay;
            CurrentRay = ray;
            bool shooting = GunEquipped && InPrototype;
            PickupObject found = null;
            WorldButton foundButton = null;
            float length = RayLengthWithoutHit;
            RaycastHit hit = default;

            if (hasRay && Physics.Raycast(ray, out hit, shooting ? shootingDistance : maxDistance,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                length = hit.distance;
                if (!shooting) found = hit.collider.GetComponentInParent<PickupObject>();
                foundButton = hit.collider.GetComponentInParent<WorldButton>();
            }

            SetHovered(Held == null ? found : null);
            SetHoveredButton(Held == null ? foundButton : null);

            bool pressed = grab.GrabPressedThisFrame || AssistWink;
            if (Held != null)
            {
                bool keepHolding = heldByWink ? !AssistWink : grab.GrabHeld;
                if (hasRay && keepHolding) Drag(ray);
                else ReleaseHeld();
            }
            else if (HoveredButton != null && pressed)
            {
                HoveredButton.Press();
            }
            else if (Hovered != null && pressed)
            {
                Grab(desktop, hit);
                heldByWink = Held != null && !grab.GrabPressedThisFrame;
            }

            UpdateVisuals(desktop, hasRay, ray, length);
            UpdateStatus();
        }

        public void ToggleGaze()
        {
            if (!Winks.AssistOn) gazeEnabled = !gazeEnabled; // the assist always aims with the eyes
        }

        public void SetStatusVisible(bool visible)
        {
            statusText.gameObject.SetActive(visible);
            headsetStatusText.gameObject.SetActive(visible);
        }

        /// <summary>Puts every pickup back where it started, including the gun.</summary>
        public void ResetAll()
        {
            Held = null;
            heldByWink = false;
            gun = null;
            SetHovered(null);
            foreach (var pickup in FindObjectsByType<PickupObject>(FindObjectsSortMode.None)) pickup.ReturnHome();
        }

        bool TryGetRay(bool desktop, out Ray ray)
        {
            ray = default;
            Transform source;
            if (desktop)
            {
                aimSource = gazeEnabled ? "Desktop camera" : "None";
                source = gazeEnabled ? headPose : null;
            }
            else if (!gazeEnabled)
            {
                aimSource = "Controller";
                source = controllerPose;
            }
            else if (eyeDeviceAvailable && eyeGazePose != null)
            {
                aimSource = "Eye tracker";
                source = eyeGazePose;
            }
            else
            {
                aimSource = "Head (no eye device)";
                source = headPose;
            }

            if (source == null) return false;
            ray = new Ray(source.position, source.forward);
            return true;
        }

        void Grab(bool desktop, RaycastHit hit)
        {
            PickupObject target = Hovered;
            SetHovered(null);

            if (target.StaysInHand)
            {
                gun = target;
                gun.PickUp(desktop ? desktopHoldPoint : controllerHoldPoint);
                return;
            }

            Held = target;
            Held.BeginDrag();
            dragDistance = hit.distance;
            dragOffset = Held.transform.position - hit.point;
        }

        void Drag(Ray ray)
        {
            // Stop short of whatever is in the way so the object can't be pushed through walls or the floor.
            float distance = dragDistance;
            if (Physics.SphereCast(ray, DragClearance, out RaycastHit blocker, dragDistance,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                distance = blocker.distance;

            Vector3 target = ray.GetPoint(distance) + dragOffset;
            float blend = 1f - Mathf.Exp(-DragSmoothing * Time.deltaTime); // steadies eye tracker jitter
            Held.transform.position = Vector3.Lerp(Held.transform.position, target, blend);
        }

        void ReleaseHeld()
        {
            Held.Release();
            Held = null;
            heldByWink = false;
        }

        void SetHovered(PickupObject target)
        {
            if (target == Hovered) return;
            if (Hovered != null) Hovered.SetHovered(false);
            Hovered = target;
            if (Hovered != null) Hovered.SetHovered(true);
        }

        void SetHoveredButton(WorldButton target)
        {
            if (target == HoveredButton) return;
            if (HoveredButton != null) HoveredButton.SetHovered(false);
            HoveredButton = target;
            if (HoveredButton != null) HoveredButton.SetHovered(true);
        }

        void UpdateVisuals(bool desktop, bool hasRay, Ray ray, float length)
        {
            bool showLine = !desktop && !gazeEnabled && hasRay;
            controllerLine.enabled = showLine;
            if (showLine)
            {
                controllerLine.SetPosition(0, ray.origin);
                controllerLine.SetPosition(1, ray.GetPoint(length));
            }

            reticle.gameObject.SetActive(hasRay);
            if (!hasRay) return;

            // Held clear of the surface and kept a readable size, so objects can't swallow it.
            Vector3 surface = ray.GetPoint(length);
            float size = Mathf.Max(ReticleMinSize, Vector3.Distance(headPose.position, surface) * ReticleSizePerMetre);
            reticle.localScale = Vector3.one * size;
            reticle.position = ray.GetPoint(Mathf.Max(0f, length - size));
        }

        void UpdateStatus()
        {
            string target = Held != null ? "Dragging " + Held.name
                          : Hovered != null ? "Looking at " + Hovered.name
                          : GunEquipped ? "Gun equipped" : "";
            string assist = Winks.AssistOn ? "   Wink assist: ON" : "";
            string line = $"Eye gaze: {(gazeEnabled ? "ON" : "OFF")}   Aim: {aimSource}{assist}\n{target}";
            statusText.text = line;
            headsetStatusText.text = $"{line}\n{Winks.EyeReadout}";
        }
    }
}
