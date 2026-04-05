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

    /// <summary>Rect used as the fly animation destination for this pool type.</summary>
    public RectTransform FlyTargetRect => (RectTransform)transform;

    /// <summary>Static pool-type art (same sprite shown in the bar for this element).</summary>
    public Sprite PoolTypeSprite => icon != null ? icon.sprite : null;

    public void SetValue(int value)
    {
        valueText.text = value.ToString();
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
    }
}
