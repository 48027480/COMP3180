using UnityEngine;
using UnityEngine.InputSystem;

namespace COMP3180.GazeStudy
{
    /// <summary>
    /// The deliberate confirmation signal (controller trigger). Space also works for desktop testing.
    /// Leave triggerAction empty to use built-in right-hand trigger bindings, or assign the XRI
    /// "XRI Right Interaction / Activate" action if the built-in binding does not fire on the headset.
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public class ConfirmInput : MonoBehaviour
    {
        [SerializeField] InputActionReference triggerAction;

        InputAction trigger;
        InputAction ownedTrigger;
        InputAction space;

        public bool PressedThisFrame { get; private set; }

        void OnEnable()
        {
            if (triggerAction != null && triggerAction.action != null)
            {
                trigger = triggerAction.action;
            }
            else
            {
                ownedTrigger = new InputAction("ConfirmTrigger", InputActionType.Button);
                ownedTrigger.AddBinding("<XRController>{RightHand}/{TriggerButton}");
                ownedTrigger.AddBinding("<XRController>{RightHand}/triggerPressed");
                trigger = ownedTrigger;
            }

            space = new InputAction("ConfirmSpace", InputActionType.Button, "<Keyboard>/space");
            trigger.Enable();
            space.Enable();
        }

        void OnDisable()
        {
            if (trigger != null) trigger.Disable();
            if (space != null) { space.Disable(); space.Dispose(); space = null; }
            if (ownedTrigger != null) { ownedTrigger.Dispose(); ownedTrigger = null; }
        }

        void Update()
        {
            PressedThisFrame = (trigger != null && trigger.WasPressedThisFrame())
                            || (space != null && space.WasPressedThisFrame());
        }
    }
}
