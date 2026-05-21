using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One trial cell for a rank panel. Assign complete/locked roots each with an <see cref="Image"/> using <see cref="PlayerTrialSO.trialIcon"/>.
/// Hover tooltip uses <see cref="HoverTooltipTargetUI"/> (no Button required) — needs a raycast target on <see cref="tooltipHoverArea"/>.
/// </summary>
public sealed class ProgressionTrialSlotUI : MonoBehaviour
{
    [SerializeField] private GameObject completeVisualRoot;
    [SerializeField] private Image completeIconImage;
    [SerializeField] private GameObject lockedVisualRoot;
    [SerializeField] private Image lockedIconImage;
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
        if (completeVisualRoot != null)
            completeVisualRoot.SetActive(completed);
        if (lockedVisualRoot != null)
            lockedVisualRoot.SetActive(!completed);

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

        var title = string.IsNullOrWhiteSpace(trial.trialID) ? trial.name : trial.trialID;
        hoverTooltip.SetContent(title, ProgressionManager.BuildTrialTooltipBody(trial, state));
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
