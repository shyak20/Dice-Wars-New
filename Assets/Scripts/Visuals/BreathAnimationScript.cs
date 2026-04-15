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

    [Header("Target")]
    [SerializeField] private Transform targetTransform;

    private Vector3 _baseLocalPosition;

    private void Awake()
    {
        if (targetTransform == null)
            targetTransform = transform;

        if (targetTransform == null)
            throw new System.InvalidOperationException("BreathAnimationScript requires an assigned targetTransform.");

        _baseLocalPosition = targetTransform.localPosition;
    }

    private void Update()
    {
        float pingPong = Mathf.PingPong(Time.time * movementSpeed, 1f);
        float easedT = EaseInOutSine(pingPong);

        float x = Mathf.Lerp(-moveX, moveX, easedT);
        float y = Mathf.Lerp(-moveY, moveY, easedT);

        targetTransform.localPosition = _baseLocalPosition + new Vector3(x, y, 0f);
    }

    private static float EaseInOutSine(float t)
    {
        return 0.5f - 0.5f * Mathf.Cos(Mathf.PI * Mathf.Clamp01(t));
    }
}
