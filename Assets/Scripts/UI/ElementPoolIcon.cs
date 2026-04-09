using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ElementPoolIcon : MonoBehaviour
{
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text valueText;

    [Header("Jackpot presentation (optional)")]
    [SerializeField] private GameObject jackpotMultiplierRoot;
    [SerializeField] private TMP_Text jackpotMultiplierText;
    private HoverTooltipTargetUI hoverTooltipTarget;
    private DieType configuredType;

    /// <summary>Rect used as the fly animation destination for this pool type.</summary>
    public RectTransform FlyTargetRect => (RectTransform)transform;

    /// <summary>Current art on the pool icon (defaults + runtime overrides).</summary>
    public Sprite PoolTypeSprite => icon != null ? icon.sprite : null;

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

    public void ConfigureType(DieType type)
    {
        configuredType = type;
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
            Debug.LogError($"ElementPoolIcon on '{gameObject.name}': icon Image is not assigned!");
        if (valueText == null)
            Debug.LogError($"ElementPoolIcon on '{gameObject.name}': valueText is not assigned!");
        if (jackpotMultiplierRoot != null)
            jackpotMultiplierRoot.SetActive(false);

        var hoverTargetGo = icon != null ? icon.gameObject : gameObject;
        hoverTooltipTarget = hoverTargetGo.GetComponent<HoverTooltipTargetUI>() ?? hoverTargetGo.AddComponent<HoverTooltipTargetUI>();
        UpdateTooltipText();
    }

    private void UpdateTooltipText()
    {
        if (hoverTooltipTarget == null) return;
        hoverTooltipTarget.SetContent(configuredType.ToString(), GetElementDescription(configuredType));
    }

    private static string GetElementDescription(DieType type)
    {
        return type switch
        {
            DieType.Damage => "Stored physical damage that will be dealt on turn end.",
            DieType.Armor => "Stored armor gained for this turn.",
            DieType.Fire => "Stored fire effect power from rolled actions.",
            DieType.Ice => "Stored ice effect power from rolled actions.",
            DieType.Nature => "Stored nature effect power from rolled actions.",
            _ => "Stored element value from the current roll.",
        };
    }
}
