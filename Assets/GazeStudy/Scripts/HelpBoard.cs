using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

namespace COMP3180.GazeStudy
{
    /// <summary>
    /// The help board, opened with H or a left stick click. The board stays where it opened, so it can be
    /// aimed at with the camera as well as the eyes. Keeps the regular UI out of the way during a test.
    /// </summary>
    public class HelpBoard : MonoBehaviour
    {
        const float BoardDistance = 1.6f;
        const float WallClearance = 0.15f;

        [SerializeField] GazePickupSelector selector;
        [SerializeField] TutorialSystem tutorials;
        [SerializeField] PlaytestManager playtests;
        [SerializeField] Transform head;
        [SerializeField] GameObject board;
        [SerializeField] GameObject headsetPrompt;
        [SerializeField] WorldButton movementButton;
        [SerializeField] WorldButton interactionButton;
        [SerializeField] WorldButton eyeGazeButton;

        InputAction helpAction;

        void OnEnable()
        {
            helpAction = new InputAction("HelpBoard", InputActionType.Button, "<XRController>{LeftHand}/{Primary2DAxisClick}");
            helpAction.Enable();
            movementButton.Pressed += StartMovement;
            interactionButton.Pressed += StartInteraction;
            eyeGazeButton.Pressed += StartEyeGaze;
        }

        void OnDisable()
        {
            helpAction.Dispose();
            movementButton.Pressed -= StartMovement;
            interactionButton.Pressed -= StartInteraction;
            eyeGazeButton.Pressed -= StartEyeGaze;
        }

        void Update()
        {
            bool testing = playtests.IsRunning;
            bool desktop = !XRSettings.isDeviceActive;

            selector.SetStatusVisible(!testing);
            headsetPrompt.SetActive(!testing && !desktop);

            if (testing)
            {
                board.SetActive(false);
                if (tutorials.IsActive) tutorials.Stop();
                return;
            }

            var kb = Keyboard.current;
            if ((kb != null && kb.hKey.wasPressedThisFrame) || (!desktop && helpAction.WasPressedThisFrame()))
            {
                if (tutorials.IsActive) tutorials.Stop();
                else if (board.activeSelf) board.SetActive(false);
                else OpenBoard();
            }

            if (kb != null && kb.digit1Key.wasPressedThisFrame) StartMovement();
            else if (kb != null && kb.digit2Key.wasPressedThisFrame) StartInteraction();
        }

        void OpenBoard()
        {
            Vector3 forward = Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized;
            float distance = BoardDistance;
            if (Physics.Raycast(head.position, forward, out RaycastHit hit, BoardDistance,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                distance = hit.distance - WallClearance;

            board.transform.SetPositionAndRotation(head.position + forward * distance, Quaternion.LookRotation(forward));
            board.SetActive(true);
        }

        void StartMovement() => StartTutorial(Tutorial.Movement);
        void StartInteraction() => StartTutorial(Tutorial.Interaction);
        void StartEyeGaze() => StartTutorial(Tutorial.EyeGaze);

        void StartTutorial(Tutorial tutorial)
        {
            board.SetActive(false);
            tutorials.Begin(tutorial);
        }
    }
}
