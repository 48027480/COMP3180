using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

namespace COMP3180.GazeStudy
{
    /// <summary>
    /// Single place that decides where the selection ray comes from.
    ///   Eye        -> "Gaze Pose" transform driven by the OpenXR eye gaze action
    ///   Controller -> right controller transform
    ///   Head       -> main camera (fallback only; always logged as HeadFallback)
    /// With no headset active (editor testing) every mode uses the camera, driven by DesktopLook.
    /// Hover is computed here, so both techniques share exactly the same hit-testing and feedback.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class SelectionRaySource : MonoBehaviour
    {
        [Header("Pose sources")]
        [SerializeField] Transform eyeGazePose;
        [SerializeField] Transform controllerPose;
        [SerializeField] Transform headPose;

        [Header("Behaviour")]
        [SerializeField] bool forceDesktopMode;
        [Tooltip("Decision rule for the headset: if eye gaze is unavailable, allow head gaze (logged as HeadFallback).")]
        [SerializeField] bool allowHeadFallbackForEye;
        [SerializeField] float maxDistance = 30f;

        [Header("Visuals (optional)")]
        [SerializeField] LineRenderer controllerLine;
        [SerializeField] float controllerLineLength = 6f;
        [SerializeField] Transform debugReticle;

        public RayMode Mode { get; set; } = RayMode.Controller;
        public bool DesktopMode { get; private set; }
        public bool EyeGazeAvailable { get; private set; }
        public bool AllowHeadFallback => allowHeadFallbackForEye;
        public bool ShowDebugReticle { get; set; }
        public Ray CurrentRay { get; private set; }
        public GazeSource CurrentSource { get; private set; }
        public SelectableTarget Hovered { get; private set; }
        public Transform HeadPose => headPose;

        readonly RaycastHit[] hits = new RaycastHit[32];
        float nextDeviceCheck;

        void Awake()
        {
            if (headPose == null && Camera.main != null) headPose = Camera.main.transform;
            RefreshDevices();
        }

        void OnDisable()
        {
            SetHovered(null);
            if (controllerLine != null) controllerLine.enabled = false;
            if (debugReticle != null) debugReticle.gameObject.SetActive(false);
        }

        void RefreshDevices()
        {
            DesktopMode = forceDesktopMode || !XRSettings.isDeviceActive;
            EyeGazeAvailable = DetectEyeGazeDevice();
        }

        static bool DetectEyeGazeDevice()
        {
            foreach (var d in InputSystem.devices)
            {
                string layout = d.layout ?? "";
                string product = d.description.product ?? "";
                if (layout.IndexOf("EyeGaze", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    product.IndexOf("Eye Gaze", StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }

        void Update()
        {
            if (Time.unscaledTime >= nextDeviceCheck)
            {
                nextDeviceCheck = Time.unscaledTime + 0.5f;
                RefreshDevices();
            }
            Sample();
        }

        /// <summary>Recompute ray + hover right now (TrialManager calls this at the moment of a press).</summary>
        public void Sample()
        {
            if (headPose == null) return;

            Ray ray = ComputeRay(out GazeSource src);
            CurrentRay = ray;
            CurrentSource = src;

            SelectableTarget found = null;
            float best = float.MaxValue;
            int n = Physics.RaycastNonAlloc(ray, hits, maxDistance,
                        Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var t = hits[i].collider.GetComponentInParent<SelectableTarget>();
                if (t != null && hits[i].distance < best) { best = hits[i].distance; found = t; }
            }

            if (Hovered != null && !Hovered.isActiveAndEnabled) Hovered = null;
            SetHovered(found);
            UpdateVisuals(ray, found != null ? best : controllerLineLength);
        }

        Ray ComputeRay(out GazeSource src)
        {
            if (DesktopMode)
            {
                src = GazeSource.DesktopMouse;
                return FromTransform(headPose);
            }

            switch (Mode)
            {
                case RayMode.Eye:
                    if (EyeGazeAvailable && eyeGazePose != null)
                    {
                        src = GazeSource.EyeTracker;
                        return FromTransform(eyeGazePose);
                    }
                    src = GazeSource.HeadFallback;
                    return FromTransform(headPose);

                case RayMode.Controller:
                    src = GazeSource.ControllerRay;
                    return FromTransform(controllerPose != null ? controllerPose : headPose);

                default:
                    src = GazeSource.HeadFallback;
                    return FromTransform(headPose);
            }
        }

        static Ray FromTransform(Transform t) { return new Ray(t.position, t.forward); }

        void SetHovered(SelectableTarget t)
        {
            if (t == Hovered) return;
            if (Hovered != null) Hovered.SetHovered(false);
            Hovered = t;
            if (Hovered != null) Hovered.SetHovered(true);
        }

        void UpdateVisuals(Ray ray, float length)
        {
            if (controllerLine != null)
            {
                bool show = !DesktopMode && Mode == RayMode.Controller;
                controllerLine.enabled = show;
                if (show)
                {
                    controllerLine.useWorldSpace = true;
                    controllerLine.positionCount = 2;
                    controllerLine.SetPosition(0, ray.origin);
                    controllerLine.SetPosition(1, ray.GetPoint(length));
                }
            }

            if (debugReticle != null)
            {
                bool show = DesktopMode || ShowDebugReticle;
                debugReticle.gameObject.SetActive(show);
                if (show) debugReticle.position = ray.GetPoint(length);
            }
        }
    }
}
