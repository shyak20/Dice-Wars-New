using UnityEngine;

/// <summary>
/// Drives an effect object's scale and XY motion based on combat power progress.
/// Attach this to the effect GameObject in the fight scene.
/// </summary>
public sealed class PowerReactiveEffectController : MonoBehaviour
{
    [Header("Scale From Power")]
    [SerializeField, Min(0f)] private float minScale = 1f;
    [SerializeField, Min(0f)] private float maxScale = 2f;
    [SerializeField, Min(0f)] private float baseScaleTransitionTime = 0.5f;

    [Header("Pulse")]
    [SerializeField, Min(0f)] private float minPulseScale = 0.95f;
    [SerializeField, Min(0f)] private float maxPulseScale = 1.05f;
    [SerializeField, Min(0f)] private float scalePulseSpeedMin = 1f;
    [SerializeField, Min(0f)] private float scalePulseSpeedMax = 2f;

    [Header("Movement")]
    [SerializeField, Min(0f)] private float moveY = 0.2f;
    [SerializeField, Min(0f)] private float moveX = 0.1f;
    [SerializeField, Min(0f)] private float movementSpeedMin = 0.75f;
    [SerializeField, Min(0f)] private float movementSpeedMax = 1.5f;

    [Header("Sprite Alpha Pulse (Optional)")]
    [SerializeField] private SpriteRenderer pulseSpriteRenderer;
    [SerializeField, Min(0f)] private float spritePulseSpeedMin = 0.75f;
    [SerializeField, Min(0f)] private float spritePulseSpeedMax = 2f;
    [SerializeField, Range(0f, 1f)] private float spritePulseStrengthMin = 0.25f;
    [SerializeField, Range(0f, 1f)] private float spritePulseStrengthMax = 1f;

    [Header("Target")]
    [SerializeField] private Transform effectTransform;

    private Vector3 _baseLocalPosition;
    private float _targetNormalizedPower;
    private float _displayedNormalizedPower;
    private Color _baseSpriteColor = Color.white;

    private void Awake()
    {
        if (effectTransform == null)
            effectTransform = transform;

        if (effectTransform == null)
            throw new System.InvalidOperationException("PowerReactiveEffectController requires an assigned effectTransform.");

        _baseLocalPosition = effectTransform.localPosition;
        if (pulseSpriteRenderer != null)
            _baseSpriteColor = pulseSpriteRenderer.color;

        ValidateRanges();
        _targetNormalizedPower = 0f;
        _displayedNormalizedPower = 0f;
    }

    private void OnEnable()
    {
        CombatEvents.OnPowerChanged += HandlePowerChanged;
    }

    private void OnDisable()
    {
        CombatEvents.OnPowerChanged -= HandlePowerChanged;
    }

    private void Update()
    {
        _displayedNormalizedPower = StepDisplayedPowerTowardTarget(_displayedNormalizedPower, _targetNormalizedPower, baseScaleTransitionTime);

        float pulseSpeed = Mathf.Lerp(scalePulseSpeedMin, scalePulseSpeedMax, _displayedNormalizedPower);
        float pulseT = Mathf.PingPong(Time.time * pulseSpeed, 1f);
        float pulseScale = Mathf.Lerp(minPulseScale, maxPulseScale, pulseT);

        float baseWorldScale = Mathf.Lerp(minScale, maxScale, _displayedNormalizedPower);
        float finalWorldScale = baseWorldScale * pulseScale;
        SetUniformWorldScale(effectTransform, finalWorldScale);

        float movementSpeed = Mathf.Lerp(movementSpeedMin, movementSpeedMax, _displayedNormalizedPower);
        float movementPhase = Time.time * movementSpeed;
        float xAmplitude = moveX * _displayedNormalizedPower;
        float yAmplitude = moveY * _displayedNormalizedPower;
        float xOffset = Mathf.Sin(movementPhase) * xAmplitude;
        float yOffset = Mathf.Cos(movementPhase) * yAmplitude;

        effectTransform.localPosition = _baseLocalPosition + new Vector3(xOffset, yOffset, 0f);
        ApplySpriteAlphaPulse();
    }

    private void HandlePowerChanged(int currentPower, int maxPower)
    {
        if (maxPower <= 0)
        {
            _targetNormalizedPower = 0f;
            return;
        }

        // Scaling depends on power ratio only, so 25/50 and 3000/6000 produce the same result.
        _targetNormalizedPower = Mathf.Clamp01((float)currentPower / maxPower);
    }

    private void ValidateRanges()
    {
        if (maxScale < minScale)
            throw new System.InvalidOperationException("PowerReactiveEffectController: maxScale must be >= minScale.");

        if (maxPulseScale < minPulseScale)
            throw new System.InvalidOperationException("PowerReactiveEffectController: maxPulseScale must be >= minPulseScale.");

        if (scalePulseSpeedMax < scalePulseSpeedMin)
            throw new System.InvalidOperationException("PowerReactiveEffectController: scalePulseSpeedMax must be >= scalePulseSpeedMin.");

        if (movementSpeedMax < movementSpeedMin)
            throw new System.InvalidOperationException("PowerReactiveEffectController: movementSpeedMax must be >= movementSpeedMin.");

        if (spritePulseSpeedMax < spritePulseSpeedMin)
            throw new System.InvalidOperationException("PowerReactiveEffectController: spritePulseSpeedMax must be >= spritePulseSpeedMin.");

        if (spritePulseStrengthMax < spritePulseStrengthMin)
            throw new System.InvalidOperationException("PowerReactiveEffectController: spritePulseStrengthMax must be >= spritePulseStrengthMin.");
    }

    private void ApplySpriteAlphaPulse()
    {
        if (pulseSpriteRenderer == null)
            return;

        float spritePulseSpeed = Mathf.Lerp(spritePulseSpeedMin, spritePulseSpeedMax, _displayedNormalizedPower);
        float spritePulseStrength = Mathf.Lerp(spritePulseStrengthMin, spritePulseStrengthMax, _displayedNormalizedPower);
        float alphaT = Mathf.PingPong(Time.time * spritePulseSpeed, 1f);
        float alpha = Mathf.Lerp(0f, spritePulseStrength, alphaT);

        Color c = _baseSpriteColor;
        c.a = alpha;
        pulseSpriteRenderer.color = c;
    }

    private static float StepDisplayedPowerTowardTarget(float current, float target, float fullRangeTime)
    {
        if (fullRangeTime <= 0f)
            return target;

        // fullRangeTime is the duration for a 0->1 transition.
        // This gives linear timing: larger changes take proportionally longer.
        float normalizedUnitsPerSecond = 1f / fullRangeTime;
        return Mathf.MoveTowards(current, target, normalizedUnitsPerSecond * Time.deltaTime);
    }

    private static void SetUniformWorldScale(Transform target, float worldScale)
    {
        if (target.parent == null)
        {
            target.localScale = Vector3.one * worldScale;
            return;
        }

        Vector3 parentLossy = target.parent.lossyScale;
        float localX = parentLossy.x == 0f ? worldScale : worldScale / parentLossy.x;
        float localY = parentLossy.y == 0f ? worldScale : worldScale / parentLossy.y;
        float localZ = parentLossy.z == 0f ? worldScale : worldScale / parentLossy.z;
        target.localScale = new Vector3(localX, localY, localZ);
    }
}
