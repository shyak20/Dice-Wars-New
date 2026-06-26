using UnityEngine;

/// <summary>
/// Simple breathing-style movement animation using eased ping-pong motion on X and Y.
/// RectTransform targets apply motion additively in LateUpdate so spread/layout animators can coexist.
/// </summary>
public sealed class BreathAnimationScript : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveX = 0.1f;
    [SerializeField] private float moveY = 0.1f;
    [SerializeField, Min(0f)] private float movementSpeed = 1f;
    [SerializeField] private bool randomizeStartVariation;

    [Header("Target")]
    [SerializeField] private Transform targetTransform;
    [Header("Pause Conditions")]
    [SerializeField] private bool pauseOnWinAndFaceRewardScreens = true;

    private Vector3 _baseLocalPosition;
    private float _loopTimeOffset;
    private RectTransform _targetRect;
    private Vector2 _lastBreathOffset;
    private DieFaceSpreadView _spreadViewAncestor;
    private WinStageFlowController _winStageFlow;
    private FaceRewardManager _faceRewardManager;

    private void Awake()
    {
        if (targetTransform == null)
            targetTransform = transform;

        if (targetTransform == null)
            throw new System.InvalidOperationException("BreathAnimationScript requires an assigned targetTransform.");

        _targetRect = targetTransform as RectTransform;
        _baseLocalPosition = targetTransform.localPosition;
        _loopTimeOffset = 0f;

        if (randomizeStartVariation)
        {
            if (_targetRect == null)
            {
                var randomStartOffset = new Vector3(
                    Random.Range(-moveX, moveX),
                    Random.Range(-moveY, moveY),
                    0f);

                _baseLocalPosition += randomStartOffset;
            }

            _loopTimeOffset = Random.Range(0f, 1000f);
        }

        CacheSpreadAncestor();
        _winStageFlow = FindObjectOfType<WinStageFlowController>(true);
        _faceRewardManager = FindObjectOfType<FaceRewardManager>(true);
    }

    private void OnEnable()
    {
        _lastBreathOffset = Vector2.zero;
        CacheSpreadAncestor();
    }

    private void OnDisable()
    {
        RemoveBreathOffset();
    }

    private void Update()
    {
        if (_targetRect != null)
            return;

        if (pauseOnWinAndFaceRewardScreens && ShouldPauseForUi())
        {
            targetTransform.localPosition = _baseLocalPosition;
            return;
        }

        ApplyLocalPositionBreath();
    }

    private void LateUpdate()
    {
        if (_targetRect == null)
            return;

        if (pauseOnWinAndFaceRewardScreens && ShouldPauseForUi())
        {
            RemoveBreathOffset();
            return;
        }

        ApplyAnchoredBreath();
    }

    private void ApplyLocalPositionBreath()
    {
        var offset = ComputeBreathOffset();
        targetTransform.localPosition = _baseLocalPosition + new Vector3(offset.x, offset.y, 0f);
    }

    private void ApplyAnchoredBreath()
    {
        if (_lastBreathOffset.sqrMagnitude > 0.0001f)
        {
            _targetRect.anchoredPosition -= _lastBreathOffset;
            _lastBreathOffset = Vector2.zero;
        }

        var offset = ComputeBreathOffset();
        _targetRect.anchoredPosition += offset;
        _lastBreathOffset = offset;
    }

    private void RemoveBreathOffset()
    {
        if (_targetRect == null || _lastBreathOffset.sqrMagnitude < 0.0001f)
            return;

        _targetRect.anchoredPosition -= _lastBreathOffset;
        _lastBreathOffset = Vector2.zero;
    }

    private Vector2 ComputeBreathOffset()
    {
        var pingPong = Mathf.PingPong((Time.time + _loopTimeOffset) * movementSpeed, 1f);
        var easedT = EaseInOutSine(pingPong);

        var x = Mathf.Lerp(-moveX, moveX, easedT);
        var y = Mathf.Lerp(-moveY, moveY, easedT);
        return new Vector2(x, y);
    }

    private static float EaseInOutSine(float t)
    {
        return 0.5f - 0.5f * Mathf.Cos(Mathf.PI * Mathf.Clamp01(t));
    }

    private void CacheSpreadAncestor()
    {
        _spreadViewAncestor = GetComponentInParent<DieFaceSpreadView>(true);
    }

    private bool ShouldPauseForUi()
    {
        if (_winStageFlow == null)
            _winStageFlow = FindObjectOfType<WinStageFlowController>(true);
        if (_faceRewardManager == null)
            _faceRewardManager = FindObjectOfType<FaceRewardManager>(true);

        var winVisible = _winStageFlow != null && _winStageFlow.IsWinStageVisible;
        var faceRewardVisible = _faceRewardManager != null && _faceRewardManager.gameObject.activeInHierarchy;

        if (faceRewardVisible && IsUnderActiveFaceSpread())
            return false;

        return winVisible || faceRewardVisible;
    }

    private bool IsUnderActiveFaceSpread()
    {
        if (_spreadViewAncestor == null)
            CacheSpreadAncestor();

        return _spreadViewAncestor != null && _spreadViewAncestor.isActiveAndEnabled;
    }
}
