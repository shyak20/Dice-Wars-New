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

    [Tooltip("When the hovered UI has no Canvas in parents (rare), parent the tooltip here.")]
    [SerializeField] private Canvas fallbackCanvas;

    [Tooltip("Added to every show call’s screen offset (global nudge for all hover tooltips).")]
    [SerializeField] private Vector2 hoverTooltipScreenOffset;

    [Tooltip("Screen offset from the anchor when the trigger passes isAbove=true (replaces the caller offset, not added to it). Still adds Hover Tooltip Screen Offset.")]
    [SerializeField] private Vector2 hoverAboveTooltipScreenOffset;

    HoverTooltipPanelUI _panel;
    HoverTrialRewardsTooltipPanelUI _trialRewardsPanel;
    Canvas _panelParentCanvas;

    /// <summary>True when a prefab is assigned so <see cref="HoverTooltipTargetUI"/> can present tooltips.</summary>
    public bool HasValidPrefab => panelPrefab != null;

    public bool HasValidTrialRewardsPrefab => trialRewardsPanelPrefab != null;

    void Awake()
    {
        if (panelPrefab == null)
            Debug.LogError($"HoverTooltipManager on '{name}': assign panelPrefab (HoverTooltipPanelUI prefab).", this);
    }

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

        if (_trialRewardsPanel != null)
        {
            Destroy(_trialRewardsPanel.gameObject);
            _trialRewardsPanel = null;
        }

        _panelParentCanvas = null;
    }

    /// <summary>Resolves title, body, and optional panel background from supported <see cref="ScriptableObject"/> types.</summary>
    public static bool TryGetTooltipContent(ScriptableObject source, out string title, out string description, out Sprite tooltipBackground)
    {
        title = string.Empty;
        description = string.Empty;
        tooltipBackground = null;
        if (source == null)
            return false;

        switch (source)
        {
            case GemSO gem:
                title = gem.DisplayLabel;
                description = gem.description ?? string.Empty;
                return true;
            case RelicSO relic:
                title = string.IsNullOrEmpty(relic.title) ? relic.name : relic.title;
                description = relic.description ?? string.Empty;
                return true;
            case DieFaceSO face:
                if (!DieFaceGameIconOnlyTooltipText.TryBuild(face, out title, out description))
                    return false;
                tooltipBackground = face.uiTooltipBackground;
                return true;
            case DieAssetSO die:
                title = string.IsNullOrEmpty(die.dieName) ? die.name : die.dieName;
                description = $"Type: {die.dieType}";
                tooltipBackground = die.uiTooltipBackground;
                return true;
            case StatusEffectSO status:
                title = string.IsNullOrEmpty(status.effectName) ? status.name : status.effectName;
                description = status.description ?? string.Empty;
                return true;
            case PlayerTrialSO:
                return false;
            default:
                title = source.name;
                description = string.Empty;
                return true;
        }
    }

    /// <summary>Show using data resolved from <paramref name="source"/> (same positioning rules as <see cref="Show"/>).</summary>
    public void ShowForScriptableObject(
        RectTransform anchor,
        Vector2 screenPixelOffset,
        ScriptableObject source,
        bool isAbove = false)
    {
        if (!TryGetTooltipContent(source, out var t, out var d, out var bg))
            return;
        Show(anchor, screenPixelOffset, t, d, bg, isAbove);
    }

    /// <summary>
    /// Shows the shared panel aligned to <paramref name="anchor"/>.
    /// When <paramref name="isAbove"/> is true, uses <see cref="hoverAboveTooltipScreenOffset"/> instead of <paramref name="screenPixelOffset"/>.
    /// </summary>
    public void Show(
        RectTransform anchor,
        Vector2 screenPixelOffset,
        string title,
        string description,
        Sprite tooltipBackground = null,
        bool isAbove = false)
    {
        if (panelPrefab == null || anchor == null)
            return;

        var targetCanvas = ResolveCanvas(anchor);
        if (targetCanvas == null)
            return;

        _trialRewardsPanel?.Hide();
        EnsurePanelUnderCanvas(targetCanvas);

        _panel.Show(title, description, tooltipBackground);
        _panel.AlignToRectWithScreenOffset(anchor, ResolveScreenOffset(screenPixelOffset, isAbove));
    }

    Vector2 ResolveScreenOffset(Vector2 callerScreenOffset, bool isAbove) =>
        (isAbove ? hoverAboveTooltipScreenOffset : callerScreenOffset) + hoverTooltipScreenOffset;

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
        _panel?.Hide();

        _trialRewardsPanel.Show(trial, state, ResolveIconIndex());
        _trialRewardsPanel.AlignToRectWithScreenOffset(anchor, ResolveScreenOffset(screenPixelOffset, isAbove));
    }

    public void Hide()
    {
        _panel?.Hide();
        _trialRewardsPanel?.Hide();
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
        _panelParentCanvas = canvas;
    }

    void EnsureTrialRewardsPanelUnderCanvas(Canvas canvas)
    {
        if (_trialRewardsPanel != null && _panelParentCanvas == canvas)
            return;

        if (_panelParentCanvas != canvas)
            DestroyPanelIfOwned();

        _trialRewardsPanel = Instantiate(trialRewardsPanelPrefab, canvas.transform);
        _trialRewardsPanel.name = $"{trialRewardsPanelPrefab.name} (Runtime)";
        _panelParentCanvas = canvas;
    }
}
