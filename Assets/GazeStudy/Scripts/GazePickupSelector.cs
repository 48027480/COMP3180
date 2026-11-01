using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

namespace COMP3180.GazeStudy
{
    /// <summary>
    /// Playground selection: aim at an object, press confirm to pick it up, press again to drop it.
    ///   Gaze ON  -> aim with the eye tracker (head if no eye device; camera on desktop)
    ///   Gaze OFF -> aim with the right controller laser (no aiming on desktop)
    /// Toggle: Ctrl on the keyboard, or the right controller's A / menu button.
    /// The nearest collider blocks the ray, so stands and objects in front hide what is behind them.
    /// </summary>
    public class GazePickupSelector : MonoBehaviour
    {
        const float RayLengthWithoutHit = 6f;

        [Header("Pose sources")]
        [SerializeField] Transform eyeGazePose;
        [SerializeField] Transform controllerPose;
        [SerializeField] Transform headPose;

        [Header("Hold points")]
        [SerializeField] Transform controllerHoldPoint;
        [SerializeField] Transform desktopHoldPoint;

        [Header("Input and visuals")]
        [SerializeField] ConfirmInput confirm;
        [SerializeField] LineRenderer controllerLine;
        [SerializeField] Transform reticle;
        [SerializeField] TMP_Text statusText;
        [SerializeField] TMP_Text headsetStatusText;
        [SerializeField] float maxDistance = 15f;
        [SerializeField] bool gazeEnabled = true;

        public bool GazeEnabled => gazeEnabled;
        public PickupObject Hovered { get; private set; }
        public PickupObject Held { get; private set; }

        InputAction toggleAction;
        float nextDeviceCheck;
        bool eyeDeviceAvailable;
        string aimSource = "";

        void OnEnable()
        {
            toggleAction = new InputAction("ToggleGaze", InputActionType.Button);
            toggleAction.AddBinding("<XRController>{RightHand}/{PrimaryButton}");
            toggleAction.AddBinding("<XRController>{RightHand}/{MenuButton}");
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
            if (ctrl || toggleAction.WasPressedThisFrame()) ToggleGaze();

            bool desktop = !XRSettings.isDeviceActive;
            bool hasRay = TryGetRay(desktop, out Ray ray);
            PickupObject found = null;
            float length = RayLengthWithoutHit;

            if (hasRay && Physics.Raycast(ray, out RaycastHit hit, maxDistance,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                length = hit.distance;
                found = hit.collider.GetComponentInParent<PickupObject>();
            }

            SetHovered(Held == null ? found : null);
            UpdateVisuals(desktop, hasRay, ray, length);

            if (confirm.PressedThisFrame) ConfirmPressed(desktop);
            UpdateStatus();
        }

        public void ToggleGaze() { gazeEnabled = !gazeEnabled; }

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

        void ConfirmPressed(bool desktop)
        {
            if (Held != null)
            {
                Held.Release();
                Held = null;
            }
            else if (Hovered != null)
            {
                Held = Hovered;
                SetHovered(null);
                Held.PickUp(desktop ? desktopHoldPoint : controllerHoldPoint);
            }
        }

        void SetHovered(PickupObject target)
        {
            if (target == Hovered) return;
            if (Hovered != null) Hovered.SetHovered(false);
            Hovered = target;
            if (Hovered != null) Hovered.SetHovered(true);
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
            if (hasRay) reticle.position = ray.GetPoint(length);
        }

        void UpdateStatus()
        {
            string target = Held != null ? "Holding " + Held.name
                          : Hovered != null ? "Looking at " + Hovered.name : "";
            string line = $"Eye gaze: {(gazeEnabled ? "ON" : "OFF")}   Aim: {aimSource}\n{target}";
            statusText.text = line + "\n\nWASD / left stick: move     Hold right mouse / right stick: look" +
                              "\nCtrl / right A or menu: eye gaze on/off     Space / trigger: pick up, drop";
            headsetStatusText.text = line;
        }
    }
}
