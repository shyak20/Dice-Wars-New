using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Generic die tooltip presenter (faces grid + gem sockets + face/gem/status hover texts).
/// Reusable across fight, shop, rewards, and other screens.
/// </summary>
public sealed class DieTooltipOverlayUI : MonoBehaviour
{
    /// <summary>Used when <see cref="dieTooltipFaceGrid"/> is not set in the inspector.</summary>
    public static readonly int[] DefaultFaceGridLayout = { -1, 0, -1, 1, 2, 3, -1, 4, -1, -1, 5 };

    [Header("Die Tooltip")]
    [SerializeField] private GameObject dieTooltipPanel;
    [SerializeField] private Transform dieTooltipSlotContainer;
    [SerializeField] private GameObject dieTooltipSlotPrefab;
    [Tooltip("Layout order for the face grid. -1 = spacer (empty cell). 0–5 = index into die.faces. Leave empty (size 0) to use the default layout.")]
    [SerializeField] private int[] dieTooltipFaceGrid;
    [Tooltip("If > 0, applies Fixed Column Count on the slot container’s GridLayoutGroup when a die is shown. Use 4 for the default 11-cell layout; 0 leaves the group as authored.")]
    [SerializeField, Min(0)] private int faceGridFixedColumnCount;
    [Tooltip("Expands the slot container RectTransform so all grid rows/columns fit (uses cell size, spacing, padding).")]
    [SerializeField] private bool resizeFaceGridContainerToFitCells = true;

    [Header("Gem slots (optional)")]
    [SerializeField] private Transform dieTooltipGemIconContainer;
    [SerializeField] private DieTooltipGemSlotView dieTooltipGemSlotPrefab;

    [Header("Face/Gem hover")]
    [SerializeField] private GameObject faceHoverTooltipPanel;
    [SerializeField] private TMP_Text faceHoverTitleText;
    [SerializeField] private TMP_Text faceHoverDescriptionText;

    [Header("Type backgrounds (optional)")]
    [Tooltip("Full die tooltip frame; sprite comes from DieAssetSO.uiTooltipBackground.")]
    [SerializeField] private Image dieTooltipTypeBackground;
    [Tooltip("Face hover frame from DieFaceSO.uiTooltipBackground; cleared for gem hover.")]
    [SerializeField] private Image faceHoverTypeBackground;

    [Header("Status hover (from face actions)")]
    [SerializeField] private GameObject statusHoverTooltipPanel;
    [SerializeField] private TMP_Text statusHoverTitleText;
    [SerializeField] private TMP_Text statusHoverDescriptionText;

    public DieAssetSO CurrentDie { get; private set; }

    private readonly Vector3[] _worldCornersScratch = new Vector3[4];

    /// <param name="horizontalCenterReference">When set (e.g. die icon <see cref="RectTransform"/>), moves the tooltip panel so its pivot’s world X matches this rect’s horizontal center.</param>
    /// <param name="onFaceClicked">When faces are interactable, receives the clicked slot’s <see cref="UIRewardSlot"/> for swap confirmation UI.</param>
    public void ShowDie(DieAssetSO die, bool facesInteractable, Action<int, DieFaceSO, UIRewardSlot> onFaceClicked = null, RectTransform horizontalCenterReference = null)
    {
        if (dieTooltipPanel == null || dieTooltipSlotContainer == null || dieTooltipSlotPrefab == null || die == null)
            return;

        CurrentDie = die;
        dieTooltipPanel.SetActive(true);
        DieTooltipBackgrounds.ApplyDieTooltip(dieTooltipTypeBackground, die);
        HideFaceHoverTooltip();
        HideStatusHoverTooltip();

        foreach (Transform child in dieTooltipSlotContainer)
            Destroy(child.gameObject);

        if (die.faces == null || die.faces.Length == 0) return;
        var grid = GetFaceGridIndices();
        for (var i = 0; i < grid.Length; i++)
        {
            var faceIndex = grid[i];
            if (faceIndex < 0 || faceIndex >= die.faces.Length)
            {
                CreateTooltipSpacer();
                continue;
            }

            var face = die.faces[faceIndex];
            if (face == null)
            {
                CreateTooltipSpacer();
                continue;
            }

            var go = Instantiate(dieTooltipSlotPrefab, dieTooltipSlotContainer);
            var slot = go.GetComponent<UIRewardSlot>();
            if (slot == null)
            {
                Debug.LogError("DieTooltipOverlayUI: dieTooltipSlotPrefab must include UIRewardSlot.", this);
                Destroy(go);
                continue;
            }

            if (facesInteractable && onFaceClicked != null)
                slot.Bind(face, _ => onFaceClicked.Invoke(faceIndex, face, slot));
            else
                slot.Bind(face, null);

            slot.SetInteractable(facesInteractable && onFaceClicked != null);
            slot.SetExternalStatusHoverTooltipEnabled(false);
            RegisterFaceHover(slot, face);
        }

        RefreshFaceGridContainerLayout(grid.Length);
        if (dieTooltipSlotContainer is RectTransform gridRt)
            LayoutRebuilder.ForceRebuildLayoutImmediate(gridRt);

        RebuildTooltipGemIcons(die);

        if (horizontalCenterReference != null)
            AlignDieTooltipPanelPivotWorldXToRect(horizontalCenterReference);
    }

    /// <summary>Moves the die tooltip panel in world space so pivot X matches the reference rect’s horizontal center (preserves Y/Z). Tooltip should use pivot x ≈ 0.5 for visual centering.</summary>
    public void AlignDieTooltipPanelPivotWorldXToRect(RectTransform reference)
    {
        var panelRt = dieTooltipPanel != null ? dieTooltipPanel.transform as RectTransform : null;
        if (panelRt == null || reference == null) return;

        reference.GetWorldCorners(_worldCornersScratch);
        var centerWorldX = (_worldCornersScratch[0].x + _worldCornersScratch[2].x) * 0.5f;

        var pos = panelRt.position;
        pos.x = centerWorldX;
        panelRt.position = pos;
    }

    public void Hide()
    {
        CurrentDie = null;
        if (dieTooltipPanel != null)
            dieTooltipPanel.SetActive(false);
        DieTooltipBackgrounds.Clear(dieTooltipTypeBackground);
        HideFaceHoverTooltip();
        HideStatusHoverTooltip();
    }

    private void RebuildTooltipGemIcons(DieAssetSO die)
    {
        if (dieTooltipGemIconContainer == null || dieTooltipGemSlotPrefab == null || die == null)
            return;

        foreach (Transform child in dieTooltipGemIconContainer)
            Destroy(child.gameObject);

        for (var i = 0; i < DieAssetSO.GemSocketCount; i++)
        {
            var gem = die.GetSocketedGemAt(i);
            var view = Instantiate(dieTooltipGemSlotPrefab, dieTooltipGemIconContainer);
            view.Bind(gem);
            view.transform.localScale = Vector3.one;
            RegisterGemHover(view, gem);
        }
    }

    private void CreateTooltipSpacer()
    {
        var spacer = new GameObject("Empty", typeof(RectTransform), typeof(LayoutElement));
        spacer.transform.SetParent(dieTooltipSlotContainer, false);

        var slotRect = dieTooltipSlotPrefab.transform as RectTransform;
        var spacerRect = spacer.transform as RectTransform;
        var layout = spacer.GetComponent<LayoutElement>();

        if (layout != null && slotRect != null)
        {
            var w = slotRect.rect.width > 1f ? slotRect.rect.width : slotRect.sizeDelta.x;
            var h = slotRect.rect.height > 1f ? slotRect.rect.height : slotRect.sizeDelta.y;
            if (w > 0f) layout.preferredWidth = w;
            if (h > 0f) layout.preferredHeight = h;
        }

        if (spacerRect != null)
            spacerRect.localScale = Vector3.one;
    }

    private void RegisterGemHover(DieTooltipGemSlotView slotView, GemSO gem)
    {
        if (slotView == null || gem == null) return;

        var go = slotView.GetHoverTarget();
        if (go == null) return;

        var et = go.GetComponent<EventTrigger>() ?? go.AddComponent<EventTrigger>();

        var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enter.callback.AddListener(_ => ShowGemHoverTooltip(gem));
        et.triggers.Add(enter);

        var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exit.callback.AddListener(_ => HideFaceHoverTooltip());
        et.triggers.Add(exit);
    }

    private void ShowGemHoverTooltip(GemSO gem)
    {
        if (faceHoverTooltipPanel == null) return;
        DieTooltipBackgrounds.Clear(faceHoverTypeBackground);
        if (faceHoverTitleText != null)
            faceHoverTitleText.text = gem != null ? gem.DisplayLabel : "";
        if (faceHoverDescriptionText != null)
            faceHoverDescriptionText.text = gem != null ? gem.description : "";
        faceHoverTooltipPanel.SetActive(true);
        HideStatusHoverTooltip();
    }

    private void RegisterFaceHover(UIRewardSlot slot, DieFaceSO face)
    {
        if (slot == null || face == null) return;

        var go = slot.GetHoverTarget();
        if (go == null) return;

        var et = go.GetComponent<EventTrigger>() ?? go.AddComponent<EventTrigger>();
        et.triggers.Clear();

        var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enter.callback.AddListener(_ => ShowFaceHoverTooltip(face));

        var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exit.callback.AddListener(_ =>
        {
            HideFaceHoverTooltip();
            HideStatusHoverTooltip();
        });

        slot.AppendHoverRevealListeners(enter, exit);
        et.triggers.Add(enter);
        et.triggers.Add(exit);
    }

    private void ShowFaceHoverTooltip(DieFaceSO face)
    {
        if (face != null)
            DieTooltipBackgrounds.ApplyFaceTooltip(faceHoverTypeBackground, face);
        else
            DieTooltipBackgrounds.Clear(faceHoverTypeBackground);

        if (faceHoverTooltipPanel != null)
        {
            if (faceHoverTitleText != null) faceHoverTitleText.text = face != null ? face.Title : "";
            if (faceHoverDescriptionText != null) faceHoverDescriptionText.text = face != null ? face.Description : "";
            faceHoverTooltipPanel.SetActive(true);
        }

        UIRewardSlot.BuildEffectTooltip(face, out var effectTitle, out var effectDescription);
        if (!string.IsNullOrWhiteSpace(effectTitle) || !string.IsNullOrWhiteSpace(effectDescription))
            ShowStatusHoverTooltip(effectTitle, effectDescription);
        else
            HideStatusHoverTooltip();
    }

    private void HideFaceHoverTooltip()
    {
        if (faceHoverTitleText != null) faceHoverTitleText.text = "";
        if (faceHoverDescriptionText != null) faceHoverDescriptionText.text = "";
        DieTooltipBackgrounds.Clear(faceHoverTypeBackground);
        if (faceHoverTooltipPanel != null) faceHoverTooltipPanel.SetActive(false);
    }

    private void ShowStatusHoverTooltip(string title, string description)
    {
        if (statusHoverTooltipPanel == null) return;

        if (statusHoverTitleText != null)
            statusHoverTitleText.text = title ?? string.Empty;
        if (statusHoverDescriptionText != null)
            statusHoverDescriptionText.text = description ?? string.Empty;

        statusHoverTooltipPanel.SetActive(true);
    }

    private void HideStatusHoverTooltip()
    {
        if (statusHoverTitleText != null) statusHoverTitleText.text = "";
        if (statusHoverDescriptionText != null) statusHoverDescriptionText.text = "";
        if (statusHoverTooltipPanel != null) statusHoverTooltipPanel.SetActive(false);
    }

    private int[] GetFaceGridIndices()
    {
        if (dieTooltipFaceGrid != null && dieTooltipFaceGrid.Length > 0)
            return dieTooltipFaceGrid;
        return DefaultFaceGridLayout;
    }

    private void RefreshFaceGridContainerLayout(int cellCount)
    {
        if (dieTooltipSlotContainer == null || cellCount <= 0) return;
        var glg = dieTooltipSlotContainer.GetComponent<GridLayoutGroup>();
        var rt = dieTooltipSlotContainer as RectTransform;
        if (glg == null || rt == null) return;

        if (faceGridFixedColumnCount > 0)
        {
            glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            glg.constraintCount = faceGridFixedColumnCount;
        }

        if (!resizeFaceGridContainerToFitCells)
            return;

        var cols = ComputeGridColumnCount(glg, cellCount);
        var rows = Mathf.Max(1, Mathf.CeilToInt(cellCount / (float)cols));
        var pad = glg.padding;
        var cellSize = glg.cellSize;
        var spacing = glg.spacing;
        var contentW = cols * cellSize.x + Mathf.Max(0, cols - 1) * spacing.x + pad.horizontal;
        var contentH = rows * cellSize.y + Mathf.Max(0, rows - 1) * spacing.y + pad.vertical;
        rt.sizeDelta = new Vector2(contentW, contentH);
    }

    private int ComputeGridColumnCount(GridLayoutGroup glg, int cellCount)
    {
        if (faceGridFixedColumnCount > 0)
            return Mathf.Max(1, faceGridFixedColumnCount);
        switch (glg.constraint)
        {
            case GridLayoutGroup.Constraint.FixedColumnCount:
                return Mathf.Max(1, glg.constraintCount);
            case GridLayoutGroup.Constraint.FixedRowCount:
                var rows = Mathf.Max(1, glg.constraintCount);
                return Mathf.Max(1, Mathf.CeilToInt(cellCount / (float)rows));
            default:
                return Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(cellCount)));
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (dieTooltipFaceGrid == null || dieTooltipFaceGrid.Length == 0) return;
        for (var i = 0; i < dieTooltipFaceGrid.Length; i++)
        {
            var v = dieTooltipFaceGrid[i];
            if (v < -1)
                Debug.LogWarning($"DieTooltipOverlayUI on '{name}': dieTooltipFaceGrid[{i}] = {v}; use -1 for spacer or non-negative face indices.", this);
        }
    }
#endif
}
