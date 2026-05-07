using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class StatusEffectIconUI : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text stackText;
    [Tooltip("Optional root for the stack badge visuals (text/background). Hidden when stacks are 0.")]
    [SerializeField] private GameObject stackVisualRoot;
    [Header("Tooltip")]
    [Tooltip("Can be a scene instance or a prefab asset. If prefab, a runtime instance is created under this icon's Canvas.")]
    [SerializeField] private HoverTooltipPanelUI hoverTooltipPanel;
    [Tooltip("Tooltip offset in screen pixels relative to the hovered status icon.")]
    [SerializeField] private Vector2 tooltipScreenOffset = new Vector2(0f, 24f);
    private HoverTooltipTargetUI hoverTooltipTarget;
    private HoverTooltipTargetUI hoverTooltipTargetOnIcon;
    private HoverTooltipPanelUI _runtimeTooltipPanel;

    private void Awake()
    {
        if (iconImage == null)
            Debug.LogError($"StatusEffectIconUI on '{gameObject.name}': iconImage is not assigned!");
        if (stackText == null)
            Debug.LogError($"StatusEffectIconUI on '{gameObject.name}': stackText is not assigned!");

        hoverTooltipTarget = GetComponent<HoverTooltipTargetUI>() ?? gameObject.AddComponent<HoverTooltipTargetUI>();
        hoverTooltipTarget.SetTooltipScreenOffset(tooltipScreenOffset);
        if (iconImage != null)
        {
            hoverTooltipTargetOnIcon = iconImage.gameObject.GetComponent<HoverTooltipTargetUI>() ?? iconImage.gameObject.AddComponent<HoverTooltipTargetUI>();
            hoverTooltipTargetOnIcon.SetTooltipScreenOffset(tooltipScreenOffset);
            iconImage.raycastTarget = true;
        }
    }

    public void Setup(StatusEffectInstance effect)
    {
        RefreshVisual(effect);
    }

    public void SetTooltipPanelPrefab(HoverTooltipPanelUI panelPrefab)
    {
        if (panelPrefab == null)
            return;
        if (hoverTooltipPanel == panelPrefab)
            return;

        hoverTooltipPanel = panelPrefab;
        _runtimeTooltipPanel = null;
    }

    public void SetupCustom(Sprite icon, string title, string description, Sprite tooltipBackground = null)
    {
        if (iconImage != null)
        {
            iconImage.sprite = icon;
            iconImage.enabled = icon != null;
        }

        ApplyTooltipContent(title, description, tooltipBackground);

        UpdateStacks(0);
    }

    public void RefreshVisual(StatusEffectInstance effect)
    {
        if (effect == null || effect.Definition == null) return;
        var spr = GameIconCatalog.GetStatusIcon(effect.Definition);
        if (spr == null && effect.Definition is BurnEffectSO)
            spr = GameIconCatalog.GetElementIcon(DieType.Fire);
        if (spr == null && effect.Definition is EchoEffectSO)
            spr = GameIconCatalog.GetElementIcon(DieType.Armor);
        if (spr == null && effect.Definition is ImmuneEffectSO)
            spr = GameIconCatalog.GetElementIcon(DieType.Ice);
        if (spr == null && effect.Definition is ThornsEffectSO)
            spr = GameIconCatalog.GetActionIcon(ActionVisualId.Thorns);
        if (iconImage != null)
        {
            iconImage.sprite = spr;
            iconImage.enabled = spr != null;
        }
        if (hoverTooltipTarget != null)
        {
            var title = string.IsNullOrWhiteSpace(effect.Definition.effectName) ? effect.Definition.name : effect.Definition.effectName;
            ApplyTooltipContent(title, effect.Definition.description);
        }
        UpdateStacks(effect.Stacks);
    }

    public void UpdateStacks(int stacks)
    {
        var hasStacks = stacks > 0 && stackText != null;
        if (stackText != null)
            stackText.text = hasStacks ? stacks.ToString() : string.Empty;
        if (stackVisualRoot != null)
            stackVisualRoot.SetActive(hasStacks);
    }

    private void ApplyTooltipContent(string title, string description, Sprite tooltipBackground = null)
    {
        var panel = ResolveTooltipPanel();
        if (hoverTooltipTarget != null)
        {
            if (panel != null) hoverTooltipTarget.Configure(panel, title, description, tooltipBackground);
            else hoverTooltipTarget.SetContent(title, description, tooltipBackground);
        }

        if (hoverTooltipTargetOnIcon != null)
        {
            if (panel != null) hoverTooltipTargetOnIcon.Configure(panel, title, description, tooltipBackground);
            else hoverTooltipTargetOnIcon.SetContent(title, description, tooltipBackground);
        }
    }

    private HoverTooltipPanelUI ResolveTooltipPanel()
    {
        if (_runtimeTooltipPanel != null)
            return _runtimeTooltipPanel;
        if (hoverTooltipPanel == null)
            return null;

        if (hoverTooltipPanel.gameObject.scene.IsValid())
        {
            _runtimeTooltipPanel = hoverTooltipPanel;
            return _runtimeTooltipPanel;
        }

        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
            return null;
        _runtimeTooltipPanel = Instantiate(hoverTooltipPanel, canvas.transform);
        _runtimeTooltipPanel.name = hoverTooltipPanel.name;
        return _runtimeTooltipPanel;
    }
}
