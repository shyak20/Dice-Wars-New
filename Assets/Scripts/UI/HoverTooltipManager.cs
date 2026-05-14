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
                if (m.panelPrefab == null)
                    continue;
                return m;
            }

            return null;
        }
    }

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
