using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using UnityEngine.XR.OpenXR;
using VIVE.OpenXR;
using VIVE.OpenXR.EyeTracker;

namespace COMP3180.GazeStudy
{
    /// <summary>
    /// Reads each eye's openness from the VIVE eye tracker. A wink is one eye closed for a moment while
    /// the other stays open, so normal blinks don't count. Three quick winks or the left Y button switch
    /// the assist on or off. With assist on, every other wink shows up as Winked for one frame.
    /// </summary>
    [DefaultExecutionOrder(-45)]
    public class WinkInput : MonoBehaviour
    {
        const float ClosedBelow = 0.25f;
        const float OpenAbove = 0.6f;
        const float WinkHoldSeconds = 0.1f;
        const float TripleGapSeconds = 0.7f;
        const int WinksToToggle = 3;
        const float NoDataWarningSeconds = 10f;

        public bool AssistOn { get; private set; }
        public bool Winked { get; private set; }
        public event System.Action AssistChanged;

        /// <summary>Live eye values for the status text, as winks can't be checked without seeing them.</summary>
        public string EyeReadout { get; private set; } = "Eyes: no data";

        InputAction toggleAction;
        float closedSince = -1f, lastWinkTime = -100f, headsetActiveTime;
        bool waitingForOpen, gotData, warned;
        int winkCount;

        void OnEnable()
        {
            toggleAction = new InputAction("ToggleAssist", InputActionType.Button, "<XRController>{LeftHand}/{SecondaryButton}");
            toggleAction.Enable();
        }

        void OnDisable() => toggleAction.Dispose();

        public void ToggleAssist()
        {
            winkCount = 0;
            AssistOn = !AssistOn;
            AssistChanged?.Invoke();
        }

        void Update()
        {
            Winked = false;
            if (!XRSettings.isDeviceActive) return;

            if (toggleAction.WasPressedThisFrame()) ToggleAssist();

            bool read = TryReadEyes(out float left, out float right, out bool valid);
            EyeReadout = valid ? $"Eyes: L {left:0.00}  R {right:0.00}" : "Eyes: no data";
            if (!read) return;

            if (valid) gotData = true;
            headsetActiveTime += Time.unscaledDeltaTime;
            if (!gotData && !warned && headsetActiveTime >= NoDataWarningSeconds)
            {
                warned = true;
                Debug.LogWarning("[Wink] No valid eye openness from the VIVE eye tracker, so winks can't be detected. Check that eye tracking is on and calibrated in the headset, and that the app runs through VIVE Streaming.");
            }

            Process(left, right, valid, Time.unscaledTime);
        }

        /// <summary>One sample of both eyes. Public so the logic can be driven without a headset.</summary>
        public void Process(float left, float right, bool valid, float time)
        {
            Winked = false;
            bool leftWink = left < ClosedBelow && right > OpenAbove;
            bool rightWink = right < ClosedBelow && left > OpenAbove;

            if (!valid || (!leftWink && !rightWink))
            {
                closedSince = -1f;
                // Both eyes open again ends the wink. A blink or lost tracking doesn't, or a long wink could count twice.
                if (valid && left > OpenAbove && right > OpenAbove) waitingForOpen = false;
                return;
            }

            if (waitingForOpen) return;
            if (closedSince < 0f) closedSince = time;
            if (time - closedSince < WinkHoldSeconds) return;

            waitingForOpen = true;
            closedSince = -1f;
            RegisterWink(time);
        }

        void RegisterWink(float time)
        {
            winkCount = time - lastWinkTime <= TripleGapSeconds ? winkCount + 1 : 1;
            lastWinkTime = time;

            if (winkCount >= WinksToToggle)
            {
                ToggleAssist();
                return;
            }

            Winked = AssistOn;
        }

        static bool TryReadEyes(out float left, out float right, out bool valid)
        {
            left = right = 0f;
            valid = false;

            var feature = OpenXRSettings.Instance.GetFeature<ViveEyeTracker>();
            if (feature == null || !feature.enabled) return false;

            XR_HTC_eye_tracker.Interop.GetEyeGeometricData(out XrSingleEyeGeometricDataHTC[] eyes);
            if (eyes == null || eyes.Length < 2) return false;

            XrSingleEyeGeometricDataHTC leftEye = eyes[(int)XrEyePositionHTC.XR_EYE_POSITION_LEFT_HTC];
            XrSingleEyeGeometricDataHTC rightEye = eyes[(int)XrEyePositionHTC.XR_EYE_POSITION_RIGHT_HTC];
            left = leftEye.eyeOpenness;
            right = rightEye.eyeOpenness;
            valid = leftEye.isValid && rightEye.isValid;
            return true;
        }
    }
}
