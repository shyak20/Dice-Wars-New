using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>Map-only shrine: permanent run max-power bonus or max-HP increase (% of current run max HP). Wire buttons in the Map scene.</summary>
public sealed class MapShrineChoicePanel : MonoBehaviour
{
    [SerializeField] private GameObject root;
    [SerializeField, Min(1)] private int maxPowerBonus = 2;
    [Tooltip("Max HP gained = floor(current run max HP × this percent ÷ 100), minimum 1. Also heals current HP by that amount.")]
    [SerializeField, Range(1, 100)] private int maxHpIncreasePercent = 25;
    [Header("Max HP choice label")]
    [Tooltip("Button or header TMP label updated when the panel opens. Use {0} for the calculated max-HP increase.")]
    [SerializeField] private TMP_Text maxHpChoiceLabel;
    [SerializeField, TextArea(1, 3)] private string maxHpChoiceTextFormat = "+{0} Max HP";
    [SerializeField] private Button maxPowerButton;
    [FormerlySerializedAs("healButton")]
    [SerializeField] private Button maxHpIncreaseButton;
    [SerializeField] private Button closeButton;

    private void Awake()
    {
        if (root == null)
            root = gameObject;
        root.SetActive(false);
        if (maxPowerButton != null)
            maxPowerButton.onClick.AddListener(OnMaxPowerChosen);
        if (maxHpIncreaseButton != null)
            maxHpIncreaseButton.onClick.AddListener(OnMaxHpIncreaseChosen);
        if (closeButton != null)
            closeButton.onClick.AddListener(Close);
    }

    /// <summary>Shows the shrine UI. Returns false if the run is not in a valid map state (tile should stay unconsumed).</summary>
    public bool TryOpenPanel()
    {
        if (RunManager.Instance == null)
        {
            Debug.LogError("MapShrineChoicePanel: RunManager missing.", this);
            return false;
        }

        if (!RunManager.Instance.UseMapBasedRun)
        {
            Debug.LogError("MapShrineChoicePanel: not in map-based run.", this);
            return false;
        }

        if (root == null)
            root = gameObject;

        RefreshMaxHpChoiceLabel();
        ActivateSelfAndAncestors(root.transform);
        root.SetActive(true);
        return true;
    }

    private void OnMaxPowerChosen()
    {
        RunManager.Instance?.ApplyShrineMaxPowerBonus(maxPowerBonus);
        Close();
    }

    private void OnMaxHpIncreaseChosen()
    {
        if (maxHpIncreasePercent > 0)
            RunManager.Instance?.ApplyShrineMaxHpIncreasePercent(maxHpIncreasePercent);
        Close();
    }

    void RefreshMaxHpChoiceLabel()
    {
        if (maxHpChoiceLabel == null)
            return;

        maxHpChoiceLabel.text = FormatMaxHpChoiceText(ComputeMaxHpIncrease());
    }

    int ComputeMaxHpIncrease()
    {
        if (RunManager.Instance == null || maxHpIncreasePercent <= 0)
            return 0;

        return RunManager.Instance.ComputeMaxHpIncreaseFromPercentOfRunMaxHp(maxHpIncreasePercent);
    }

    string FormatMaxHpChoiceText(int maxHpIncrease)
    {
        var format = maxHpChoiceTextFormat;
        if (string.IsNullOrWhiteSpace(format))
            return maxHpIncrease.ToString(CultureInfo.InvariantCulture);

        format = format.Trim();
        if (format.IndexOf("{0}", StringComparison.Ordinal) < 0)
            return format;

        return string.Format(CultureInfo.InvariantCulture, format, maxHpIncrease);
    }

    private void Close() => Hide();

    /// <summary>Closes the panel without applying a choice (e.g. map regenerated).</summary>
    public void Hide() => root.SetActive(false);

    private static void ActivateSelfAndAncestors(Transform t)
    {
        if (t == null) return;
        if (t.parent != null)
            ActivateSelfAndAncestors(t.parent);
        t.gameObject.SetActive(true);
    }
}
