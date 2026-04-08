using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>One row in the shop: icon, name, stats, price, buy / sold-out.</summary>
public class UIShopSlot : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text statsText;
    [SerializeField] private TMP_Text priceText;
    [SerializeField] private Color affordablePriceColor = Color.white;
    [SerializeField] private Color unaffordablePriceColor = new Color(1f, 0.35f, 0.35f);
    [SerializeField] private Button buyButton;
    [SerializeField] private GameObject soldOutStamp;

    private UIShopWindow _window;
    private ShopItem _item;

    private void Awake()
    {
        if (buyButton != null)
            buyButton.onClick.AddListener(OnBuyClicked);
    }

    public void Bind(UIShopWindow window, ShopItem item)
    {
        _window = window;
        _item = item;
        Refresh();
    }

    public void Refresh()
    {
        if (_item == null) return;

        var eco = RunEconomyManager.Instance;
        var sold = _item.IsSoldOut;
        // If RunEconomyManager is missing, still allow clicking Buy so TryBuy can log (otherwise interactable stays false forever).
        var canAffordGold = eco != null && eco.CanAfford(_item.CalculatedPrice);
        var economyMissing = eco == null;

        if (soldOutStamp != null) soldOutStamp.SetActive(sold);
        if (buyButton != null)
            buyButton.interactable = !sold && (economyMissing || canAffordGold);

        if (priceText != null)
        {
            priceText.text = $"{_item.CalculatedPrice} Gold";
            if (sold)
                priceText.color = affordablePriceColor;
            else
                priceText.color = canAffordGold ? affordablePriceColor : unaffordablePriceColor;
        }

        if (_item.ItemKind == ShopItem.Kind.Face)
        {
            var f = _item.Face;
            if (f == null) return;
            if (nameText != null) nameText.text = f.Title;
            if (statsText != null) statsText.text = $"Value {f.value} · {f.type}";
            if (iconImage != null)
            {
                var spr = GameIconCatalog.GetElementIcon(f.type);
                iconImage.sprite = spr;
                iconImage.enabled = spr != null;
                iconImage.color = spr != null ? Color.white : new Color(1f, 1f, 1f, 0f);
            }
        }
        else
        {
            var d = _item.Die;
            if (d == null) return;
            if (nameText != null) nameText.text = d.dieName;
            if (statsText != null) statsText.text = $"Full die · {d.dieType}";
            if (iconImage != null)
            {
                iconImage.sprite = d.uiIcon;
                iconImage.enabled = true;
                iconImage.color = d.uiIcon != null ? Color.white : new Color(1f, 1f, 1f, 0f);
            }
        }
    }

    private void OnBuyClicked()
    {
        if (_item == null || _item.IsSoldOut) return;
        _window?.TryBuy(_item);
    }
}
