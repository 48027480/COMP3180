using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

namespace COMP3180.GazeStudy
{
    /// <summary>
    /// Playground buttons. Grab is held: the left mouse button or any controller button the playground
    /// doesn't use for something else (grips, right trigger, right B, left X).
    /// Fire is the left mouse button or the right trigger.
    /// Controllers only count with a headset active, as the XR Interaction Simulator turns desktop
    /// keys into simulated controller presses.
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public class GrabInput : MonoBehaviour
    {
        InputAction mouseButton;
        InputAction grab;
        InputAction fire;

        public bool GrabHeld { get; private set; }
        public bool GrabPressedThisFrame { get; private set; }
        public bool FireHeld { get; private set; }
        public bool FirePressedThisFrame { get; private set; }

        void OnEnable()
        {
            mouseButton = new InputAction("Mouse", InputActionType.Button, "<Mouse>/leftButton");

            grab = new InputAction("Grab", InputActionType.Button);
            foreach (string hand in new[] { "{LeftHand}", "{RightHand}" })
            {
                grab.AddBinding($"<XRController>{hand}/{{GripButton}}");
                grab.AddBinding($"<XRController>{hand}/gripPressed");
            }
            grab.AddBinding("<XRController>{RightHand}/{TriggerButton}");
            grab.AddBinding("<XRController>{RightHand}/triggerPressed");
            grab.AddBinding("<XRController>{RightHand}/{SecondaryButton}");
            grab.AddBinding("<XRController>{LeftHand}/{PrimaryButton}");

            fire = new InputAction("Fire", InputActionType.Button);
            fire.AddBinding("<XRController>{RightHand}/{TriggerButton}");
            fire.AddBinding("<XRController>{RightHand}/triggerPressed");

            mouseButton.Enable();
            grab.Enable();
            fire.Enable();
        }

        void OnDisable()
        {
            mouseButton.Dispose();
            grab.Dispose();
            fire.Dispose();
        }

        void Update()
        {
            bool headset = XRSettings.isDeviceActive;
            bool mouseHeld = mouseButton.IsPressed();
            bool mousePressed = mouseButton.WasPressedThisFrame();

            GrabHeld = mouseHeld || (headset && grab.IsPressed());
            GrabPressedThisFrame = mousePressed || (headset && grab.WasPressedThisFrame());
            FireHeld = mouseHeld || (headset && fire.IsPressed());
            FirePressedThisFrame = mousePressed || (headset && fire.WasPressedThisFrame());
        }
    }
}
