using UnityEngine;
using UnityEngine.InputSystem;

namespace COMP3180.GazeStudy
{
    /// <summary>
    /// Editor-only testing aid. When no headset is active, hold the RIGHT mouse button and move the
    /// mouse to turn the XR Origin (simulates head/gaze movement). Space acts as the trigger.
    /// </summary>
    public class DesktopLook : MonoBehaviour
    {
        [SerializeField] SelectionRaySource ray;
        [Tooltip("Assign the XR Origin root.")]
        [SerializeField] Transform rigRoot;
        [SerializeField] float sensitivity = 0.12f;

        float yaw, pitch;

        void Start()
        {
            if (rigRoot == null) rigRoot = transform;
            Vector3 e = rigRoot.localEulerAngles;
            yaw = e.y;
            pitch = e.x > 180f ? e.x - 360f : e.x;
        }

        void Update()
        {
            if (ray == null || !ray.DesktopMode) return;
            var mouse = Mouse.current;
            if (mouse == null || !mouse.rightButton.isPressed) return;

            Vector2 d = mouse.delta.ReadValue();
            yaw += d.x * sensitivity;
            pitch = Mathf.Clamp(pitch - d.y * sensitivity, -80f, 80f);
            rigRoot.localRotation = Quaternion.Euler(pitch, yaw, 0f);
        }
    }
}
