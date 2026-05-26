using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One trial cell for a rank panel. Assign complete/locked visuals; complete roots are toggled together when the trial is done.
/// Hover tooltip uses <see cref="HoverTooltipTargetUI"/> (no Button required) — needs a raycast target on <see cref="tooltipHoverArea"/>.
/// </summary>
public sealed class ProgressionTrialSlotUI : MonoBehaviour
{
    [SerializeField] private List<GameObject> completeVisualRoots = new List<GameObject>();
    [SerializeField] private Image completeIconImage;
    [SerializeField] private GameObject lockedVisualRoot;
    [SerializeField] private Image lockedIconImage;
    [Tooltip("Shows PlayerTrialSO.trialName (or asset name when empty).")]
    [SerializeField] private TMP_Text trialTitleText;
    [Tooltip("Fills from 0 to target as trial progress increases.")]
    [SerializeField] private Slider progressSlider;
    [Tooltip("Optional progress text while the trial is incomplete.")]
    [SerializeField] private TMP_Text progressText;
    [Tooltip("Object that receives hover (usually this slot root). Needs a Graphic with raycastTarget, or one is added.")]
    [SerializeField] private GameObject tooltipHoverArea;
    [SerializeField] private HoverTooltipTargetUI hoverTooltip;

    bool _hoverTargetsInitialized;

    public void Bind(PlayerTrialSO trial, TrialSaveData state)
    {
        if (trial == null)
        {
            Debug.LogError("ProgressionTrialSlotUI.Bind: trial is null.", this);
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);

        var icon = trial.trialIcon;
        ApplyIcon(completeIconImage, icon);
        ApplyIcon(lockedIconImage, icon);

        var completed = state.isCompleted;
        SetCompleteVisualsActive(completed);
        if (lockedVisualRoot != null)
            lockedVisualRoot.SetActive(!completed);

        if (trialTitleText != null)
            trialTitleText.text = GetTrialDisplayName(trial);

        UpdateProgressSlider(trial, state);

        if (progressText != null)
        {
            if (completed)
            {
                progressText.gameObject.SetActive(false);
            }
            else
            {
                progressText.gameObject.SetActive(true);
                var current = Mathf.Max(0, state.currentValue);
                progressText.text = $"{current}/{trial.targetValue}";
            }
        }

        ConfigureTooltip(trial, state);
    }

    void SetCompleteVisualsActive(bool active)
    {
        if (completeVisualRoots == null)
            return;

        for (var i = 0; i < completeVisualRoots.Count; i++)
        {
            var root = completeVisualRoots[i];
            if (root != null)
                root.SetActive(active);
        }
    }

    static string GetTrialDisplayName(PlayerTrialSO trial) => trial.DisplayName;

    void UpdateProgressSlider(PlayerTrialSO trial, TrialSaveData state)
    {
        if (progressSlider == null)
            return;

        progressSlider.interactable = false;

        var target = Mathf.Max(1, trial.targetValue);
        var current = state.isCompleted
            ? target
            : Mathf.Clamp(state.currentValue, 0, target);

        progressSlider.minValue = 0f;
        progressSlider.maxValue = target;
        progressSlider.wholeNumbers = true;
        progressSlider.value = current;
    }

    static void ApplyIcon(Image image, Sprite sprite)
    {
        if (image == null)
            return;

        image.sprite = sprite;
        image.enabled = sprite != null;
    }

    void ConfigureTooltip(PlayerTrialSO trial, TrialSaveData state)
    {
        EnsureHoverTooltipTargets();

        if (hoverTooltip == null)
            return;

        hoverTooltip.SetTrialTooltip(trial, state);
        hoverTooltip.SetIsAbove(true);
    }

    void EnsureHoverTooltipTargets()
    {
        if (_hoverTargetsInitialized)
            return;

        _hoverTargetsInitialized = true;

        var hoverGo = tooltipHoverArea != null ? tooltipHoverArea : gameObject;
        EnsureRaycastReceiver(hoverGo);

        hoverTooltip = hoverGo.GetComponent<HoverTooltipTargetUI>() ?? hoverGo.AddComponent<HoverTooltipTargetUI>();

        DisableDecorativeRaycasts(hoverGo.transform);
    }

    static void EnsureRaycastReceiver(GameObject hoverGo)
    {
        var graphic = hoverGo.GetComponent<Graphic>();
        if (graphic == null)
        {
            var image = hoverGo.AddComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0f);
            image.raycastTarget = true;
            return;
        }

        graphic.raycastTarget = true;
        if (graphic is Image img && img.sprite == null)
            img.color = new Color(img.color.r, img.color.g, img.color.b, Mathf.Max(img.color.a, 0.02f));
    }

    void DisableDecorativeRaycasts(Transform hoverRoot)
    {
        var graphics = GetComponentsInChildren<Graphic>(true);
        for (var i = 0; i < graphics.Length; i++)
        {
            var graphic = graphics[i];
            if (graphic == null || graphic.transform == hoverRoot)
                continue;

            graphic.raycastTarget = false;
        }
    }
}
