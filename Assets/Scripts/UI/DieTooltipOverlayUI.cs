using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Generic die tooltip presenter (faces grid + gem sockets). Face/gem hover text goes through the shared
/// <see cref="HoverTooltipManager"/> via per-slot <see cref="HoverTooltipTargetUI"/>.
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

    [Header("Type backgrounds (optional)")]
    [Tooltip("Full die tooltip frame; sprite comes from DieAssetSO.uiTooltipBackground.")]
    [SerializeField] private Image dieTooltipTypeBackground;

    [Header("Face replacement rules")]
    [Tooltip("Shown when the player picks a face slot that would exceed the act's max same-value faces cap.")]
    [SerializeField] private GameObject faceReplacementRuleErrorObject;
    [Tooltip("Message shown on the error object. Use {0} where the act’s max same numeric value faces per die should appear (see MapActDefinitionSO.maxSameNumericValueFacesPerDie).")]
    [SerializeField] private TMP_Text faceReplacementRuleErrorText;
    [Tooltip("If set, this rect is shaken; otherwise the error GameObject’s RectTransform is used.")]
    [SerializeField] private RectTransform faceReplacementRuleErrorShakeTarget;
    [SerializeField, Min(0.05f)] private float replacementErrorShakeDuration = 0.32f;
    [SerializeField, Min(0f)] private float replacementErrorShakeMaxHorizontalOffset = 14f;
    [SerializeField, Min(0f)] private float replacementErrorShakeMaxVerticalOffset = 6f;
    [SerializeField, Min(1f)] private float replacementErrorShakeWaves = 10f;

    public DieAssetSO CurrentDie { get; private set; }

    private readonly List<UIRewardSlot> _faceSlots = new();
    private readonly Dictionary<int, UIRewardSlot> _faceSlotsByFaceIndex = new();
    private readonly Vector3[] _worldCornersScratch = new Vector3[4];
    private Coroutine _replacementErrorShakeRoutine;
    private RectTransform _replacementErrorShakeRect;
    private Vector2 _replacementErrorShakeBaseAnchoredPosition;
    private string _faceReplacementRuleErrorTextTemplate;

    void Awake()
    {
        if (faceReplacementRuleErrorText != null)
            _faceReplacementRuleErrorTextTemplate = faceReplacementRuleErrorText.text;

        if (faceReplacementRuleErrorObject == null)
            return;
        var rt = faceReplacementRuleErrorShakeTarget != null
            ? faceReplacementRuleErrorShakeTarget
            : faceReplacementRuleErrorObject.transform as RectTransform;
        if (rt != null)
            _replacementErrorShakeBaseAnchoredPosition = rt.anchoredPosition;
    }

    void OnDisable() => StopReplacementErrorShake(resetPosition: true);

    /// <param name="horizontalCenterReference">When set (e.g. die icon <see cref="RectTransform"/>), moves the tooltip panel so its pivot’s world X matches this rect’s horizontal center.</param>
    /// <param name="onFaceClicked">When faces are interactable, receives the clicked slot’s <see cref="UIRewardSlot"/> for swap confirmation UI.</param>
    /// <param name="replacementSlotAllowed">When set, face slots call this before <paramref name="onFaceClicked"/>; if false, the click is ignored and <see cref="ShowFaceReplacementRuleError"/> runs.</param>
    public void ShowDie(DieAssetSO die, bool facesInteractable, Action<int, DieFaceSO, UIRewardSlot> onFaceClicked = null, RectTransform horizontalCenterReference = null, Func<int, bool> replacementSlotAllowed = null)
    {
        if (dieTooltipPanel == null || dieTooltipSlotContainer == null || dieTooltipSlotPrefab == null || die == null)
            return;

        // Hover preview (facesInteractable=false) may already be showing this die; clicking the die
        // must rebuild with clickable face slots — only skip duplicate non-interactive refreshes.
        if (CurrentDie == die && dieTooltipPanel.activeSelf && !facesInteractable)
            return;

        CurrentDie = die;
        dieTooltipPanel.SetActive(true);
        if (dieTooltipSlotContainer != null)
            dieTooltipSlotContainer.gameObject.SetActive(true);
        if (dieTooltipGemIconContainer != null)
            dieTooltipGemIconContainer.gameObject.SetActive(true);
        SetDecorativeRaycastBlocking(false);
        DieTooltipBackgrounds.ApplyDieTooltip(dieTooltipTypeBackground, die);
        HoverTooltipManager.HideAllTooltipPanels();
        HideFaceReplacementRuleError();
        _faceSlots.Clear();
        _faceSlotsByFaceIndex.Clear();

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
            {
                var capturedIndex = faceIndex;
                slot.Bind(face, _ =>
                {
                    if (replacementSlotAllowed != null && !replacementSlotAllowed.Invoke(capturedIndex))
                    {
                        ShowFaceReplacementRuleError();
                        return;
                    }

                    onFaceClicked.Invoke(capturedIndex, face, slot);
                });
            }
            else
                slot.Bind(face, null);

            slot.SetInteractable(facesInteractable && onFaceClicked != null);
            slot.EnsureStandaloneHoverReveal();
            slot.SetFaceTooltipIncludesHeader(true);
            _faceSlots.Add(slot);
            _faceSlotsByFaceIndex[faceIndex] = slot;
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

    public bool TryGetFaceSlot(int faceIndex, out UIRewardSlot slot) =>
        _faceSlotsByFaceIndex.TryGetValue(faceIndex, out slot);

    /// <summary>During face-swap resolve, disables slot clicks while the replace animation plays on the tooltip.</summary>
    public void SetAllFaceSlotsInteractable(bool interactable)
    {
        HoverTooltipManager.HideAllTooltipPanels();
        for (var i = 0; i < _faceSlots.Count; i++)
        {
            var slot = _faceSlots[i];
            if (slot == null)
                continue;
            slot.SetInteractable(interactable);
            slot.SetHoverRevealEnabled(interactable);
        }
    }

    /// <summary>
    /// Disables raycasts on decorative tooltip graphics so the panel does not steal hover from dice underneath.
    /// Face/gem slot graphics are left unchanged so nested hover tooltips still work.
    /// </summary>
    public void SetDecorativeRaycastBlocking(bool blocks)
    {
        if (dieTooltipPanel == null)
            return;

        foreach (var graphic in dieTooltipPanel.GetComponentsInChildren<Graphic>(true))
        {
            if (IsDescendantOf(graphic.transform, dieTooltipSlotContainer))
                continue;
            if (IsDescendantOf(graphic.transform, dieTooltipGemIconContainer))
                continue;

            graphic.raycastTarget = blocks;
        }
    }

    static bool IsDescendantOf(Transform transform, Transform ancestor)
    {
        if (transform == null || ancestor == null)
            return false;

        return transform == ancestor || transform.IsChildOf(ancestor);
    }

    public void Hide()
    {
        CurrentDie = null;
        _faceSlots.Clear();
        _faceSlotsByFaceIndex.Clear();
        if (dieTooltipPanel != null)
            dieTooltipPanel.SetActive(false);
        DieTooltipBackgrounds.Clear(dieTooltipTypeBackground);
        HoverTooltipManager.HideAllTooltipPanels();
        HideFaceReplacementRuleError();
    }

    /// <summary>True when the pointer is over the active die tooltip panel (used to avoid hover flicker on parent dice).</summary>
    public bool IsPointerOverDieTooltipPanel()
    {
        if (dieTooltipPanel == null || !dieTooltipPanel.activeSelf || EventSystem.current == null)
            return false;

        var panelTransform = dieTooltipPanel.transform;
        var ped = new PointerEventData(EventSystem.current) { position = Input.mousePosition };
        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(ped, results);
        for (var i = 0; i < results.Count; i++)
        {
            var hit = results[i].gameObject.transform;
            if (hit == panelTransform || hit.IsChildOf(panelTransform))
                return true;
        }

        return false;
    }

    public void ShowFaceReplacementRuleError()
    {
        if (faceReplacementRuleErrorObject == null)
            return;

        EnsureFaceHoverHostVisible();
        ApplyFaceReplacementRuleErrorText();

        EnsureReplacementErrorShakeRect();
        StopReplacementErrorShake(resetPosition: true);
        faceReplacementRuleErrorObject.SetActive(true);

        if (_replacementErrorShakeRect == null)
            return;

        _replacementErrorShakeBaseAnchoredPosition = _replacementErrorShakeRect.anchoredPosition;
        _replacementErrorShakeRoutine = StartCoroutine(CoShakeReplacementError());
    }

    public void HideFaceReplacementRuleError()
    {
        StopReplacementErrorShake(resetPosition: true);
        if (faceReplacementRuleErrorObject != null)
            faceReplacementRuleErrorObject.SetActive(false);
    }

    void ApplyFaceReplacementRuleErrorText()
    {
        if (faceReplacementRuleErrorText == null)
            return;

        var template = _faceReplacementRuleErrorTextTemplate;
        if (string.IsNullOrEmpty(template))
            template = faceReplacementRuleErrorText.text;
        if (string.IsNullOrEmpty(template))
            return;

        var cap = SameValueFaceCapUtility.GetMaxSameNumericValueFacesPerDie();
        var displayCap = cap >= int.MaxValue / 2 ? 0 : cap;

        if (template.IndexOf("{0}", StringComparison.Ordinal) >= 0)
            faceReplacementRuleErrorText.text = string.Format(CultureInfo.InvariantCulture, template, displayCap);
        else
            faceReplacementRuleErrorText.text = template;
    }

    void EnsureReplacementErrorShakeRect()
    {
        _replacementErrorShakeRect = faceReplacementRuleErrorShakeTarget;
        if (_replacementErrorShakeRect == null && faceReplacementRuleErrorObject != null)
            _replacementErrorShakeRect = faceReplacementRuleErrorObject.transform as RectTransform;
    }

    void StopReplacementErrorShake(bool resetPosition)
    {
        if (_replacementErrorShakeRoutine != null)
        {
            StopCoroutine(_replacementErrorShakeRoutine);
            _replacementErrorShakeRoutine = null;
        }

        if (resetPosition && _replacementErrorShakeRect != null)
            _replacementErrorShakeRect.anchoredPosition = _replacementErrorShakeBaseAnchoredPosition;
    }

    IEnumerator CoShakeReplacementError()
    {
        var rt = _replacementErrorShakeRect;
        if (rt == null)
            yield break;

        var basePos = _replacementErrorShakeBaseAnchoredPosition;
        var duration = Mathf.Max(0.05f, replacementErrorShakeDuration);
        var t = 0f;

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            var u = Mathf.Clamp01(t / duration);
            var decay = 1f - u;
            var angle = u * Mathf.PI * replacementErrorShakeWaves;
            var ox = Mathf.Sin(angle) * replacementErrorShakeMaxHorizontalOffset * decay;
            var oy = Mathf.Sin(angle * 1.13f) * replacementErrorShakeMaxVerticalOffset * decay;
            rt.anchoredPosition = basePos + new Vector2(ox, oy);
            yield return null;
        }

        rt.anchoredPosition = basePos;
        _replacementErrorShakeRoutine = null;
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

        var target = go.GetComponent<HoverTooltipTargetUI>() ?? go.AddComponent<HoverTooltipTargetUI>();
        target.SetScriptableSource(gem);
    }

    void EnsureFaceHoverHostVisible()
    {
        if (dieTooltipPanel == null)
            return;

        dieTooltipPanel.SetActive(true);

        var showDieGrid = CurrentDie != null;
        if (dieTooltipSlotContainer != null)
            dieTooltipSlotContainer.gameObject.SetActive(showDieGrid);
        if (dieTooltipGemIconContainer != null)
            dieTooltipGemIconContainer.gameObject.SetActive(showDieGrid);

        if (!showDieGrid)
            SetDecorativeRaycastBlocking(false);
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
