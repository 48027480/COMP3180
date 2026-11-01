using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace COMP3180.GazeStudy
{
    /// <summary>
    /// Tomorrow's gate check. Works only while no session is running.
    ///   F2 = toggle debug reticle (shows where the active ray points)
    ///   F3 = cycle ray mode: Eye -> Controller -> Head
    ///   F4 = show/hide the target ring (check hover highlight follows your eyes)
    ///   F5 = switch Small/Large targets   R = recentre (handled by TrialManager)
    /// Look at a small target with Eye mode: if the highlight tracks your eyes (not your head), eye gaze works.
    /// </summary>
    public class GazeProbe : MonoBehaviour
    {
        [SerializeField] SelectionRaySource ray;
        [SerializeField] TrialManager trials;
        [SerializeField] TargetRing ring;
        [SerializeField] TMP_Text probeText; // optional, screen-space

        bool ringVisible;
        TargetSize size = TargetSize.Large;

        void Update()
        {
            if (trials != null && trials.IsRunning) return;
            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb.f2Key.wasPressedThisFrame) ray.ShowDebugReticle = !ray.ShowDebugReticle;

            if (kb.f3Key.wasPressedThisFrame)
            {
                ray.Mode = ray.Mode == RayMode.Eye ? RayMode.Controller
                         : ray.Mode == RayMode.Controller ? RayMode.Head : RayMode.Eye;
                Debug.Log("[GazeProbe] Ray mode = " + ray.Mode);
            }

            if (kb.f4Key.wasPressedThisFrame)
            {
                ringVisible = !ringVisible;
                if (ringVisible) ring.PlaceInFrontOf(ray.HeadPose);
                ring.SetTargetsVisible(ringVisible);
            }

            if (kb.f5Key.wasPressedThisFrame)
            {
                size = size == TargetSize.Large ? TargetSize.Small : TargetSize.Large;
                ring.SetSize(size);
            }

            if (probeText != null)
            {
                probeText.text = $"PROBE  mode={ray.Mode}  source={ray.CurrentSource}  eyeDevice={ray.EyeGazeAvailable}  " +
                                 $"hover={(ray.Hovered != null ? ray.Hovered.Index.ToString() : "-")}  size={size}\n" +
                                 "F2 reticle  F3 mode  F4 targets  F5 size  R recentre";
            }
        }
    }
}
