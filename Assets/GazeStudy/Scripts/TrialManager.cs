using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace COMP3180.GazeStudy
{
    /// <summary>
    /// Runs one participant's session:
    ///   per technique: practice block, then a block per target size (order counterbalanced by participant ID).
    /// Experimenter keys (on the PC keyboard):
    ///   Enter = start session / advance a screen   Up/Down = participant ID (idle only)
    ///   R = recentre targets in front of the participant   Esc = abort session (data kept)
    /// The participant advances screens with the trigger.
    /// </summary>
    public class TrialManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] SelectionRaySource ray;
        [SerializeField] TargetRing ring;
        [SerializeField] ConfirmInput confirm;
        [SerializeField] DataLogger logger;
        [Tooltip("World-space text inside the headset view.")]
        [SerializeField] TMP_Text hudText;
        [Tooltip("Screen-space overlay text for the experimenter (PC Game view).")]
        [SerializeField] TMP_Text statusText;

        [Header("Session")]
        [SerializeField] int participantId = 1;
        [Tooltip("Resume after an abort: index into the block list (0 = start).")]
        [SerializeField] int startAtBlock = 0;
        [SerializeField] int practiceTrials = 4;
        [SerializeField] int trialsPerBlock = 16;
        [SerializeField] TargetSize practiceSize = TargetSize.Large;

        [Header("Timing (seconds)")]
        [SerializeField] float itiMin = 0.8f;
        [SerializeField] float itiMax = 1.2f;
        [Tooltip("Presses earlier than this after target onset are ignored (carry-over / anticipation).")]
        [SerializeField] float inputLockout = 0.15f;
        [SerializeField] float flashSeconds = 0.2f;
        [SerializeField] float introMinSeconds = 1.0f;

        public bool IsRunning { get; private set; }

        List<BlockSpec> blocks = new List<BlockSpec>();
        Coroutine session;
        bool trialActive;
        int currentBlock, currentTrial;
        string warning = "";
        float warningUntil;
        float nextStatus;

        static readonly Color GoodColor = new Color(0.2f, 0.8f, 0.3f);
        static readonly Color BadColor = new Color(0.9f, 0.2f, 0.2f);

        // ---------------------------------------------------------------- lifecycle

        void Start()
        {
            ring.HideAll();
            ShowHud("READY\n\nExperimenter: press Enter to start");
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (!IsRunning)
                {
                    if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) StartSession();
                    if (kb.upArrowKey.wasPressedThisFrame) participantId++;
                    if (kb.downArrowKey.wasPressedThisFrame) participantId = Mathf.Max(1, participantId - 1);
                }
                else if (kb.escapeKey.wasPressedThisFrame)
                {
                    AbortSession("Session aborted by experimenter.");
                }

                if (kb.rKey.wasPressedThisFrame && !trialActive) Recenter();
            }
            UpdateStatus();
        }

        void OnDisable() { if (logger != null) logger.End(); }

        void Recenter() { ring.PlaceInFrontOf(ray.HeadPose); }

        // ---------------------------------------------------------------- session control

        void StartSession()
        {
            blocks = BuildBlocks(participantId);

            bool needsEye = blocks.Exists(b => b.Technique == Technique.GazeTrigger);
            if (needsEye && !ray.DesktopMode && !ray.EyeGazeAvailable && !ray.AllowHeadFallback)
            {
                Warn("EYE GAZE DEVICE NOT FOUND. Fix OpenXR eye tracking, or tick 'Allow Head Fallback For Eye' on SelectionRaySource (will be logged as HeadFallback).");
                return;
            }

            if (startAtBlock < 0 || startAtBlock >= blocks.Count) startAtBlock = 0;
            ray.ShowDebugReticle = false;
            logger.Begin(participantId);
            session = StartCoroutine(RunSession());
        }

        void AbortSession(string reason)
        {
            if (session != null) StopCoroutine(session);
            session = null;
            trialActive = false;
            IsRunning = false;
            ring.HideAll();
            logger.End();
            ShowHud("Session stopped.");
            Warn(reason);
        }

        // ---------------------------------------------------------------- the session itself

        IEnumerator RunSession()
        {
            IsRunning = true;

            for (int b = startAtBlock; b < blocks.Count; b++)
            {
                BlockSpec block = blocks[b];
                currentBlock = b;
                currentTrial = 0;

                ray.Mode = block.Technique == Technique.Controller ? RayMode.Controller : RayMode.Eye;
                ring.HideAll();
                ring.SetSize(block.Size);
                ring.PlaceInFrontOf(ray.HeadPose); // re-anchor each block to cope with head drift

                ShowHud(IntroText(block));
                yield return WaitForAdvance(introMinSeconds);
                HideHud();

                List<int> queue = BuildQueue(block, b);
                for (int t = 0; t < queue.Count; t++)
                {
                    currentTrial = t + 1;

                    // inter-trial interval: central dot pulls gaze back to the middle
                    ring.SetTargetsVisible(false);
                    ring.ShowFixation(true);
                    yield return new WaitForSeconds(Random.Range(itiMin, itiMax));
                    ring.ShowFixation(false);

                    // target onset
                    ring.Cue(queue[t]);
                    ring.SetTargetsVisible(true);
                    double onset = Time.realtimeSinceStartupAsDouble;
                    trialActive = true;

                    while (true)
                    {
                        yield return null;
                        if (confirm.PressedThisFrame &&
                            Time.realtimeSinceStartupAsDouble - onset >= inputLockout) break;
                    }

                    double pressTime = Time.realtimeSinceStartupAsDouble;
                    trialActive = false;
                    ray.Sample(); // freshest ray/hover at the moment of the press

                    SelectableTarget hovered = ray.Hovered;
                    SelectableTarget cued = ring.CuedTarget;
                    TrialOutcome outcome = hovered == null ? TrialOutcome.Miss
                                         : hovered == cued ? TrialOutcome.Correct
                                         : TrialOutcome.WrongTarget;

                    Ray r = ray.CurrentRay;
                    float err = Vector3.Angle(r.direction, cued.transform.position - r.origin);

                    logger.Log(new TrialRecord
                    {
                        Participant = participantId,
                        BlockIndex = b,
                        Technique = block.Technique,
                        Size = block.Size,
                        Practice = block.IsPractice,
                        TrialInBlock = t + 1,
                        TargetIndex = cued.Index,
                        SelectedIndex = hovered != null ? hovered.Index : -1,
                        Outcome = outcome,
                        TimeMs = (pressTime - onset) * 1000.0,
                        AngularErrorDeg = err,
                        Source = ray.CurrentSource,
                        EyeDeviceAvailable = ray.EyeGazeAvailable,
                        TargetSizeDeg = ring.CurrentSizeDeg,
                        DistanceM = ring.Distance
                    });

                    // identical, brief feedback in every condition
                    if (hovered != null) hovered.Flash(outcome == TrialOutcome.Correct ? GoodColor : BadColor);
                    yield return new WaitForSeconds(flashSeconds);
                    if (hovered != null) hovered.ClearFlash();
                }

                ring.HideAll();
            }

            ShowHud("ALL DONE\n\nThank you!\nPlease wait for the experimenter.");
            logger.End();
            IsRunning = false;
            session = null;
        }

        IEnumerator WaitForAdvance(float minSeconds)
        {
            float t0 = Time.unscaledTime;
            while (true)
            {
                yield return null;
                if (Time.unscaledTime - t0 < minSeconds) continue;
                var kb = Keyboard.current;
                bool enter = kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame);
                if (confirm.PressedThisFrame || enter) yield break;
            }
        }

        // ---------------------------------------------------------------- design: counterbalancing + trial order

        /// <summary>
        /// Order keyed to (participantId-1) % 4:
        ///   technique first: odd ID = Controller, even ID = Gaze+Trigger
        ///   size first:      IDs 1,2 = Large first, IDs 3,4 = Small first (ID 5 repeats ID 1)
        /// Each technique gets a short practice block (practiceSize) before its two real blocks.
        /// </summary>
        List<BlockSpec> BuildBlocks(int id)
        {
            bool controllerFirst = (id % 2) == 1;
            bool largeFirst = (((id - 1) / 2) % 2) == 0;

            Technique[] techs = controllerFirst
                ? new[] { Technique.Controller, Technique.GazeTrigger }
                : new[] { Technique.GazeTrigger, Technique.Controller };
            TargetSize[] sizes = largeFirst
                ? new[] { TargetSize.Large, TargetSize.Small }
                : new[] { TargetSize.Small, TargetSize.Large };

            var list = new List<BlockSpec>();
            int real = 0;
            foreach (var tech in techs)
            {
                list.Add(new BlockSpec { Technique = tech, Size = practiceSize, IsPractice = true, Trials = practiceTrials, DisplayNumber = 0 });
                foreach (var size in sizes)
                    list.Add(new BlockSpec { Technique = tech, Size = size, IsPractice = false, Trials = trialsPerBlock, DisplayNumber = ++real });
            }
            return list;
        }

        /// <summary>Balanced, seeded (reproducible) target order with no immediate repeats where possible.</summary>
        List<int> BuildQueue(BlockSpec block, int blockIndex)
        {
            var rng = new System.Random(participantId * 1000 + blockIndex);
            int reps = Mathf.Max(1, Mathf.CeilToInt((float)block.Trials / ring.Count));
            var list = new List<int>();
            for (int r = 0; r < reps; r++)
                for (int i = 0; i < ring.Count; i++) list.Add(i);

            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                int tmp = list[i]; list[i] = list[j]; list[j] = tmp;
            }

            if (list.Count > block.Trials) list.RemoveRange(block.Trials, list.Count - block.Trials);

            for (int i = 1; i < list.Count; i++)
            {
                if (list[i] != list[i - 1]) continue;
                for (int j = i + 1; j < list.Count; j++)
                {
                    if (list[j] != list[i - 1])
                    {
                        int tmp = list[i]; list[i] = list[j]; list[j] = tmp;
                        break;
                    }
                }
            }
            return list;
        }

        // ---------------------------------------------------------------- text

        string IntroText(BlockSpec block)
        {
            string header = block.IsPractice ? "PRACTICE" : "BLOCK " + block.DisplayNumber + " of 4";
            string how = block.Technique == Technique.Controller
                ? "CONTROLLER POINTING\nAim the laser at the BLUE sphere,\nthen press the trigger."
                : "LOOK + TRIGGER\nLook at the BLUE sphere,\nthen press the trigger.\n(Do not aim with the controller.)";
            string size = block.Size == TargetSize.Small ? "Targets: SMALL" : "Targets: LARGE";
            return header + "\n\n" + how + "\n\n" + size + "\nBe fast and accurate.\n\nPress the trigger when ready.";
        }

        void ShowHud(string text)
        {
            if (hudText == null) return;
            hudText.gameObject.SetActive(true);
            hudText.text = text;
        }

        void HideHud() { if (hudText != null) hudText.gameObject.SetActive(false); }

        void Warn(string message)
        {
            warning = message;
            warningUntil = Time.unscaledTime + 8f;
            Debug.LogWarning("[TrialManager] " + message);
        }

        void UpdateStatus()
        {
            if (statusText == null || Time.unscaledTime < nextStatus) return;
            nextStatus = Time.unscaledTime + 0.2f;

            string line1 = IsRunning
                ? $"P{participantId:00}  Block {currentBlock + 1}/{blocks.Count}  Trial {currentTrial}"
                : $"P{participantId:00}  IDLE   [Enter] start   [Up/Down] participant   [R] recentre   [Esc] abort";
            string line2 = $"Ray mode: {ray.Mode}   Source: {ray.CurrentSource}   EyeDevice: {ray.EyeGazeAvailable}   Desktop: {ray.DesktopMode}";
            string line3 = string.IsNullOrEmpty(logger.FilePath) ? "" : "Log: " + logger.FilePath;
            string line4 = Time.unscaledTime < warningUntil ? "\n" + warning : "";
            statusText.text = line1 + "\n" + line2 + "\n" + line3 + line4;
        }
    }
}
