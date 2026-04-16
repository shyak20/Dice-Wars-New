using System.Collections;
using UnityEngine;
using UnityEngine.VFX;

/// <summary>
/// Drives an effect object's scale and XY motion based on combat power progress.
/// Attach this to the effect GameObject in the fight scene.
/// </summary>
public sealed class PowerReactiveEffectController : MonoBehaviour
{
    [Header("Power → blend (non-linear)")]
    [Tooltip("Maps displayed normalized power (time/X, 0–1) to the blend factor (value/Y, typically 0–1) used for Min→Max lerps except Scale From Power (minScale→maxScale), which stays linear.")]
    [SerializeField] private AnimationCurve powerToBlendCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

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

    [Header("Visual Effect — Wave Rate (Optional)")]
    [Tooltip("VFX Graph component on the effect object. Expose a float named like Wave Rate on the blackboard.")]
    [SerializeField] private VisualEffect waveVisualEffect;
    [SerializeField] private string waveRatePropertyName = "Wave Rate";
    [SerializeField] private float waveRateMin = 0f;
    [SerializeField] private float waveRateMax = 1f;

    [Header("Visual Effect — Flare Bright Size (Optional)")]
    [Tooltip("When enabled, sets the exposed float (e.g. FlareBrightSize) from min at 0 power to max at full power.")]
    [SerializeField] private bool applyFlareBrightSize;
    [SerializeField] private string flareBrightSizePropertyName = "FlareBrightSize";
    [SerializeField] private float flareBrightSizeMin = 0f;
    [SerializeField] private float flareBrightSizeMax = 1f;

    [Header("Visual Effect — Sphere Texture Speed (Optional)")]
    [Tooltip("When enabled, sets the exposed Vector2 (e.g. SphereTextureSpeed) from min at 0 power to max at full power.")]
    [SerializeField] private bool applySphereTextureSpeed;
    [SerializeField] private string sphereTextureSpeedPropertyName = "SphereTextureSpeed";
    [SerializeField] private Vector2 sphereTextureSpeedMin = Vector2.zero;
    [SerializeField] private Vector2 sphereTextureSpeedMax = Vector2.one;

    [Header("Turn end — fly to enemy")]
    [Tooltip("How long the orb takes to reach the enemy (seconds).")]
    [SerializeField, Min(0.01f)] private float flyTime = 0.45f;
    [Tooltip("X = normalized flight time (0–1). Y = blend along the path from start to the enemy anchor (0 = start, 1 = hit). Default is linear.")]
    [SerializeField] private AnimationCurve flyCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    [Header("Flight Arc")]
    [Tooltip("Extra lateral offset added to the orb during flight (world-space X). Set 0 to disable. Use negative values to arc the other way.")]
    [SerializeField] private float flightArcHeight = 0.2f;
    [Tooltip("X = normalized flight time (0–1). Y = arc multiplier. Default peaks at mid and returns to 0 at the end.")]
    [SerializeField] private AnimationCurve flightArcCurve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.5f, 1f), new Keyframe(1f, 0f));

    [Header("Target")]
    [SerializeField] private Transform effectTransform;

    private Vector3 _baseLocalPosition;
    private int _currentCombatPower;
    private float _targetNormalizedPower;
    private float _displayedNormalizedPower;
    private Color _baseSpriteColor = Color.white;
    private bool _isFlyingToEnemy;
    private bool _postHitHiddenAtEnemy;
    private int _waveRatePropertyId;
    private int _flareBrightSizePropertyId;
    private int _sphereTextureSpeedPropertyId;

    private void Awake()
    {
        if (effectTransform == null)
            effectTransform = transform;

        if (effectTransform == null)
            throw new System.InvalidOperationException("PowerReactiveEffectController requires an assigned effectTransform.");

        _baseLocalPosition = effectTransform.localPosition;
        if (pulseSpriteRenderer != null)
            _baseSpriteColor = pulseSpriteRenderer.color;

        if (waveVisualEffect != null)
        {
            if (string.IsNullOrWhiteSpace(waveRatePropertyName))
                throw new System.InvalidOperationException("PowerReactiveEffectController: waveRatePropertyName must be set when waveVisualEffect is assigned.");

            _waveRatePropertyId = Shader.PropertyToID(waveRatePropertyName.Trim());
            if (!waveVisualEffect.HasFloat(_waveRatePropertyId))
                throw new System.InvalidOperationException(
                    $"PowerReactiveEffectController: Visual Effect '{waveVisualEffect.name}' has no exposed float property '{waveRatePropertyName.Trim()}'. Check the VFX blackboard name matches exactly.");

            if (applyFlareBrightSize)
            {
                if (string.IsNullOrWhiteSpace(flareBrightSizePropertyName))
                    throw new System.InvalidOperationException("PowerReactiveEffectController: flareBrightSizePropertyName must be set when applyFlareBrightSize is enabled.");

                _flareBrightSizePropertyId = Shader.PropertyToID(flareBrightSizePropertyName.Trim());
                if (!waveVisualEffect.HasFloat(_flareBrightSizePropertyId))
                    throw new System.InvalidOperationException(
                        $"PowerReactiveEffectController: Visual Effect '{waveVisualEffect.name}' has no exposed float property '{flareBrightSizePropertyName.Trim()}'. Check the VFX blackboard name matches exactly.");
            }

            if (applySphereTextureSpeed)
            {
                if (string.IsNullOrWhiteSpace(sphereTextureSpeedPropertyName))
                    throw new System.InvalidOperationException("PowerReactiveEffectController: sphereTextureSpeedPropertyName must be set when applySphereTextureSpeed is enabled.");

                _sphereTextureSpeedPropertyId = Shader.PropertyToID(sphereTextureSpeedPropertyName.Trim());
                if (!waveVisualEffect.HasVector2(_sphereTextureSpeedPropertyId))
                    throw new System.InvalidOperationException(
                        $"PowerReactiveEffectController: Visual Effect '{waveVisualEffect.name}' has no exposed Vector2 property '{sphereTextureSpeedPropertyName.Trim()}'. Check the VFX blackboard name matches exactly.");
            }
        }

        if (powerToBlendCurve == null || powerToBlendCurve.length == 0)
            throw new System.InvalidOperationException("PowerReactiveEffectController: powerToBlendCurve must be assigned with at least one key.");

        if (flyCurve == null || flyCurve.length == 0)
            throw new System.InvalidOperationException("PowerReactiveEffectController: flyCurve must be assigned with at least one key.");

        ValidateRanges();
        _currentCombatPower = 0;
        _targetNormalizedPower = 0f;
        _displayedNormalizedPower = 0f;
    }

    private void OnEnable()
    {
        CombatEvents.OnPowerChanged += HandlePowerChanged;
        CombatEvents.OnPlayerTurnStarted += HandlePlayerTurnStarted;
    }

    private void OnDisable()
    {
        CombatEvents.OnPowerChanged -= HandlePowerChanged;
        CombatEvents.OnPlayerTurnStarted -= HandlePlayerTurnStarted;
    }

    /// <summary>Used by <see cref="CombatManager"/> after turn resolution (perfect-strike presentation already finished when applicable).</summary>
    public IEnumerator RunFlightToEnemyAndWait(EnemyController enemy)
    {
        if (enemy == null)
            yield break;

        if (_currentCombatPower <= 0)
            yield break;

        Transform anchor = enemy.GetPowerOrbHitAnchor();
        if (anchor == null)
            throw new System.InvalidOperationException("PowerReactiveEffectController: enemy returned no power-orb hit anchor.");

        _isFlyingToEnemy = true;
        Vector3 flightStartWorld = effectTransform.position;
        float duration = flyTime;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float u = Mathf.Clamp01(elapsed / duration);
            float eased = Mathf.Clamp01(flyCurve.Evaluate(u));
            Vector3 pos = Vector3.LerpUnclamped(flightStartWorld, anchor.position, eased);

            if (flightArcHeight != 0f && flightArcCurve != null && flightArcCurve.length > 0)
            {
                float arcMult = Mathf.Clamp01(flightArcCurve.Evaluate(u));
                pos.x += arcMult * flightArcHeight;
            }

            effectTransform.position = pos;
            yield return null;
        }

        effectTransform.position = anchor.position;
        _isFlyingToEnemy = false;
        SetUniformWorldScale(effectTransform, 0f);
        _postHitHiddenAtEnemy = true;
    }

    private void HandlePlayerTurnStarted()
    {
        _isFlyingToEnemy = false;
        _postHitHiddenAtEnemy = false;
        effectTransform.localPosition = _baseLocalPosition;
        _displayedNormalizedPower = _targetNormalizedPower;
    }

    private void Update()
    {
        if (_postHitHiddenAtEnemy)
        {
            SetUniformWorldScale(effectTransform, 0f);
            return;
        }

        if (_isFlyingToEnemy)
            return;

        _displayedNormalizedPower = StepDisplayedPowerTowardTarget(_displayedNormalizedPower, _targetNormalizedPower, baseScaleTransitionTime);

        float powerBlend = EvaluatePowerBlend(_displayedNormalizedPower);

        float pulseSpeed = Mathf.Lerp(scalePulseSpeedMin, scalePulseSpeedMax, powerBlend);
        float pulseT = Mathf.PingPong(Time.time * pulseSpeed, 1f);
        float pulseScale = Mathf.Lerp(minPulseScale, maxPulseScale, pulseT);

        float finalWorldScale = 0f;
        if (_currentCombatPower > 0)
        {
            float baseWorldScale = Mathf.Lerp(minScale, maxScale, _displayedNormalizedPower);
            finalWorldScale = baseWorldScale * pulseScale;
        }

        SetUniformWorldScale(effectTransform, finalWorldScale);

        float movementSpeed = Mathf.Lerp(movementSpeedMin, movementSpeedMax, powerBlend);
        float movementPhase = Time.time * movementSpeed;
        float xAmplitude = moveX * powerBlend;
        float yAmplitude = moveY * powerBlend;
        float xOffset = Mathf.Sin(movementPhase) * xAmplitude;
        float yOffset = Mathf.Cos(movementPhase) * yAmplitude;

        effectTransform.localPosition = _baseLocalPosition + new Vector3(xOffset, yOffset, 0f);
        ApplySpriteAlphaPulse(powerBlend);
        ApplyWaveRate(powerBlend);
        ApplyFlareBrightSize(powerBlend);
        ApplySphereTextureSpeed(powerBlend);
    }

    private void HandlePowerChanged(int currentPower, int maxPower)
    {
        if (maxPower <= 0)
        {
            _currentCombatPower = 0;
            _targetNormalizedPower = 0f;
            return;
        }

        _currentCombatPower = currentPower;

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

        if (waveRateMax < waveRateMin)
            throw new System.InvalidOperationException("PowerReactiveEffectController: waveRateMax must be >= waveRateMin.");

        if (applyFlareBrightSize)
        {
            if (waveVisualEffect == null)
                throw new System.InvalidOperationException("PowerReactiveEffectController: waveVisualEffect must be assigned when applyFlareBrightSize is enabled.");

            if (flareBrightSizeMax < flareBrightSizeMin)
                throw new System.InvalidOperationException("PowerReactiveEffectController: flareBrightSizeMax must be >= flareBrightSizeMin.");
        }

        if (applySphereTextureSpeed)
        {
            if (waveVisualEffect == null)
                throw new System.InvalidOperationException("PowerReactiveEffectController: waveVisualEffect must be assigned when applySphereTextureSpeed is enabled.");

            if (sphereTextureSpeedMax.x < sphereTextureSpeedMin.x || sphereTextureSpeedMax.y < sphereTextureSpeedMin.y)
                throw new System.InvalidOperationException("PowerReactiveEffectController: sphereTextureSpeedMax must be >= sphereTextureSpeedMin per component (x and y).");
        }

        if (flyTime <= 0f)
            throw new System.InvalidOperationException("PowerReactiveEffectController: flyTime must be > 0.");

        if (flightArcCurve == null || flightArcCurve.length == 0)
            throw new System.InvalidOperationException("PowerReactiveEffectController: flightArcCurve must be assigned with at least one key (or set Flight Arc Height to 0 to disable the arc).");
    }

    private void ApplySpriteAlphaPulse(float powerBlend)
    {
        if (pulseSpriteRenderer == null)
            return;

        float spritePulseSpeed = Mathf.Lerp(spritePulseSpeedMin, spritePulseSpeedMax, powerBlend);
        float spritePulseStrength = Mathf.Lerp(spritePulseStrengthMin, spritePulseStrengthMax, powerBlend);
        float alphaT = Mathf.PingPong(Time.time * spritePulseSpeed, 1f);
        float alpha = Mathf.Lerp(0f, spritePulseStrength, alphaT);

        Color c = _baseSpriteColor;
        c.a = alpha;
        pulseSpriteRenderer.color = c;
    }

    private void ApplyWaveRate(float powerBlend)
    {
        if (waveVisualEffect == null)
            return;

        float waveRate = Mathf.Lerp(waveRateMin, waveRateMax, powerBlend);
        waveVisualEffect.SetFloat(_waveRatePropertyId, waveRate);
    }

    private void ApplyFlareBrightSize(float powerBlend)
    {
        if (waveVisualEffect == null || !applyFlareBrightSize)
            return;

        float flareBrightSize = Mathf.Lerp(flareBrightSizeMin, flareBrightSizeMax, powerBlend);
        waveVisualEffect.SetFloat(_flareBrightSizePropertyId, flareBrightSize);
    }

    private void ApplySphereTextureSpeed(float powerBlend)
    {
        if (waveVisualEffect == null || !applySphereTextureSpeed)
            return;

        Vector2 sphereTextureSpeed = Vector2.Lerp(sphereTextureSpeedMin, sphereTextureSpeedMax, powerBlend);
        waveVisualEffect.SetVector2(_sphereTextureSpeedPropertyId, sphereTextureSpeed);
    }

    private float EvaluatePowerBlend(float displayedNormalizedPower)
    {
        float t = Mathf.Clamp01(displayedNormalizedPower);
        return Mathf.Clamp01(powerToBlendCurve.Evaluate(t));
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
