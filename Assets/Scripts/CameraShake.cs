using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Shakes a camera's local position. Multiple instances may exist (map + preloaded fight);
/// <see cref="Instance"/> and <see cref="ShakeActive"/> resolve the one in the active scene that is enabled.
/// Optional shake targets move in sync (overlay / world-space UI on the map).
/// UI offsets are applied in <see cref="LateUpdate"/> so Screen Space canvases are not reset by Unity first.
/// </summary>
public class CameraShake : MonoBehaviour
{
    [Serializable]
    public sealed class AdditionalShakeTarget
    {
        public Transform target;
        [Min(0f)] public float magnitudeScale = 1f;
    }

    sealed class RuntimeAdditionalShakeTarget
    {
        public Transform Target;
        public float MagnitudeScale;
    }

    struct ActiveShakeCapture
    {
        public Transform transform;
        public Vector3 originalLocalPosition;
        public Vector2 originalAnchoredPosition;
        public bool useAnchoredPosition;
        public float magnitudeScale;
    }

    static readonly List<CameraShake> Registry = new List<CameraShake>();

    [Header("Additional shake targets (overlay / world-space UI)")]
    [SerializeField] private List<AdditionalShakeTarget> additionalShakeTargets = new List<AdditionalShakeTarget>();

    readonly List<RuntimeAdditionalShakeTarget> _runtimeAdditionalTargets = new List<RuntimeAdditionalShakeTarget>();
    readonly List<ActiveShakeCapture> _activeCaptures = new List<ActiveShakeCapture>();

    /// <summary>Active, enabled shake in <see cref="SceneManager.GetActiveScene"/> when possible.</summary>
    public static CameraShake Instance => ResolveForActiveScene();

    private Vector3 _originalLocalPosition;
    private Coroutine _shakeRoutine;
    private bool _isShaking;
    private float _shakeDuration;
    private float _shakeMagnitude;
    private float _shakeElapsed;
    private Vector2 _currentShakeOffset;

    private void OnEnable()
    {
        Register();
        CaptureOriginalLocalPosition();
    }

    private void OnDisable()
    {
        if (_shakeRoutine != null)
        {
            StopCoroutine(_shakeRoutine);
            _shakeRoutine = null;
        }

        _isShaking = false;
        RestoreAllTransforms();
        Unregister();
    }

    private void OnDestroy() => Unregister();

    private void LateUpdate()
    {
        if (!_isShaking)
            return;

        ApplyShakeOffset(_currentShakeOffset);
    }

    /// <summary>Shakes the active scene's camera when one is available.</summary>
    public static void ShakeActive(float duration, float magnitude) => Instance?.Shake(duration, magnitude);

    /// <summary>Registers a transform to receive the same shake offset as this camera (e.g. overlay UI group).</summary>
    public void RegisterAdditionalTarget(Transform target, float magnitudeScale = 1f)
    {
        if (target == null)
            throw new ArgumentNullException(nameof(target));

        for (var i = 0; i < _runtimeAdditionalTargets.Count; i++)
        {
            if (_runtimeAdditionalTargets[i].Target == target)
                return;
        }

        _runtimeAdditionalTargets.Add(new RuntimeAdditionalShakeTarget
        {
            Target = target,
            MagnitudeScale = Mathf.Max(0f, magnitudeScale),
        });
    }

    /// <summary>
    /// Registers shake targets for a canvas. Screen-space canvases shake each direct child because Unity resets the canvas root every frame.
    /// </summary>
    public void RegisterCanvasForUiShake(Canvas canvas, float screenSpaceMagnitudeScale, float worldSpaceMagnitudeScale)
    {
        if (canvas == null)
            return;

        if (canvas.renderMode == RenderMode.WorldSpace)
        {
            RegisterAdditionalTarget(canvas.transform, worldSpaceMagnitudeScale);
            RegisterDirectChildren(canvas.transform, worldSpaceMagnitudeScale);
            return;
        }

        RegisterDirectChildren(canvas.transform, screenSpaceMagnitudeScale);
    }

    public void ClearRuntimeAdditionalTargets() => _runtimeAdditionalTargets.Clear();

    public int GetRuntimeAdditionalTargetCount() => _runtimeAdditionalTargets.Count;

    public void Shake(float duration, float magnitude)
    {
        if (!isActiveAndEnabled || !gameObject.activeInHierarchy)
            return;

        CaptureOriginalLocalPosition();
        CaptureAdditionalTargets();

        _shakeDuration = Mathf.Max(0f, duration);
        _shakeMagnitude = magnitude;
        _shakeElapsed = 0f;
        _currentShakeOffset = Vector2.zero;
        _isShaking = _shakeDuration > 0f && _shakeMagnitude > 0f;

        if (_shakeRoutine != null)
            StopCoroutine(_shakeRoutine);

        if (!_isShaking)
            return;

        _shakeRoutine = StartCoroutine(CoShake());
    }

    private IEnumerator CoShake()
    {
        while (_shakeElapsed < _shakeDuration)
        {
            _currentShakeOffset = UnityEngine.Random.insideUnitCircle * _shakeMagnitude;
            _shakeElapsed += Time.deltaTime;
            yield return null;
        }

        _isShaking = false;
        RestoreAllTransforms();
        _shakeRoutine = null;
    }

    private void RegisterDirectChildren(Transform root, float magnitudeScale)
    {
        for (var i = 0; i < root.childCount; i++)
            RegisterAdditionalTarget(root.GetChild(i), magnitudeScale);
    }

    private void ApplyShakeOffset(Vector2 offset)
    {
        transform.localPosition = new Vector3(
            _originalLocalPosition.x + offset.x,
            _originalLocalPosition.y + offset.y,
            _originalLocalPosition.z);

        for (var i = 0; i < _activeCaptures.Count; i++)
        {
            var capture = _activeCaptures[i];
            if (capture.transform == null)
                continue;

            var scaled = offset * capture.magnitudeScale;
            if (capture.useAnchoredPosition && capture.transform is RectTransform rect)
            {
                rect.anchoredPosition = new Vector2(
                    capture.originalAnchoredPosition.x + scaled.x,
                    capture.originalAnchoredPosition.y + scaled.y);
            }
            else
            {
                var local = capture.originalLocalPosition;
                capture.transform.localPosition = new Vector3(
                    local.x + scaled.x,
                    local.y + scaled.y,
                    local.z);
            }
        }
    }

    private void CaptureAdditionalTargets()
    {
        _activeCaptures.Clear();

        CaptureTargetList(additionalShakeTargets);
        for (var i = 0; i < _runtimeAdditionalTargets.Count; i++)
        {
            var entry = _runtimeAdditionalTargets[i];
            CaptureTarget(entry.Target, entry.MagnitudeScale);
        }
    }

    private void CaptureTargetList(List<AdditionalShakeTarget> targets)
    {
        if (targets == null)
            return;

        for (var i = 0; i < targets.Count; i++)
        {
            var entry = targets[i];
            if (entry?.target == null)
                continue;
            CaptureTarget(entry.target, entry.magnitudeScale);
        }
    }

    private void CaptureTarget(Transform target, float magnitudeScale)
    {
        if (target == null)
            return;

        for (var i = 0; i < _activeCaptures.Count; i++)
        {
            if (_activeCaptures[i].transform == target)
                return;
        }

        var capture = new ActiveShakeCapture
        {
            transform = target,
            originalLocalPosition = target.localPosition,
            magnitudeScale = Mathf.Max(0f, magnitudeScale),
        };

        if (target is RectTransform rect)
        {
            capture.useAnchoredPosition = true;
            capture.originalAnchoredPosition = rect.anchoredPosition;
        }

        _activeCaptures.Add(capture);
    }

    private void RestoreAllTransforms()
    {
        transform.localPosition = _originalLocalPosition;

        for (var i = 0; i < _activeCaptures.Count; i++)
        {
            var capture = _activeCaptures[i];
            if (capture.transform == null)
                continue;

            if (capture.useAnchoredPosition && capture.transform is RectTransform rect)
                rect.anchoredPosition = capture.originalAnchoredPosition;
            else
                capture.transform.localPosition = capture.originalLocalPosition;
        }

        _activeCaptures.Clear();
    }

    private void CaptureOriginalLocalPosition()
    {
        if (_isShaking)
            return;
        _originalLocalPosition = transform.localPosition;
    }

    private void Register()
    {
        if (!Registry.Contains(this))
            Registry.Add(this);
    }

    private void Unregister() => Registry.Remove(this);

    static void CleanupDestroyedEntries()
    {
        for (var i = Registry.Count - 1; i >= 0; i--)
        {
            if (Registry[i] == null)
                Registry.RemoveAt(i);
        }
    }

    static CameraShake ResolveForActiveScene()
    {
        CleanupDestroyedEntries();
        if (Registry.Count == 0)
            return null;

        var activeScene = SceneManager.GetActiveScene();
        CameraShake fallback = null;
        for (var i = 0; i < Registry.Count; i++)
        {
            var shake = Registry[i];
            if (shake == null || !shake.isActiveAndEnabled || !shake.gameObject.activeInHierarchy)
                continue;

            if (fallback == null)
                fallback = shake;
            if (activeScene.IsValid() && shake.gameObject.scene == activeScene)
                return shake;
        }

        return fallback;
    }
}
