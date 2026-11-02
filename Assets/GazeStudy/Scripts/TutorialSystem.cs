using System;
using TMPro;
using UnityEngine;

namespace COMP3180.GazeStudy
{
    public enum Tutorial { Movement, Interaction, EyeGaze }

    /// <summary>
    /// Short tutorials shown on a panel in front of the head. Each step advances when the player does
    /// what it asks.
    /// </summary>
    public class TutorialSystem : MonoBehaviour
    {
        const float WalkDistance = 2f;
        const float TurnAngle = 45f;
        const float DragDistance = 0.5f;
        const float CompleteCardSeconds = 4f;

        class Step
        {
            public string text;
            public Action begin;
            public Func<bool> done;
        }

        [SerializeField] GazePickupSelector selector;
        [SerializeField] PlayerBody body;
        [SerializeField] Transform head;
        [SerializeField] GameObject panel;
        [SerializeField] TMP_Text panelText;

        Step[] steps;
        int stepIndex;
        string completeText;
        float hideTime;
        Vector3 startPosition, startForward;
        bool startGaze;

        public bool IsActive => panel.activeSelf;

        public void Begin(Tutorial tutorial)
        {
            switch (tutorial)
            {
                case Tutorial.Movement: steps = MovementSteps(); completeText = "Movement tutorial complete"; break;
                case Tutorial.Interaction: steps = InteractionSteps(); completeText = "Interaction tutorial complete\nPick up the gun to enter the prototype area, and step on the red pad to reset everything."; break;
                default: steps = EyeGazeSteps(); completeText = "Eye gaze tutorial complete"; break;
            }
            panel.SetActive(true);
            StartStep(0);
        }

        public void Stop()
        {
            steps = null;
            panel.SetActive(false);
        }

        void Update()
        {
            if (!panel.activeSelf) return;

            if (steps == null)
            {
                if (Time.time >= hideTime) panel.SetActive(false);
                return;
            }

            if (!steps[stepIndex].done()) return;

            if (stepIndex + 1 < steps.Length)
            {
                StartStep(stepIndex + 1);
            }
            else
            {
                steps = null;
                panelText.text = completeText;
                hideTime = Time.time + CompleteCardSeconds;
            }
        }

        void StartStep(int index)
        {
            stepIndex = index;
            steps[index].begin?.Invoke();
            panelText.text = $"Step {index + 1} of {steps.Length}\n{steps[index].text}\n<size=70%>H / left stick click: exit tutorial</size>";
        }

        void MarkPlayer()
        {
            startPosition = head.position;
            startForward = FlatForward();
        }

        float Walked()
        {
            Vector3 offset = head.position - startPosition;
            offset.y = 0f;
            return offset.magnitude;
        }

        Vector3 FlatForward() => Vector3.ProjectOnPlane(head.forward, Vector3.up);

        Step[] MovementSteps() => new[]
        {
            new Step { text = "Walk a few steps.\nWASD or left stick", begin = MarkPlayer, done = () => Walked() >= WalkDistance },
            new Step { text = "Look around.\nHold right mouse and move, or use the right stick", begin = MarkPlayer, done = () => Vector3.Angle(startForward, FlatForward()) >= TurnAngle },
            new Step { text = "Move faster.\nHold Shift, or the left trigger, while walking", begin = MarkPlayer, done = () => body.IsSprinting && Walked() >= WalkDistance }
        };

        Step[] InteractionSteps() => new[]
        {
            new Step { text = "Look at an object until it brightens.", done = () => selector.Hovered != null },
            new Step { text = "Pick it up.\nHold left mouse or any free controller button", done = () => selector.Held != null },
            new Step
            {
                text = "Keep holding and look around to drag it.",
                begin = () => { if (selector.Held != null) startPosition = selector.Held.transform.position; },
                done = () => selector.Held == null || Vector3.Distance(selector.Held.transform.position, startPosition) >= DragDistance
            },
            new Step { text = "Let go to drop it.", done = () => selector.Held == null }
        };

        Step[] EyeGazeSteps() => new[]
        {
            new Step
            {
                text = "Switch eye gaze.\nPress Ctrl, or the right A button",
                begin = () => startGaze = selector.GazeEnabled,
                done = () => selector.GazeEnabled != startGaze
            },
            new Step { text = "Eye gaze must be on to aim with your eyes.\nPress the same button until it is ON", done = () => selector.GazeEnabled },
            new Step { text = "Look at an object until it brightens.", done = () => selector.Hovered != null }
        };
    }
}
