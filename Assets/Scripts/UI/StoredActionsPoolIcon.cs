using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>One icon + amount for a deferred dice action row in <see cref="StoredActionsPoolDisplay"/>.</summary>
public class StoredActionsPoolIcon : MonoBehaviour
{
    [Tooltip("Optional. Behind the action/die icon; sprite comes from GameIconIndexSO per pool row.")]
    [SerializeField] private Image rowBackgroundImage;
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text valueText;

    [Header("Jackpot presentation (optional)")]
    [SerializeField] private GameObject jackpotMultiplierRoot;
    [SerializeField] private TMP_Text jackpotMultiplierText;

    [Tooltip("TMP format string; {0} is replaced by the jackpot multiply amount (integer).")]
    [SerializeField] private string jackpotMultiplierTextFormat = "×{0}";

    [Header("Bust presentation (optional)")]
    [Tooltip("Shown when this pooled row is destroyed during bust presentation.")]
    [SerializeField] private GameObject bustDestroyRoot;

    [Header("Jackpot value bump (optional)")]
    [Tooltip("Background behind the amount text — scaled up, then the multiplied value is shown, then scale returns.")]
    [SerializeField] private Transform valueAmountBackgroundRoot;

    [Tooltip("Uniform scale factor applied to the background at the peak of the pulse (e.g. 1.2).")]
    [SerializeField] private float jackpotValueRevealBgScaleMultiplier = 1.2f;

    [SerializeField] private float jackpotValueRevealScaleUpDuration = 0.1f;
    [SerializeField] private float jackpotValueRevealScaleDownDuration = 0.1f;

    [Header("Flyout value change pulse (optional)")]
    [Tooltip("Uniform scale peak when a die flyout amount changes (e.g. Increase Other Elements bonus on hit).")]
    [SerializeField] private float flyoutValueChangePulseScaleMultiplier = 1.2f;
    [SerializeField] private float flyoutValueChangePulseUpDuration = 0.08f;
    [SerializeField] private float flyoutValueChangePulseDownDuration = 0.12f;

    private HoverTooltipTargetUI hoverTooltipTarget;
    private PoolRowKey configuredKey;
    private Vector3 _valueBgBaseScale = Vector3.one;
    private Vector3 _valueTextBaseScale = Vector3.one;
    private Coroutine _valueRevealCoroutine;
    private Coroutine _flyoutValueChangePulseCoroutine;
    private MonoBehaviour _valueRevealCoroutineRunner;
    private bool _jackpotPostMultiplyRevealInProgress;
    private bool _jackpotPostMultiplyValueTextApplied;
    private readonly Dictionary<Transform, bool> _defaultChildActiveStates = new Dictionary<Transform, bool>();

    /// <summary>Every icon ever created and not yet destroyed (player pool, enemy pools, drag tokens, flyout rows). Maintained at Awake/OnDestroy so scene-wide presentations never search.</summary>
    private static readonly List<StoredActionsPoolIcon> Instances = new List<StoredActionsPoolIcon>();

    public bool HasBustDestroyRoot => bustDestroyRoot != null;

    public RectTransform FlyTargetRect => (RectTransform)transform;

    public PoolRowKey RowKey => configuredKey;

    /// <summary>True once this row's post-multiply value has been written to the amount text during the jackpot sequence.</summary>
    public bool JackpotPostMultiplyValueTextApplied => _jackpotPostMultiplyValueTextApplied;

    /// <summary>True when jackpot value reveal is already running or finished (e.g. drag tokens prepared before the jackpot sequence).</summary>
    public bool ShouldScheduleJackpotValueReveal => !_jackpotPostMultiplyRevealInProgress && !_jackpotPostMultiplyValueTextApplied;

    public bool IsJackpotValueRevealInProgress => _jackpotPostMultiplyRevealInProgress;

    public Sprite RowSprite => icon != null ? icon.sprite : null;

    public void SetPoolSprite(Sprite sprite)
    {
        if (icon == null) return;
        icon.sprite = sprite;
        icon.enabled = sprite != null;
    }

    public void SetRowBackground(Sprite sprite)
    {
        if (rowBackgroundImage == null) return;
        rowBackgroundImage.sprite = sprite;
        rowBackgroundImage.enabled = sprite != null;
    }

    public void SetValue(int value)
    {
        if (valueText == null) return;
        if (_jackpotPostMultiplyRevealInProgress) return;
        valueText.text = value.ToString();
    }

    /// <summary>Updates a die flyout row amount (+N format) and pulses the value presentation.</summary>
    public void SetFlyoutAmountWithPulse(int value)
    {
        if (valueText == null) return;
        if (_jackpotPostMultiplyRevealInProgress) return;
        valueText.text = value > 0 ? $"+{value}" : value.ToString();
        PlayFlyoutValueChangePulse();
    }

    /// <summary>Updates a displayed amount (plain number) and pulses — used by drag-to-assign tokens.</summary>
    public void SetAmountWithPulse(int value)
    {
        if (valueText == null) return;
        if (_jackpotPostMultiplyRevealInProgress) return;
        valueText.text = value.ToString();
        PlayFlyoutValueChangePulse();
    }

    /// <summary>When used as a draggable assignment token, turn off child raycasts so the token's drag surface receives pointer hits.</summary>
    public void SetPointerRaycastsEnabled(bool enabled)
    {
        if (rowBackgroundImage != null) rowBackgroundImage.raycastTarget = enabled;
        if (icon != null) icon.raycastTarget = enabled;
        if (valueText != null) valueText.raycastTarget = enabled;
    }

    public void Configure(PoolRowKey key)
    {
        configuredKey = key;
        UpdateTooltipText();
    }

    /// <summary>Icon + amount for <see cref="DiceRollOutcomeFlyoutController"/> (uses +N for positive deltas).</summary>
    public void SetupForDiceRollFlyout(PoolRowKey key, Sprite iconSprite, int deltaAmount, Sprite backgroundOverride = null)
    {
        Configure(key);
        SetPoolSprite(iconSprite);
        SetRowBackground(backgroundOverride != null ? backgroundOverride : GameIconCatalog.TryGetPoolRowBackground(key));
        if (valueText == null) return;
        valueText.text = deltaAmount > 0 ? $"+{deltaAmount}" : deltaAmount.ToString();
    }

    /// <summary>Icon + background only (no amount) for die-to-die reroll flyouts that vanish on arrival.</summary>
    public void SetupForDieToDieActionFlyout(PoolRowKey key, Sprite iconSprite, Sprite backgroundSprite)
    {
        Configure(key);
        SetPoolSprite(iconSprite);
        SetRowBackground(backgroundSprite);
        if (valueText != null)
            valueText.text = string.Empty;
    }

    public void ShowJackpotMultiplierBadge(int multiplier)
    {
        if (jackpotMultiplierRoot == null) return;
        if (jackpotMultiplierText != null)
        {
            var fmt = string.IsNullOrWhiteSpace(jackpotMultiplierTextFormat) ? "×{0}" : jackpotMultiplierTextFormat;
            jackpotMultiplierText.text = string.Format(fmt, multiplier);
        }

        jackpotMultiplierRoot.SetActive(true);
    }

    public void HideJackpotMultiplierBadge()
    {
        if (jackpotMultiplierRoot != null)
            jackpotMultiplierRoot.SetActive(false);
        if (bustDestroyRoot != null)
            bustDestroyRoot.SetActive(false);
        CaptureDefaultChildActiveStates();
    }

    /// <summary>
    /// After <paramref name="delayAfterJackpotStart"/> (from when the row's jackpot was shown), scales the value
    /// background up, sets the post-multiply amount, then scales the background back down. Uses unscaled time.
    /// </summary>
    /// <param name="coroutineRunner">
    /// Host for <see cref="MonoBehaviour.StartCoroutine"/> when this icon is not <see cref="GameObject.activeInHierarchy"/>
    /// (Unity cannot start coroutines on inactive objects). Pass the active presentation driver, e.g. <see cref="JackpotPresentationController"/>.
    /// </param>
    public void ScheduleJackpotPostMultiplyValueReveal(int newValue, float delayAfterJackpotStart, MonoBehaviour coroutineRunner = null)
    {
        var runner = coroutineRunner;
        if (runner == null || !runner.gameObject.activeInHierarchy)
        {
            if (gameObject.activeInHierarchy)
                runner = this;
            else
                runner = GetComponentInParent<StoredActionsPoolDisplay>();
        }

        if (runner == null || !runner.gameObject.activeInHierarchy)
        {
            Debug.LogError(
                $"StoredActionsPoolIcon on '{name}': cannot start jackpot value reveal — no active coroutine host (assign {nameof(coroutineRunner)} or activate this hierarchy). Applying value immediately.",
                this);
            _jackpotPostMultiplyRevealInProgress = false;
            _jackpotPostMultiplyValueTextApplied = true;
            SetValueUnchecked(newValue);
            return;
        }

        if (_valueRevealCoroutine != null && _valueRevealCoroutineRunner != null)
            _valueRevealCoroutineRunner.StopCoroutine(_valueRevealCoroutine);
        _jackpotPostMultiplyRevealInProgress = true;
        _jackpotPostMultiplyValueTextApplied = false;
        _valueRevealCoroutineRunner = runner;
        _valueRevealCoroutine = runner.StartCoroutine(CoJackpotPostMultiplyValueReveal(newValue, delayAfterJackpotStart));
    }

    public void CancelJackpotValueReveal()
    {
        _jackpotPostMultiplyRevealInProgress = false;
        _jackpotPostMultiplyValueTextApplied = false;
        if (_valueRevealCoroutine != null && _valueRevealCoroutineRunner != null)
        {
            _valueRevealCoroutineRunner.StopCoroutine(_valueRevealCoroutine);
            _valueRevealCoroutine = null;
            _valueRevealCoroutineRunner = null;
        }

        RestoreValueBackgroundScale();
    }

    private void Awake()
    {
        if (icon == null)
            Debug.LogError($"StoredActionsPoolIcon on '{gameObject.name}': icon Image is not assigned!");
        if (valueText == null)
            Debug.LogError($"StoredActionsPoolIcon on '{gameObject.name}': valueText is not assigned!");
        if (bustDestroyRoot == null)
            Debug.LogError($"StoredActionsPoolIcon on '{gameObject.name}': bustDestroyRoot is not assigned!", this);
        if (jackpotMultiplierRoot != null)
            jackpotMultiplierRoot.SetActive(false);
        if (valueAmountBackgroundRoot != null)
            _valueBgBaseScale = valueAmountBackgroundRoot.localScale;
        if (valueText != null)
            _valueTextBaseScale = valueText.transform.localScale;

        var hoverTargetGo = icon != null ? icon.gameObject : gameObject;
        hoverTooltipTarget = hoverTargetGo.GetComponent<HoverTooltipTargetUI>() ?? hoverTargetGo.AddComponent<HoverTooltipTargetUI>();

        if (!Instances.Contains(this))
            Instances.Add(this);
    }

    private void OnDestroy()
    {
        Instances.Remove(this);
    }

    private void OnDisable()
    {
        CancelJackpotValueReveal();
        CancelFlyoutValueChangePulse();
    }

    /// <summary>True when this active icon has a bust root and should participate in scene-wide Cast Overload presentation.</summary>
    public bool IsActiveBustTarget => HasBustDestroyRoot && gameObject.activeInHierarchy;

    public void ShowBustDestroyVisual(bool visible)
    {
        if (bustDestroyRoot == null)
            return;

        if (visible)
        {
            if (_defaultChildActiveStates.Count == 0)
                CaptureDefaultChildActiveStates();
            DisableAllNonBustVisualChildren();
            if (bustDestroyRoot.activeSelf)
                bustDestroyRoot.SetActive(false);
            bustDestroyRoot.SetActive(true);
            RestartBustChildEffects();
        }
        else
        {
            bustDestroyRoot.SetActive(false);
        }
    }

    private void RestartBustChildEffects()
    {
        var particleSystems = bustDestroyRoot.GetComponentsInChildren<ParticleSystem>(true);
        for (var i = 0; i < particleSystems.Length; i++)
        {
            var ps = particleSystems[i];
            if (ps == null)
                continue;
            ps.Clear(true);
            ps.Play(true);
        }
    }

    public void RestoreDefaultChildVisualStates()
    {
        foreach (var kvp in _defaultChildActiveStates)
        {
            if (kvp.Key == null) continue;
            kvp.Key.gameObject.SetActive(kvp.Value);
        }
    }

    public void ResetToIdleVisualState()
    {
        RestoreDefaultChildVisualStates();
        HideJackpotMultiplierBadge();
        if (bustDestroyRoot != null)
            bustDestroyRoot.SetActive(false);
    }

    private void CaptureDefaultChildActiveStates()
    {
        _defaultChildActiveStates.Clear();
        for (var i = 0; i < transform.childCount; i++)
        {
            var child = transform.GetChild(i);
            if (child == null) continue;
            _defaultChildActiveStates[child] = child.gameObject.activeSelf;
        }
    }

    private void DisableAllNonBustVisualChildren()
    {
        var bustTransform = bustDestroyRoot != null ? bustDestroyRoot.transform : null;
        for (var i = 0; i < transform.childCount; i++)
        {
            var child = transform.GetChild(i);
            if (child == null || child == bustTransform)
                continue;
            child.gameObject.SetActive(false);
        }
    }

    private IEnumerator CoJackpotPostMultiplyValueReveal(int newValue, float delayAfterJackpotStart)
    {
        if (delayAfterJackpotStart > 0f)
            yield return new WaitForSecondsRealtime(delayAfterJackpotStart);

        var bg = valueAmountBackgroundRoot;
        var peakMult = jackpotValueRevealBgScaleMultiplier;
        var up = Mathf.Max(0.0001f, jackpotValueRevealScaleUpDuration);
        var down = Mathf.Max(0.0001f, jackpotValueRevealScaleDownDuration);

        if (bg != null && peakMult > 1f + 1e-5f)
        {
            var peakScale = _valueBgBaseScale * peakMult;
            for (var t = 0f; t < up; t += Time.unscaledDeltaTime)
            {
                var u = Mathf.Clamp01(t / up);
                bg.localScale = Vector3.LerpUnclamped(_valueBgBaseScale, peakScale, u);
                yield return null;
            }

            bg.localScale = peakScale;
        }

        SetValueUnchecked(newValue);
        _jackpotPostMultiplyValueTextApplied = true;

        if (bg != null && peakMult > 1f + 1e-5f)
        {
            var from = bg.localScale;
            for (var t = 0f; t < down; t += Time.unscaledDeltaTime)
            {
                var u = Mathf.Clamp01(t / down);
                bg.localScale = Vector3.LerpUnclamped(from, _valueBgBaseScale, u);
                yield return null;
            }

            bg.localScale = _valueBgBaseScale;
        }

        _jackpotPostMultiplyRevealInProgress = false;
        _valueRevealCoroutine = null;
        _valueRevealCoroutineRunner = null;
    }

    private void SetValueUnchecked(int value)
    {
        if (valueText == null) return;
        valueText.text = value.ToString();
    }

    private void RestoreValueBackgroundScale()
    {
        if (valueAmountBackgroundRoot != null)
            valueAmountBackgroundRoot.localScale = _valueBgBaseScale;
    }

    private void PlayFlyoutValueChangePulse()
    {
        var pulseRoot = valueAmountBackgroundRoot != null ? valueAmountBackgroundRoot : valueText?.transform;
        if (pulseRoot == null)
            return;

        var peakMult = flyoutValueChangePulseScaleMultiplier;
        if (peakMult <= 1f + 1e-5f)
            return;

        if (_flyoutValueChangePulseCoroutine != null)
            StopCoroutine(_flyoutValueChangePulseCoroutine);
        _flyoutValueChangePulseCoroutine = StartCoroutine(CoFlyoutValueChangePulse(pulseRoot));
    }

    private void CancelFlyoutValueChangePulse()
    {
        if (_flyoutValueChangePulseCoroutine != null)
        {
            StopCoroutine(_flyoutValueChangePulseCoroutine);
            _flyoutValueChangePulseCoroutine = null;
        }

        RestoreValueBackgroundScale();
        if (valueText != null)
            valueText.transform.localScale = _valueTextBaseScale;
    }

    private IEnumerator CoFlyoutValueChangePulse(Transform pulseRoot)
    {
        var baseScale = pulseRoot == valueAmountBackgroundRoot ? _valueBgBaseScale : _valueTextBaseScale;
        var peakScale = baseScale * flyoutValueChangePulseScaleMultiplier;
        var up = Mathf.Max(0.0001f, flyoutValueChangePulseUpDuration);
        var down = Mathf.Max(0.0001f, flyoutValueChangePulseDownDuration);

        for (var t = 0f; t < up; t += Time.deltaTime)
        {
            if (pulseRoot == null)
                yield break;

            var u = Mathf.Clamp01(t / up);
            pulseRoot.localScale = Vector3.LerpUnclamped(baseScale, peakScale, u);
            yield return null;
        }

        if (pulseRoot != null)
            pulseRoot.localScale = peakScale;

        for (var t = 0f; t < down; t += Time.deltaTime)
        {
            if (pulseRoot == null)
                yield break;

            var u = Mathf.Clamp01(t / down);
            pulseRoot.localScale = Vector3.LerpUnclamped(peakScale, baseScale, u);
            yield return null;
        }

        if (pulseRoot != null)
            pulseRoot.localScale = baseScale;

        _flyoutValueChangePulseCoroutine = null;
    }

    private void UpdateTooltipText()
    {
        if (hoverTooltipTarget == null) return;
        var title = configuredKey.StableId.Length > 0 ? configuredKey.DisplayLabel : "Action";
        hoverTooltipTarget.SetContent(title, GetRowDescription(configuredKey));
    }

    private static string GetRowDescription(PoolRowKey key)
    {
        if (PoolRowKey.TryGetDieType(key, out var dt))
        {
            return dt switch
            {
                DieType.Damage => "Deferred damage from this face — applied when you end the turn.",
                DieType.Armor => "Deferred armor from this face — applied when you end the turn.",
                DieType.Fire => "Deferred fire from this face — resolves to a status when the turn ends (if configured on the action).",
                DieType.Ice => "Deferred ice from this face — resolves when the turn ends.",
                DieType.Nature => "Deferred nature from this face — resolves when the turn ends.",
                DieType.Curse => "Self-damage from this curse face — applied to you when you end the turn.",
                _ => "Deferred value from this face."
            };
        }

        if (string.Equals(key.StableId, "Heal", StringComparison.OrdinalIgnoreCase))
            return "Heal from a rolled face — restores HP when you end the turn.";

        return "Deferred action from a die — runs when you end the turn; may become a status effect.";
    }

    /// <summary>
    /// Every registered <see cref="StoredActionsPoolIcon"/> (player pool, enemy pools, drag tokens, flyout rows).
    /// Reads the creation-time registry directly — no scene search.
    /// </summary>
    public static List<StoredActionsPoolIcon> FindAllInLoadedScenes(bool includeInactive = true)
    {
        var list = new List<StoredActionsPoolIcon>(Instances.Count);
        for (var i = Instances.Count - 1; i >= 0; i--)
        {
            var icon = Instances[i];
            if (icon == null)
            {
                Instances.RemoveAt(i);
                continue;
            }

            if (!includeInactive && !icon.gameObject.activeInHierarchy)
                continue;
            list.Add(icon);
        }

        return list;
    }

    /// <summary>Active icons with a bust root, sorted top-to-bottom — every icon participates in Cast Overload, not just the player Element Container.</summary>
    public static List<StoredActionsPoolIcon> FindAllActiveBustTargetsTopToBottom()
    {
        var targets = new List<StoredActionsPoolIcon>(Instances.Count);
        for (var i = Instances.Count - 1; i >= 0; i--)
        {
            var icon = Instances[i];
            if (icon == null)
            {
                Instances.RemoveAt(i);
                continue;
            }

            if (icon.IsActiveBustTarget)
                targets.Add(icon);
        }

        SortTopToBottom(targets);
        return targets;
    }

    /// <summary>Sorts icons top-to-bottom using world Y, then sibling index.</summary>
    public static void SortTopToBottom(List<StoredActionsPoolIcon> icons)
    {
        if (icons == null)
            return;

        icons.Sort((a, b) =>
        {
            if (a == null && b == null) return 0;
            if (a == null) return 1;
            if (b == null) return -1;

            var ay = a.transform.position.y;
            var by = b.transform.position.y;
            var yCmp = by.CompareTo(ay);
            if (yCmp != 0)
                return yCmp;

            return a.transform.GetSiblingIndex().CompareTo(b.transform.GetSiblingIndex());
        });
    }

    public static void HideAllJackpotPresentationsInScene()
    {
        var icons = FindAllInLoadedScenes(true);
        for (var i = 0; i < icons.Count; i++)
            icons[i]?.HideJackpotMultiplierBadge();
    }

    public static void HideAllBustDestroyVisualsInScene()
    {
        var icons = FindAllInLoadedScenes(true);
        for (var i = 0; i < icons.Count; i++)
            icons[i]?.ShowBustDestroyVisual(false);
    }

    public static void RestoreAllDefaultChildVisualStatesInScene()
    {
        var icons = FindAllInLoadedScenes(true);
        for (var i = 0; i < icons.Count; i++)
            icons[i]?.RestoreDefaultChildVisualStates();
    }
}
