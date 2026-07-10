using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Entry point for hover text using <see cref="HoverTooltipPanelUI"/>. Multiple instances may exist across additively loaded scenes;
/// <see cref="Instance"/> resolves to an enabled manager in <see cref="SceneManager.GetActiveScene"/>. Duplicate components are not destroyed.
/// </summary>
[DisallowMultipleComponent]
public sealed class HoverTooltipManager : MonoBehaviour
{
    static readonly List<HoverTooltipManager> Registry = new List<HoverTooltipManager>();
    static ProgressionRewardVisualCatalogSO _sharedProgressionRewardVisualCatalog;

    /// <summary>Last assigned catalog from any enabled manager (e.g. map scene while fight loads additively).</summary>
    public static ProgressionRewardVisualCatalogSO SharedProgressionRewardVisualCatalog =>
        _sharedProgressionRewardVisualCatalog;

    /// <summary>
    /// Enabled manager in the <see cref="SceneManager.GetActiveScene"/> with a valid prefab, or null if none qualifies.
    /// </summary>
    public static HoverTooltipManager Instance
    {
        get
        {
            CleanupDestroyedEntries();
            var activeScene = SceneManager.GetActiveScene();
            for (var i = 0; i < Registry.Count; i++)
            {
                var m = Registry[i];
                if (m == null)
                    continue;
                if (!m.isActiveAndEnabled)
                    continue;
                if (m.gameObject.scene != activeScene)
                    continue;
                if (m.panelPrefab == null && m.trialRewardsPanelPrefab == null)
                    continue;
                return m;
            }

            return null;
        }
    }

    [Tooltip("Prefab whose root has HoverTooltipPanelUI (same layout as legacy scene instances).")]
    [SerializeField] private HoverTooltipPanelUI panelPrefab;

    [Tooltip("Prefab whose root has HoverTrialRewardsTooltipPanelUI (trial slots on Dice Select).")]
    [SerializeField] private HoverTrialRewardsTooltipPanelUI trialRewardsPanelPrefab;

    [Tooltip("Icons for trial reward rows. Uses GameIconCatalog.Active when unset.")]
    [SerializeField] private GameIconIndexSO gameIconIndex;

    [Tooltip("Shared progression reward visuals for trial tooltips and celebration popups.")]
    [SerializeField] private ProgressionRewardVisualCatalogSO progressionRewardVisualCatalog;

    public ProgressionRewardVisualCatalogSO ProgressionRewardVisualCatalog => ResolveProgressionRewardVisualCatalog();

    [Tooltip("When the hovered UI has no Canvas in parents (rare), parent the tooltip here.")]
    [SerializeField] private Canvas fallbackCanvas;

    [Tooltip("Added to every show call’s anchor-local offset (canvas units; scales with Canvas Scaler).")]
    [SerializeField] private Vector2 hoverTooltipScreenOffset;

    [Tooltip("Added to trial rewards tooltip show calls only (replaces Hover Tooltip Screen Offset for those tooltips).")]
    [SerializeField] private Vector2 trialRewardsTooltipScreenOffset;

    [Tooltip("Anchor-local offset when the trigger passes isAbove=true (replaces the caller offset, not added to it). Still adds Hover Tooltip Screen Offset.")]
    [SerializeField] private Vector2 hoverAboveTooltipScreenOffset;

    [Header("Placement & sorting")]
    [Tooltip("Sorting order of the runtime tooltip canvases. Keep above every scene canvas so tooltips always render front-most.")]
    [SerializeField] private int tooltipSortingOrder = 10000;

    [Tooltip("Gap (canvas units) between the hovered element and the closest tooltip stacked on it (first tooltip of the face cluster, or the explain tooltip under face action options).")]
    [SerializeField, Min(0f)] private float anchorTooltipGap = 8f;

    [Tooltip("Gap (canvas units) between stacked tooltips (main tooltip to status tooltip, and status to status).")]
    [SerializeField, Min(0f)] private float secondaryTooltipGap = 8f;

    [Tooltip("Padding from screen edges (pixels) when shifting tooltips left/right to stay on screen.")]
    [SerializeField, Min(0f)] private float screenEdgePadding = 16f;

    HoverTooltipPanelUI _panel;
    readonly List<HoverTooltipPanelUI> _secondaryPanels = new List<HoverTooltipPanelUI>();
    HoverTrialRewardsTooltipPanelUI _trialRewardsPanel;
    Canvas _panelParentCanvas;
    readonly List<TooltipContent> _secondaryEntriesScratch = new List<TooltipContent>();

    /// <summary>True when a prefab is assigned so <see cref="HoverTooltipTargetUI"/> can present tooltips.</summary>
    public bool HasValidPrefab => panelPrefab != null;

    public bool HasValidTrialRewardsPrefab => trialRewardsPanelPrefab != null;

    void Awake()
    {
        if (progressionRewardVisualCatalog != null)
            _sharedProgressionRewardVisualCatalog = progressionRewardVisualCatalog;

        if (panelPrefab == null)
            Debug.LogError($"HoverTooltipManager on '{name}': assign panelPrefab (HoverTooltipPanelUI prefab).", this);
        if (trialRewardsPanelPrefab != null && ResolveProgressionRewardVisualCatalog() == null)
            Debug.LogError(
                $"HoverTooltipManager on '{name}': assign progressionRewardVisualCatalog when trialRewardsPanelPrefab is set.",
                this);
    }

    ProgressionRewardVisualCatalogSO ResolveProgressionRewardVisualCatalog() =>
        progressionRewardVisualCatalog != null
            ? progressionRewardVisualCatalog
            : _sharedProgressionRewardVisualCatalog;

    void OnEnable() => RegisterSelf();

    void OnDisable()
    {
        UnregisterSelf();
        DestroyPanelIfOwned();
    }

    void OnDestroy()
    {
        UnregisterSelf();
        DestroyPanelIfOwned();
    }

    void RegisterSelf()
    {
        if (!Registry.Contains(this))
            Registry.Add(this);
    }

    void UnregisterSelf()
    {
        Registry.Remove(this);
    }

    static void CleanupDestroyedEntries()
    {
        for (var i = Registry.Count - 1; i >= 0; i--)
        {
            if (Registry[i] == null)
                Registry.RemoveAt(i);
        }
    }

    /// <summary>Hides the runtime panel on every registered manager (safe when scene switches or pointer leaves).</summary>
    public static void HideAllTooltipPanels()
    {
        CleanupDestroyedEntries();
        for (var i = 0; i < Registry.Count; i++)
        {
            var m = Registry[i];
            if (m != null)
                m.Hide();
        }
    }

    void DestroyPanelIfOwned()
    {
        if (_panel != null)
        {
            Destroy(_panel.gameObject);
            _panel = null;
        }

        for (var i = 0; i < _secondaryPanels.Count; i++)
        {
            if (_secondaryPanels[i] != null)
                Destroy(_secondaryPanels[i].gameObject);
        }
        _secondaryPanels.Clear();

        if (_trialRewardsPanel != null)
        {
            Destroy(_trialRewardsPanel.gameObject);
            _trialRewardsPanel = null;
        }

        _panelParentCanvas = null;
    }

    /// <summary>
    /// Show using data resolved from <paramref name="source"/> via <see cref="TooltipContentResolver"/>
    /// (main content plus stacked secondary status/effect explanations).
    /// </summary>
    public void ShowForScriptableObject(
        RectTransform anchor,
        Vector2 screenPixelOffset,
        ScriptableObject source,
        bool isAbove = false,
        bool includeFaceHeader = false)
    {
        _secondaryEntriesScratch.Clear();
        if (!TooltipContentResolver.TryResolve(source, out var main, _secondaryEntriesScratch, includeFaceHeader))
            return;
        if (main.IsEmpty && _secondaryEntriesScratch.Count == 0)
            return;

        // When the face header is shown, the face tooltip sits directly above the hovered face and the
        // status/effect explanations stack upward on top of it (face at the bottom, statuses above).
        Show(anchor, screenPixelOffset, main.Title, main.Description, main.Background, isAbove, _secondaryEntriesScratch,
            faceHeaderLayout: includeFaceHeader);
    }

    /// <summary>
    /// Shows the shared panel aligned to <paramref name="anchor"/>. Secondary status explanations are derived
    /// from status style tags in the copy (see <see cref="StatusStyleTagScanner"/>).
    /// When <paramref name="isAbove"/> is true, uses <see cref="hoverAboveTooltipScreenOffset"/> instead of <paramref name="screenPixelOffset"/>.
    /// Offsets are in the tooltip parent canvas's local space (reference-resolution units), not raw screen pixels.
    /// </summary>
    public void Show(
        RectTransform anchor,
        Vector2 screenPixelOffset,
        string title,
        string description,
        Sprite tooltipBackground = null,
        bool isAbove = false)
    {
        _secondaryEntriesScratch.Clear();
        TooltipContentResolver.AppendSecondaryForText(title, description, _secondaryEntriesScratch);
        Show(anchor, screenPixelOffset, title, description, tooltipBackground, isAbove, _secondaryEntriesScratch);
    }

    /// <summary>
    /// Shows the shared panel aligned to <paramref name="anchor"/> plus one stacked panel per secondary entry.
    /// Position is set once per call (triggers show on pointer-enter and never follow the anchor afterward).
    /// When <paramref name="faceHeaderLayout"/> is true the main tooltip is placed directly above the hovered
    /// element and the secondary panels stack upward on top of it. All panels render on a front-most
    /// override-sorting canvas, stay in front of 3D geometry, and are shifted left/right to stay on screen.
    /// </summary>
    public void Show(
        RectTransform anchor,
        Vector2 screenPixelOffset,
        string title,
        string description,
        Sprite tooltipBackground,
        bool isAbove,
        IReadOnlyList<TooltipContent> secondaryEntries,
        bool faceHeaderLayout = false)
    {
        if (panelPrefab == null || anchor == null)
            return;

        var targetCanvas = ResolveCanvas(anchor);
        if (targetCanvas == null)
            return;

        _trialRewardsPanel?.Hide();
        EnsurePanelUnderCanvas(targetCanvas);

        // Some sources (e.g. face-picker cards that already show name/description) suppress the main tooltip
        // and only present the secondary status/effect explanation, positioned relative to the hovered element.
        var mainSuppressed = string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(description);

        if (faceHeaderLayout && !mainSuppressed)
        {
            // Face tooltip on top of the cluster: statuses stack just above the hovered face (in list order,
            // top-to-bottom), then the face tooltip is placed above all of them. The gap between the hovered
            // face and the closest tooltip is anchorTooltipGap; gaps within the stack use secondaryTooltipGap.
            var topStatus = ShowSecondaryPanels(targetCanvas, secondaryEntries, anchor, stackAbove: true,
                gapFromReference: anchorTooltipGap, reverseOrder: true);
            var faceReference = topStatus != null ? topStatus : anchor;
            var faceGap = topStatus != null ? secondaryTooltipGap : anchorTooltipGap;
            PlacePanelAbove(_panel, faceReference, faceGap, tooltipSortingOrder + 1 + (secondaryEntries?.Count ?? 0),
                title, description, tooltipBackground);
            return;
        }

        RectTransform stackReference;
        bool stackSecondaryAbove;
        float stackGapFromReference;
        if (mainSuppressed)
        {
            _panel.Hide();
            stackReference = anchor;
            stackGapFromReference = anchorTooltipGap;
            // No main tooltip: place the explanation on whichever side keeps it on screen (under the element by default).
            stackSecondaryAbove = HoverTooltipLayoutUtility.GetRectScreenCenterY(anchor) < Screen.height * 0.5f;
        }
        else
        {
            _panel.Show(title, description, tooltipBackground);
            ApplyFrontMostSorting(_panel.PanelRect, tooltipSortingOrder);
            HoverTooltipLayoutUtility.ForceRebuildLayout(_panel.PanelRect);
            Canvas.ForceUpdateCanvases();
            _panel.AlignToRectWithScreenOffset(anchor, ResolveScreenOffset(screenPixelOffset, isAbove));
            HoverTooltipLayoutUtility.ClampRectInsideScreenHorizontally(_panel.PanelRect, screenEdgePadding);
            stackReference = _panel.PanelRect;
            stackGapFromReference = secondaryTooltipGap;
            stackSecondaryAbove = HoverTooltipLayoutUtility.GetRectScreenCenterY(_panel.PanelRect) < Screen.height * 0.5f;
        }

        ShowSecondaryPanels(targetCanvas, secondaryEntries, stackReference, stackSecondaryAbove, stackGapFromReference);
    }

    /// <summary>Shows and positions a panel directly above <paramref name="reference"/>, front-most and on-screen.</summary>
    void PlacePanelAbove(
        HoverTooltipPanelUI panel,
        RectTransform reference,
        float gap,
        int sortingOrder,
        string title,
        string description,
        Sprite background)
    {
        panel.Show(title, description, background);
        ApplyFrontMostSorting(panel.PanelRect, sortingOrder);
        HoverTooltipLayoutUtility.ForceRebuildLayout(panel.PanelRect);
        // Freshly instantiated panels (first hover) need a canvas flush before their world rect is valid.
        Canvas.ForceUpdateCanvases();
        HoverTooltipLayoutUtility.StackPanelAboveOrBelowRect(panel.PanelRect, reference, gap, above: true);
        HoverTooltipLayoutUtility.ClampRectInsideScreenHorizontally(panel.PanelRect, screenEdgePadding);
    }

    /// <summary>
    /// Shows one stacked panel per secondary entry, each after the previous away from <paramref name="stackReference"/>.
    /// The first panel uses <paramref name="gapFromReference"/>; the rest use <see cref="secondaryTooltipGap"/>.
    /// Returns the last (furthest) panel's rect, or null when there are no entries. With <paramref name="reverseOrder"/>
    /// the entries are laid out so the list reads in order along the stack direction.
    /// </summary>
    RectTransform ShowSecondaryPanels(
        Canvas targetCanvas,
        IReadOnlyList<TooltipContent> secondaryEntries,
        RectTransform stackReference,
        bool stackAbove,
        float gapFromReference,
        bool reverseOrder = false)
    {
        var count = secondaryEntries?.Count ?? 0;
        EnsureSecondaryPanelCount(targetCanvas, count);

        for (var i = count; i < _secondaryPanels.Count; i++)
            _secondaryPanels[i].Hide();

        if (count == 0 || stackReference == null)
            return null;

        // Each entry gets its own tooltip box, stacked one after another away from the reference
        // (e.g. two statuses appear one on top of the other).
        var previous = stackReference;
        for (var k = 0; k < count; k++)
        {
            var entryIndex = reverseOrder ? count - 1 - k : k;
            var panel = _secondaryPanels[k];
            var entry = secondaryEntries[entryIndex];
            panel.Show(entry.Title, entry.Description, entry.Background);
            ApplyFrontMostSorting(panel.PanelRect, tooltipSortingOrder + 1 + k);
            HoverTooltipLayoutUtility.ForceRebuildLayout(panel.PanelRect);
            // Freshly instantiated panels (first hover) need a canvas flush before their world rect is valid.
            Canvas.ForceUpdateCanvases();
            var gap = k == 0 ? gapFromReference : secondaryTooltipGap;
            HoverTooltipLayoutUtility.StackPanelAboveOrBelowRect(panel.PanelRect, previous, gap, stackAbove);
            HoverTooltipLayoutUtility.ClampRectInsideScreenHorizontally(panel.PanelRect, screenEdgePadding);
            previous = panel.PanelRect;
        }

        return previous;
    }

    /// <summary>Puts the panel on its own nested canvas with override sorting so it renders above every scene canvas.</summary>
    static void ApplyFrontMostSorting(RectTransform panelRect, int sortingOrder)
    {
        if (panelRect == null)
            return;

        var canvas = panelRect.GetComponent<Canvas>();
        if (canvas == null)
            canvas = panelRect.gameObject.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = sortingOrder;
    }

    Vector2 ResolveScreenOffset(Vector2 callerScreenOffset, bool isAbove) =>
        (isAbove ? hoverAboveTooltipScreenOffset : callerScreenOffset) + hoverTooltipScreenOffset;

    Vector2 ResolveTrialRewardsScreenOffset(Vector2 callerScreenOffset, bool isAbove) =>
        (isAbove ? hoverAboveTooltipScreenOffset : callerScreenOffset) + trialRewardsTooltipScreenOffset;

    public void ShowTrialRewards(
        RectTransform anchor,
        Vector2 screenPixelOffset,
        PlayerTrialSO trial,
        TrialSaveData state,
        bool isAbove = false)
    {
        if (trialRewardsPanelPrefab == null || anchor == null || trial == null)
            return;

        var targetCanvas = ResolveCanvas(anchor);
        if (targetCanvas == null)
            return;

        EnsureTrialRewardsPanelUnderCanvas(targetCanvas);
        HideSecondaryPanels();
        _panel?.Hide();

        if (ResolveProgressionRewardVisualCatalog() == null)
            return;

        _trialRewardsPanel.Show(trial, state, ResolveProgressionRewardVisualCatalog());
        ApplyFrontMostSorting(_trialRewardsPanel.PanelRect, tooltipSortingOrder);
        HoverTooltipLayoutUtility.ForceRebuildLayout(_trialRewardsPanel.PanelRect);
        Canvas.ForceUpdateCanvases();
        _trialRewardsPanel.AlignToRectWithScreenOffset(anchor, ResolveTrialRewardsScreenOffset(screenPixelOffset, isAbove));
        HoverTooltipLayoutUtility.ClampRectInsideScreenHorizontally(_trialRewardsPanel.PanelRect, screenEdgePadding);
    }

    public void Hide()
    {
        _panel?.Hide();
        HideSecondaryPanels();
        _trialRewardsPanel?.Hide();
    }

    void HideSecondaryPanels()
    {
        for (var i = 0; i < _secondaryPanels.Count; i++)
            _secondaryPanels[i]?.Hide();
    }

    GameIconIndexSO ResolveIconIndex() =>
        gameIconIndex != null ? gameIconIndex : GameIconCatalog.Active;

    Canvas ResolveCanvas(RectTransform anchor)
    {
        var targetCanvas = anchor.GetComponentInParent<Canvas>();
        if (targetCanvas == null)
            targetCanvas = fallbackCanvas;
        if (targetCanvas == null)
            targetCanvas = FindObjectOfType<Canvas>();

        if (targetCanvas == null)
            Debug.LogError("HoverTooltipManager: no Canvas found for tooltip parenting.", this);

        return targetCanvas;
    }

    void EnsurePanelUnderCanvas(Canvas canvas)
    {
        if (_panel != null && _panelParentCanvas == canvas)
            return;

        if (_panelParentCanvas != canvas)
            DestroyPanelIfOwned();

        _panel = Instantiate(panelPrefab, canvas.transform);
        _panel.name = $"{panelPrefab.name} (Runtime)";
        EnsureAlwaysInFront(_panel.PanelRect);
        _panelParentCanvas = canvas;
    }

    /// <summary>Instantiates enough stacked secondary panels (same prefab) for <paramref name="count"/> entries.</summary>
    void EnsureSecondaryPanelCount(Canvas canvas, int count)
    {
        while (_secondaryPanels.Count < count)
        {
            var panel = Instantiate(panelPrefab, canvas.transform);
            panel.name = $"{panelPrefab.name} (Runtime Secondary {_secondaryPanels.Count})";
            EnsureAlwaysInFront(panel.PanelRect);
            _secondaryPanels.Add(panel);
        }
    }

    void EnsureTrialRewardsPanelUnderCanvas(Canvas canvas)
    {
        if (_trialRewardsPanel != null && _panelParentCanvas == canvas)
            return;

        if (_panelParentCanvas != canvas)
            DestroyPanelIfOwned();

        _trialRewardsPanel = Instantiate(trialRewardsPanelPrefab, canvas.transform);
        _trialRewardsPanel.name = $"{trialRewardsPanelPrefab.name} (Runtime)";
        EnsureAlwaysInFront(_trialRewardsPanel.PanelRect);
        _panelParentCanvas = canvas;
    }

    /// <summary>Adds the LateUpdate depth maintainer so the panel keeps rendering in front of 3D geometry.</summary>
    static void EnsureAlwaysInFront(RectTransform panelRect)
    {
        if (panelRect == null)
            return;
        if (panelRect.GetComponent<TooltipAlwaysInFront>() == null)
            panelRect.gameObject.AddComponent<TooltipAlwaysInFront>();
    }
}
