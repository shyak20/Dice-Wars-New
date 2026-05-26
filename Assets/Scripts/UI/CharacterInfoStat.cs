using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// One character stat row: inspector label, value driven by <see cref="DiceSelectCharacterStatsDisplay"/>,
/// and a hover root shown while the pointer is over <see cref="hoverArea"/> (or this object when unset).
/// </summary>
public sealed class CharacterInfoStat : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private string label;
    [SerializeField] private TMP_Text labelText;
    [SerializeField] private TMP_Text valueText;
    [SerializeField] private GameObject hoverRoot;
    [Tooltip("Receives hover events. Defaults to this GameObject when unset.")]
    [SerializeField] private GameObject hoverArea;

    bool _useSelfForHover;

    void Awake()
    {
        if (labelText == null)
            throw new System.InvalidOperationException($"CharacterInfoStat on '{name}': assign labelText.");
        if (valueText == null)
            throw new System.InvalidOperationException($"CharacterInfoStat on '{name}': assign valueText.");
        if (hoverRoot == null)
            throw new System.InvalidOperationException($"CharacterInfoStat on '{name}': assign hoverRoot.");

        labelText.text = label ?? string.Empty;
        SetHoverVisible(false);

        var area = ResolveHoverArea();
        _useSelfForHover = area == gameObject;
        if (!_useSelfForHover)
            CharacterInfoStatHoverBridge.EnsureOn(area, this);
    }

    void OnDisable() => SetHoverVisible(false);

    public void SetValue(string value) => valueText.text = value ?? string.Empty;

    public void ClearValue() => SetValue(string.Empty);

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
