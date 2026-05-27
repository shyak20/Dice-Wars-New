using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// One character stat row: inspector label, value driven by callers,
/// and a hover root shown while the pointer is over <see cref="hoverArea"/> (or this object when unset).
/// <see cref="iconImage"/> and value text color come from <see cref="GameIconIndexSO"/> via <see cref="mainAttributeIconId"/>.
/// </summary>
public sealed class CharacterInfoStat : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private string label;
    [SerializeField] private TMP_Text labelText;
    [SerializeField] private TMP_Text valueText;
    [Tooltip("Optional. When set, sprite is taken from Game Icon Index for mainAttributeIconId.")]
    [SerializeField] private Image iconImage;
    [SerializeField] private GameObject hoverRoot;
    [Tooltip("Receives hover events. Defaults to this GameObject when unset.")]
    [SerializeField] private GameObject hoverArea;
    [Tooltip("Which main-attribute color from Game Icon Index tints valueText.")]
    [SerializeField] private MainAttributeIconId mainAttributeIconId;

    bool _useSelfForHover;
    Color _defaultValueColor;

    void Awake()
    {
        if (labelText == null)
            throw new System.InvalidOperationException($"CharacterInfoStat on '{name}': assign labelText.");
        if (valueText == null)
            throw new System.InvalidOperationException($"CharacterInfoStat on '{name}': assign valueText.");
        if (hoverRoot == null)
            throw new System.InvalidOperationException($"CharacterInfoStat on '{name}': assign hoverRoot.");

        _defaultValueColor = valueText.color;
        labelText.text = label ?? string.Empty;
        SetHoverVisible(false);

        var area = ResolveHoverArea();
        _useSelfForHover = area == gameObject;
        if (!_useSelfForHover)
            CharacterInfoStatHoverBridge.EnsureOn(area, this);

        ApplyMainAttributeIcon(mainAttributeIconId, null);
    }

    void OnDisable() => SetHoverVisible(false);

    public MainAttributeIconId MainAttributeIconId => mainAttributeIconId;

    public void SetMainAttributeIconId(MainAttributeIconId iconId)
    {
        mainAttributeIconId = iconId;
        ApplyMainAttributeIcon(iconId, null);
    }

    public void SetLabel(string newLabel)
    {
        label = newLabel ?? string.Empty;
        if (labelText != null)
            labelText.text = label;
    }

    public void SetValue(string value, GameIconIndexSO iconIndex = null) =>
        SetValue(value, mainAttributeIconId, iconIndex);

    public void SetValue(string value, MainAttributeIconId iconId, GameIconIndexSO iconIndex = null)
    {
        valueText.text = value ?? string.Empty;
        ApplyValueTextColor(iconId, iconIndex);
        ApplyMainAttributeIcon(iconId, iconIndex);
    }

    public void ClearValue(GameIconIndexSO iconIndex = null) => SetValue(string.Empty, iconIndex);

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_useSelfForHover)
            SetHoverVisible(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (_useSelfForHover)
            SetHoverVisible(false);
    }

    internal void NotifyHoverEnter() => SetHoverVisible(true);

    internal void NotifyHoverExit() => SetHoverVisible(false);

    void ApplyValueTextColor(MainAttributeIconId iconId, GameIconIndexSO iconIndex)
    {
        if (iconIndex != null)
            valueText.color = iconIndex.GetMainAttributeValueColor(iconId);
        else if (GameIconCatalog.Active != null)
            valueText.color = GameIconCatalog.GetMainAttributeValueColor(iconId);
        else
            valueText.color = _defaultValueColor;
    }

    void ApplyMainAttributeIcon(MainAttributeIconId iconId, GameIconIndexSO iconIndex)
    {
        if (iconImage == null)
            return;

        Sprite sprite = null;
        if (iconIndex != null)
            sprite = iconIndex.GetMainAttributeIcon(iconId);
        else if (GameIconCatalog.Active != null)
            sprite = GameIconCatalog.GetMainAttributeIcon(iconId);

        iconImage.sprite = sprite;
        iconImage.enabled = sprite != null;
    }

    void SetHoverVisible(bool visible) => hoverRoot.SetActive(visible);

    GameObject ResolveHoverArea() => hoverArea != null ? hoverArea : gameObject;

    sealed class CharacterInfoStatHoverBridge : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        CharacterInfoStat _owner;

        public static void EnsureOn(GameObject hoverGo, CharacterInfoStat owner)
        {
            var bridge = hoverGo.GetComponent<CharacterInfoStatHoverBridge>();
            if (bridge == null)
                bridge = hoverGo.AddComponent<CharacterInfoStatHoverBridge>();
            bridge._owner = owner;
        }

        public void OnPointerEnter(PointerEventData eventData) => _owner?.NotifyHoverEnter();

        public void OnPointerExit(PointerEventData eventData) => _owner?.NotifyHoverExit();
    }
}
