using UnityEngine;
using UnityEngine.InputSystem;

namespace COMP3180.GazeStudy
{
    /// <summary>
    /// Playground buttons. Grab is held: the left mouse button or any controller button the playground
    /// doesn't use for something else (grips, triggers, right B, left X and Y).
    /// Fire is the left mouse button or the right trigger.
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public class GrabInput : MonoBehaviour
    {
        InputAction grab;
        InputAction fire;

        public bool GrabHeld { get; private set; }
        public bool GrabPressedThisFrame { get; private set; }
        public bool FireHeld { get; private set; }
        public bool FirePressedThisFrame { get; private set; }

        void OnEnable()
        {
            grab = new InputAction("Grab", InputActionType.Button);
            grab.AddBinding("<Mouse>/leftButton");
            foreach (string hand in new[] { "{LeftHand}", "{RightHand}" })
            {
                grab.AddBinding($"<XRController>{hand}/{{GripButton}}");
                grab.AddBinding($"<XRController>{hand}/gripPressed");
                grab.AddBinding($"<XRController>{hand}/{{TriggerButton}}");
                grab.AddBinding($"<XRController>{hand}/triggerPressed");
            }
            grab.AddBinding("<XRController>{RightHand}/{SecondaryButton}");
            grab.AddBinding("<XRController>{LeftHand}/{PrimaryButton}");
            grab.AddBinding("<XRController>{LeftHand}/{SecondaryButton}");

            fire = new InputAction("Fire", InputActionType.Button);
            fire.AddBinding("<Mouse>/leftButton");
            fire.AddBinding("<XRController>{RightHand}/{TriggerButton}");
            fire.AddBinding("<XRController>{RightHand}/triggerPressed");

            grab.Enable();
            fire.Enable();
        }

        void OnDisable()
        {
            grab.Dispose();
            fire.Dispose();
        }

        void Update()
        {
            GrabHeld = grab.IsPressed();
            GrabPressedThisFrame = grab.WasPressedThisFrame();
            FireHeld = fire.IsPressed();
            FirePressedThisFrame = fire.WasPressedThisFrame();
        }
    }
}
