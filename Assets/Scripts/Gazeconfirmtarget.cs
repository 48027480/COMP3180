using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Attach to any target object alongside an XRSimpleInteractable.
///
/// Design principle this enforces: gaze (or the controller ray, in the
/// controller-only condition) only ever HOVERS a target. A target is only
/// ever CONFIRMED when the participant's controller trigger is pressed
/// while that target is the current hover target. This keeps both
/// experimental conditions (controller pointing vs gaze+trigger) logging
/// through the exact same confirm path, so your CSV output is directly
/// comparable between conditions.
///
/// Works for Condition A (controller ray) because XRRayInteractor also
/// raises hover events before select — this script does not care which
/// interactor produced the hover, only that a hover exists when trigger
/// is pressed.
/// </summary>
[RequireComponent(typeof(XRSimpleInteractable))]
public class GazeConfirmTarget : MonoBehaviour
{
    [Header("Identification (for logging)")]
    [Tooltip("Unique id for this target, written into the log row.")]
    public string targetId;

    [Tooltip("Is this the correct/instructed target for the current trial? " +
             "Set by your trial manager at trial start.")]
    public bool isCorrectTarget;

    [Header("Events")]
    public UnityEvent<GazeConfirmResult> OnConfirmed;

    private XRSimpleInteractable _interactable;
    private bool _isHovered;
    private float _hoverStartTime;

    private void Awake()
    {
        _interactable = GetComponent<XRSimpleInteractable>();
    }

    private void OnEnable()
    {
        _interactable.hoverEntered.AddListener(HandleHoverEntered);
        _interactable.hoverExited.AddListener(HandleHoverExited);
    }

    private void OnDisable()
    {
        _interactable.hoverEntered.RemoveListener(HandleHoverEntered);
        _interactable.hoverExited.RemoveListener(HandleHoverExited);
    }

    private void HandleHoverEntered(UnityEngine.XR.Interaction.Toolkit.Interactors.HoverEnterEventArgs args)
    {
        _isHovered = true;
        _hoverStartTime = Time.time;
    }

    private void HandleHoverExited(UnityEngine.XR.Interaction.Toolkit.Interactors.HoverExitEventArgs args)
    {
        _isHovered = false;
    }

    /// <summary>
    /// Call this from your controller trigger input handler (e.g. an
    /// InputActionReference.performed callback in your trial manager),
    /// passing the trial start time so selection time is computed
    /// relative to when the target set appeared, not relative to hover
    /// start. Only fires a result if THIS target is currently hovered.
    /// </summary>
    public void TryConfirm(float trialStartTime)
    {
        if (!_isHovered) return;

        var result = new GazeConfirmResult
        {
            targetId = targetId,
            wasCorrect = isCorrectTarget,
            selectionTimeSeconds = Time.time - trialStartTime,
            dwellBeforeConfirmSeconds = Time.time - _hoverStartTime,
            timestampUtc = DateTime.UtcNow
        };

        OnConfirmed?.Invoke(result);
    }

    public bool IsHovered => _isHovered;
}

/// <summary>
/// One row of experiment data. Feed these into your CSV/logging system.
/// </summary>
[Serializable]
public struct GazeConfirmResult
{
    public string targetId;
    public bool wasCorrect;
    public float selectionTimeSeconds;
    public float dwellBeforeConfirmSeconds;
    public DateTime timestampUtc;
}