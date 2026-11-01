using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

namespace COMP3180.GazeStudy
{
    /// <summary>
    /// Walking player for the playground, attached to the XR Origin.
    ///   Move: WASD or the left stick, relative to where the head faces.
    ///   Turn: right stick, or hold the right mouse button when no headset is active.
    /// The CharacterController and capsule body follow the head so room-scale steps stay inside the body.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerBody : MonoBehaviour
    {
        const float RespawnBelowY = -5f;
        const float HeadroomAboveBody = 0.3f;

        [SerializeField] Transform head;
        [SerializeField] Transform bodyVisual;
        [SerializeField] GameObject leftControllerVisual;
        [SerializeField] GameObject rightControllerVisual;
        [SerializeField] float moveSpeed = 2.5f;
        [SerializeField] float stickTurnSpeed = 75f;
        [SerializeField] float mouseSensitivity = 0.12f;
        [SerializeField] float gravity = 9.81f;

        CharacterController controller;
        InputAction moveAction, turnAction;
        Vector3 spawnPosition;
        float verticalSpeed, mouseYaw, mousePitch;

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            spawnPosition = transform.position;
            mouseYaw = transform.eulerAngles.y;
        }

        void OnEnable()
        {
            moveAction = new InputAction("Move", InputActionType.Value, "<XRController>{LeftHand}/{Primary2DAxis}", expectedControlType: "Vector2");
            turnAction = new InputAction("Turn", InputActionType.Value, "<XRController>{RightHand}/{Primary2DAxis}", expectedControlType: "Vector2");
            moveAction.Enable();
            turnAction.Enable();
        }

        void OnDisable()
        {
            moveAction.Dispose();
            turnAction.Dispose();
        }

        void Update()
        {
            bool desktop = !XRSettings.isDeviceActive;
            if (desktop) MouseLook();

            // Untracked controllers would float in the middle of the desktop view.
            leftControllerVisual.SetActive(!desktop);
            rightControllerVisual.SetActive(!desktop);

            float turn = turnAction.ReadValue<Vector2>().x;
            if (Mathf.Abs(turn) > 0.2f) transform.RotateAround(head.position, Vector3.up, turn * stickTurnSpeed * Time.deltaTime);

            FitBodyToHead();
            Walk();

            if (transform.position.y < RespawnBelowY)
            {
                controller.enabled = false;
                transform.position = spawnPosition;
                controller.enabled = true;
                verticalSpeed = 0f;
            }
        }

        void MouseLook()
        {
            var mouse = Mouse.current;
            if (mouse == null || !mouse.rightButton.isPressed) return;

            Vector2 d = mouse.delta.ReadValue();
            mouseYaw += d.x * mouseSensitivity;
            mousePitch = Mathf.Clamp(mousePitch - d.y * mouseSensitivity, -80f, 80f);
            transform.rotation = Quaternion.Euler(0f, mouseYaw, 0f);
            head.parent.localRotation = Quaternion.Euler(mousePitch, 0f, 0f); // the head's own rotation belongs to its Tracked Pose Driver
        }

        void FitBodyToHead()
        {
            Vector3 local = transform.InverseTransformPoint(head.position);
            float height = Mathf.Max(local.y, controller.radius * 2f);
            controller.height = height;
            controller.center = new Vector3(local.x, height * 0.5f + controller.skinWidth, local.z);

            float bodyHeight = Mathf.Max(height - HeadroomAboveBody, 0.5f);
            bodyVisual.localPosition = new Vector3(local.x, bodyHeight * 0.5f, local.z);
            bodyVisual.localScale = new Vector3(0.4f, bodyHeight * 0.5f, 0.4f);
        }

        void Walk()
        {
            Vector2 input = moveAction.ReadValue<Vector2>() + ReadKeyboard();
            input = Vector2.ClampMagnitude(input, 1f);

            Vector3 forward = Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, forward);

            verticalSpeed = controller.isGrounded ? -1f : verticalSpeed - gravity * Time.deltaTime;
            Vector3 velocity = (forward * input.y + right * input.x) * moveSpeed + Vector3.up * verticalSpeed;
            controller.Move(velocity * Time.deltaTime);
        }

        static Vector2 ReadKeyboard()
        {
            var kb = Keyboard.current;
            if (kb == null) return Vector2.zero;
            return new Vector2((kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f),
                               (kb.wKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f));
        }
    }
}
