using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>One icon + amount for a deferred dice action row in <see cref="StoredActionsPoolDisplay"/>.</summary>
public class StoredActionsPoolIcon : MonoBehaviour
{
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text valueText;

    [Header("Jackpot presentation (optional)")]
    [SerializeField] private GameObject jackpotMultiplierRoot;
    [SerializeField] private TMP_Text jackpotMultiplierText;
    private HoverTooltipTargetUI hoverTooltipTarget;
    private PoolRowKey configuredKey;

    public RectTransform FlyTargetRect => (RectTransform)transform;

    public Sprite RowSprite => icon != null ? icon.sprite : null;

    public void SetPoolSprite(Sprite sprite)
    {
        if (icon == null) return;
        icon.sprite = sprite;
        icon.enabled = sprite != null;
    }

    public void SetValue(int value)
    {
        valueText.text = value.ToString();
    }

    public void Configure(PoolRowKey key)
    {
        configuredKey = key;
        UpdateTooltipText();
    }

    public void ShowJackpotMultiplierBadge(int multiplier)
    {
        if (jackpotMultiplierRoot == null) return;
        if (jackpotMultiplierText != null)
            jackpotMultiplierText.text = $"×{multiplier}";
        jackpotMultiplierRoot.SetActive(true);
    }

    public void HideJackpotMultiplierBadge()
    {
        if (jackpotMultiplierRoot != null)
            jackpotMultiplierRoot.SetActive(false);
    }

    private void Awake()
    {
        if (icon == null)
            Debug.LogError($"StoredActionsPoolIcon on '{gameObject.name}': icon Image is not assigned!");
        if (valueText == null)
            Debug.LogError($"StoredActionsPoolIcon on '{gameObject.name}': valueText is not assigned!");
        if (jackpotMultiplierRoot != null)
            jackpotMultiplierRoot.SetActive(false);

        var hoverTargetGo = icon != null ? icon.gameObject : gameObject;
        hoverTooltipTarget = hoverTargetGo.GetComponent<HoverTooltipTargetUI>() ?? hoverTargetGo.AddComponent<HoverTooltipTargetUI>();
    }

    private void UpdateTooltipText()
    {
        if (hoverTooltipTarget == null) return;
        var title = configuredKey.StableId.Length > 0 ? configuredKey.DisplayLabel : "Action";
        hoverTooltipTarget.SetContent(title, GetRowDescription(configuredKey));
    }

    private static string GetRowDescription(PoolRowKey key)
    {
        if (PoolRowKey.TryGetDieType(key, out var dt))
        {
            return dt switch
            {
                DieType.Damage => "Deferred damage from this face — applied when you end the turn.",
                DieType.Armor => "Deferred armor from this face — applied when you end the turn.",
                DieType.Fire => "Deferred fire from this face — resolves to a status when the turn ends (if configured on the action).",
                DieType.Ice => "Deferred ice from this face — resolves when the turn ends.",
                DieType.Nature => "Deferred nature from this face — resolves when the turn ends.",
                _ => "Deferred value from this face."
            };
        }

        return "Deferred action from a die — runs when you end the turn; may become a status effect.";
    }
}
