using UnityEngine;

/// <summary>
/// Simple breathing-style movement animation using eased ping-pong motion on X and Y.
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
    private WinStageFlowController _winStageFlow;
    private FaceRewardManager _faceRewardManager;

    private void Awake()
    {
        if (targetTransform == null)
            targetTransform = transform;

        if (targetTransform == null)
            throw new System.InvalidOperationException("BreathAnimationScript requires an assigned targetTransform.");

        _baseLocalPosition = targetTransform.localPosition;
        _loopTimeOffset = 0f;

        if (randomizeStartVariation)
        {
            Vector3 randomStartOffset = new Vector3(
                Random.Range(-moveX, moveX),
                Random.Range(-moveY, moveY),
                0f);

            _baseLocalPosition += randomStartOffset;
            _loopTimeOffset = Random.Range(0f, 1000f);
        }

        _winStageFlow = FindObjectOfType<WinStageFlowController>(true);
        _faceRewardManager = FindObjectOfType<FaceRewardManager>(true);
    }

    private void Update()
    {
        if (pauseOnWinAndFaceRewardScreens && ShouldPauseForUi())
        {
            targetTransform.localPosition = _baseLocalPosition;
            return;
        }

        float pingPong = Mathf.PingPong((Time.time + _loopTimeOffset) * movementSpeed, 1f);
        float easedT = EaseInOutSine(pingPong);

        float x = Mathf.Lerp(-moveX, moveX, easedT);
        float y = Mathf.Lerp(-moveY, moveY, easedT);

        targetTransform.localPosition = _baseLocalPosition + new Vector3(x, y, 0f);
    }

    private static float EaseInOutSine(float t)
    {
        return 0.5f - 0.5f * Mathf.Cos(Mathf.PI * Mathf.Clamp01(t));
    }

    private bool ShouldPauseForUi()
    {
        if (_winStageFlow == null)
            _winStageFlow = FindObjectOfType<WinStageFlowController>(true);
        if (_faceRewardManager == null)
            _faceRewardManager = FindObjectOfType<FaceRewardManager>(true);

        var winVisible = _winStageFlow != null && _winStageFlow.IsWinStageVisible;
        var faceRewardVisible = _faceRewardManager != null && _faceRewardManager.gameObject.activeInHierarchy;
        return winVisible || faceRewardVisible;
    }
}
