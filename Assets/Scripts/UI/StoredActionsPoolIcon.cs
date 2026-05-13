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

    private HoverTooltipTargetUI hoverTooltipTarget;
    private PoolRowKey configuredKey;
    private Vector3 _valueBgBaseScale = Vector3.one;
    private Coroutine _valueRevealCoroutine;
    private MonoBehaviour _valueRevealCoroutineRunner;
    private bool _jackpotPostMultiplyRevealInProgress;
    private bool _jackpotPostMultiplyValueTextApplied;
    private readonly Dictionary<Transform, bool> _defaultChildActiveStates = new Dictionary<Transform, bool>();

    public RectTransform FlyTargetRect => (RectTransform)transform;

    public PoolRowKey RowKey => configuredKey;

    /// <summary>True once this row's post-multiply value has been written to the amount text during the jackpot sequence.</summary>
    public bool JackpotPostMultiplyValueTextApplied => _jackpotPostMultiplyValueTextApplied;

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

    public void Configure(PoolRowKey key)
    {
        configuredKey = key;
        UpdateTooltipText();
    }

    /// <summary>Icon + amount for <see cref="DiceRollOutcomeFlyoutController"/> (uses +N for positive deltas).</summary>
    public void SetupForDiceRollFlyout(PoolRowKey key, Sprite iconSprite, int deltaAmount)
    {
        Configure(key);
        SetPoolSprite(iconSprite);
        SetRowBackground(GameIconCatalog.TryGetPoolRowBackground(key));
        if (valueText == null) return;
        valueText.text = deltaAmount > 0 ? $"+{deltaAmount}" : deltaAmount.ToString();
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
        if (jackpotMultiplierRoot != null)
            jackpotMultiplierRoot.SetActive(false);
        if (valueAmountBackgroundRoot != null)
            _valueBgBaseScale = valueAmountBackgroundRoot.localScale;

        var hoverTargetGo = icon != null ? icon.gameObject : gameObject;
        hoverTooltipTarget = hoverTargetGo.GetComponent<HoverTooltipTargetUI>() ?? hoverTargetGo.AddComponent<HoverTooltipTargetUI>();
    }

    private void OnDisable() => CancelJackpotValueReveal();

    public void ShowBustDestroyVisual(bool visible)
    {
        if (bustDestroyRoot == null)
            return;

        if (visible)
            DisableAllNonBustVisualChildren();
        bustDestroyRoot.SetActive(visible);
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
}
