using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Idle feedback on a draggable <see cref="RolledOutcomeToken"/> (Element Value prefab) while the player still needs to assign it:
/// shakes until drag starts, and shows Select Outline only while this token is the exclusive selected assignable piece.
/// </summary>
[RequireComponent(typeof(RolledOutcomeToken))]
public class RolledOutcomeTokenAssignHintFeedback : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Optional. Defaults to this object's RectTransform. Prefer a child visual so drag motion does not fight the shake rotation.")]
    [SerializeField] private RectTransform shakeTarget;
    [Tooltip("Select Outline — shown only while this token is the selected assignable piece.")]
    [SerializeField] private GameObject highlight;

    [Header("Idle shake")]
    [Tooltip("Peak Z rotation offset in degrees applied while waiting for assignment.")]
    [SerializeField, Min(0f)] private float shakeRotationAmplitude = 6f;
    [Tooltip("Oscillation speed in cycles per second.")]
    [SerializeField, Min(0.01f)] private float shakeFrequency = 12f;
    [Tooltip("When enabled, shake keeps running while Time.timeScale is 0 (e.g. combat paused).")]
    [SerializeField] private bool useUnscaledTime = true;

    private RolledOutcomeToken _token;
    private Coroutine _shakeRoutine;
    private float _shakeBaseLocalRotationZ;
    private bool _placedOnEnemy;

    void Awake()
    {
        _token = GetComponent<RolledOutcomeToken>();
        if (_token == null)
            throw new InvalidOperationException($"{nameof(RolledOutcomeTokenAssignHintFeedback)} on '{name}' requires a {nameof(RolledOutcomeToken)} on the same GameObject.");

        if (shakeTarget == null)
            shakeTarget = transform as RectTransform;
        if (shakeTarget == null)
            throw new InvalidOperationException($"{nameof(RolledOutcomeTokenAssignHintFeedback)} on '{name}': assign {nameof(shakeTarget)} or place this on a UI object with a RectTransform.");

        if (highlight == null)
            throw new InvalidOperationException($"{nameof(RolledOutcomeTokenAssignHintFeedback)} on '{name}': assign the highlight GameObject.");

        _shakeBaseLocalRotationZ = shakeTarget.localEulerAngles.z;
        SetHighlightActive(false);
    }

    void OnEnable()
    {
        _token.DragStarted += HandleDragStarted;
        _token.DragEnded += HandleDragEnded;
        _token.AssignedToEnemy += HandleAssignedToEnemy;
        _token.DragEnabledChanged += HandleDragEnabledChanged;
        _token.SelectionChanged += HandleSelectionChanged;
        RefreshFeedback(force: true);
    }

    void OnDisable()
    {
        _token.DragStarted -= HandleDragStarted;
        _token.DragEnded -= HandleDragEnded;
        _token.AssignedToEnemy -= HandleAssignedToEnemy;
        _token.DragEnabledChanged -= HandleDragEnabledChanged;
        _token.SelectionChanged -= HandleSelectionChanged;
        StopShake(resetRotation: true);
        SetHighlightActive(false);
        _placedOnEnemy = false;
    }

    void HandleDragEnabledChanged(bool _) => RefreshFeedback(force: true);

    void HandleSelectionChanged(bool _) => RefreshFeedback(force: true);

    void HandleDragStarted() => RefreshFeedback(force: true);

    void HandleDragEnded() => RefreshFeedback(force: true);

    void HandleAssignedToEnemy()
    {
        _placedOnEnemy = true;
        StopShake(resetRotation: true);
        SetHighlightActive(false);
    }

    void RefreshFeedback(bool force)
    {
        if (!isActiveAndEnabled)
            return;

        var waitingForAssign = _token.IsDragEnabled;
        if (!waitingForAssign || _placedOnEnemy)
        {
            StopShake(resetRotation: true);
            SetHighlightActive(false);
            return;
        }

        var shouldShake = _token.IsSelected && !_token.IsDragging;
        if (shouldShake)
            StartShakeIfNeeded(force);
        else
            StopShake(resetRotation: true);

        SetHighlightActive(_token.IsSelected);
    }

    void StartShakeIfNeeded(bool force)
    {
        if (_shakeRoutine != null && !force)
            return;

        StopShake(resetRotation: true);
        _shakeBaseLocalRotationZ = shakeTarget.localEulerAngles.z;
        _shakeRoutine = StartCoroutine(CoIdleShake());
    }

    void StopShake(bool resetRotation)
    {
        if (_shakeRoutine != null)
        {
            StopCoroutine(_shakeRoutine);
            _shakeRoutine = null;
        }

        if (resetRotation && shakeTarget != null)
        {
            var euler = shakeTarget.localEulerAngles;
            euler.z = _shakeBaseLocalRotationZ;
            shakeTarget.localEulerAngles = euler;
        }
    }

    IEnumerator CoIdleShake()
    {
        var phase = 0f;
        while (true)
        {
            phase += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            var z = _shakeBaseLocalRotationZ + Mathf.Sin(phase * shakeFrequency) * shakeRotationAmplitude;
            var euler = shakeTarget.localEulerAngles;
            euler.z = z;
            shakeTarget.localEulerAngles = euler;
            yield return null;
        }
    }

    void SetHighlightActive(bool on)
    {
        if (highlight.activeSelf != on)
            highlight.SetActive(on);
    }
}
