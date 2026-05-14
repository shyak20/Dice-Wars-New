using UnityEngine;

/// <summary>
/// Single entry point for hover text that uses <see cref="HoverTooltipPanelUI"/>: instantiates the panel prefab once,
/// parents it under the same <see cref="Canvas"/> as the anchor (or <see cref="fallbackCanvas"/>), and positions it
/// with per-call screen offset plus <see cref="hoverTooltipScreenOffset"/>.
/// </summary>
[DisallowMultipleComponent]
public sealed class HoverTooltipManager : MonoBehaviour
{
    public static HoverTooltipManager Instance { get; private set; }

    [Tooltip("Prefab whose root has HoverTooltipPanelUI (same layout as legacy scene instances).")]
    [SerializeField] private HoverTooltipPanelUI panelPrefab;

    [Tooltip("When the hovered UI has no Canvas in parents (rare), parent the tooltip here.")]
    [SerializeField] private Canvas fallbackCanvas;

    [Tooltip("Added to every show call’s screen offset (global nudge for all hover tooltips).")]
    [SerializeField] private Vector2 hoverTooltipScreenOffset;

    HoverTooltipPanelUI _panel;
    Canvas _panelParentCanvas;

    /// <summary>True when a prefab is assigned so <see cref="HoverTooltipTargetUI"/> can present tooltips.</summary>
    public bool HasValidPrefab => panelPrefab != null;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning(
                $"HoverTooltipManager: duplicate on '{name}' — only one manager per scene. Remove this component from cloned UI prefabs; destroying duplicate component.",
                this);
            Destroy(this);
            return;
        }

        Instance = this;
        if (panelPrefab == null)
            Debug.LogError($"HoverTooltipManager on '{name}': assign panelPrefab (HoverTooltipPanelUI prefab).", this);
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
        DestroyPanelIfOwned();
    }

    void DestroyPanelIfOwned()
    {
        if (_panel == null)
            return;
        Destroy(_panel.gameObject);
        _panel = null;
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
            default:
                title = source.name;
                description = string.Empty;
                return true;
        }
    }

    /// <summary>Show using data resolved from <paramref name="source"/> (same positioning rules as <see cref="Show"/>).</summary>
    public void ShowForScriptableObject(RectTransform anchor, Vector2 screenPixelOffset, ScriptableObject source)
    {
        if (!TryGetTooltipContent(source, out var t, out var d, out var bg))
            return;
        Show(anchor, screenPixelOffset, t, d, bg);
    }

    /// <summary>Shows the shared panel aligned to <paramref name="anchor"/> with optional screen-pixel offset.</summary>
    public void Show(RectTransform anchor, Vector2 screenPixelOffset, string title, string description, Sprite tooltipBackground = null)
    {
        if (panelPrefab == null || anchor == null)
            return;

        var targetCanvas = anchor.GetComponentInParent<Canvas>();
        if (targetCanvas == null)
            targetCanvas = fallbackCanvas;
        if (targetCanvas == null)
            targetCanvas = FindObjectOfType<Canvas>();

        if (targetCanvas == null)
        {
            Debug.LogError("HoverTooltipManager.Show: no Canvas found for tooltip parenting.", this);
            return;
        }

        EnsurePanelUnderCanvas(targetCanvas);

        _panel.Show(title, description, tooltipBackground);
        var combined = screenPixelOffset + hoverTooltipScreenOffset;
        _panel.AlignToRectWithScreenOffset(anchor, combined);
    }

    public void Hide() => _panel?.Hide();

    void EnsurePanelUnderCanvas(Canvas canvas)
    {
        if (_panel != null && _panelParentCanvas == canvas)
            return;

        DestroyPanelIfOwned();

        _panel = Instantiate(panelPrefab, canvas.transform);
        _panel.name = $"{panelPrefab.name} (Runtime)";
        _panelParentCanvas = canvas;
    }
}
