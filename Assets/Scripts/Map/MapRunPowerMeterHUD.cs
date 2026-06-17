using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Map scene: shows max power budget for the next combat (same calculation as the fight UI), as <c>0 / max</c> on the meter.
/// Subscribes to run relics, shrine max-power bonus, and deck changes.
/// </summary>
public sealed class MapRunPowerMeterHUD : MonoBehaviour
{
    [SerializeField] private Slider powerSlider;
    [SerializeField] private TMP_Text powerText;
    [Tooltip("Matches combat HUD: {0} = current (always 0 on map), {1} = max.")]
    [SerializeField] private string powerTextFormat = "{0} / {1}";

    private void Awake()
    {
        if (powerSlider == null && powerText == null)
            Debug.LogError(
                $"{nameof(MapRunPowerMeterHUD)} on '{name}': assign at least {nameof(powerSlider)} or {nameof(powerText)}.",
                this);
    }

    private void OnEnable()
    {
        if (RunManager.Instance != null)
        {
            RunManager.Instance.OnRunRelicsChanged += Refresh;
            RunManager.Instance.OnRunMaxPowerBudgetChanged += Refresh;
        }

        PlayerDataContainer.OnRuntimeDeckChanged += Refresh;
    }

    private void OnDisable()
    {
        if (RunManager.Instance != null)
        {
            RunManager.Instance.OnRunRelicsChanged -= Refresh;
            RunManager.Instance.OnRunMaxPowerBudgetChanged -= Refresh;
        }

        PlayerDataContainer.OnRuntimeDeckChanged -= Refresh;
    }

    private void Start() => Refresh();

    private void Refresh()
    {
        var data = PlayerDataContainer.Instance != null ? PlayerDataContainer.Instance.RuntimeData : null;
        var max = PlayerMaxPowerForRun.Compute(data);

        const int current = 0;
        if (powerSlider != null)
        {
            powerSlider.minValue = 0f;
            powerSlider.maxValue = Mathf.Max(1f, max);
            powerSlider.value = current;
        }

        if (powerText != null)
            powerText.text = string.Format(powerTextFormat, current, max);
    }
}
