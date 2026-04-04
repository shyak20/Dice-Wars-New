using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>One row in the stack above a die (optional icon + amount text).</summary>
public class RollOutcomeFlyoutLineView : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text amountText;

    public void Setup(Sprite icon, int amount)
    {
        if (amountText != null)
            amountText.text = amount > 0 ? $"+{amount}" : amount.ToString();

        if (iconImage != null)
        {
            if (icon != null)
            {
                iconImage.sprite = icon;
                iconImage.gameObject.SetActive(true);
            }
            else
                iconImage.gameObject.SetActive(false);
        }
    }

    private void Awake()
    {
        if (amountText == null)
            Debug.LogError($"RollOutcomeFlyoutLineView on '{gameObject.name}': amountText is not assigned!");
    }
}
