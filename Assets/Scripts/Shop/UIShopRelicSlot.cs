using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Shop row for a <see cref="ShopItem.Kind.Relic"/> offer: title and description from <see cref="RelicSO"/>.</summary>
public sealed class UIShopRelicSlot : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text descriptionText;
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
        if (_item == null || _item.ItemKind != ShopItem.Kind.Relic)
            return;

        var r = _item.Relic;
        var eco = RunEconomyManager.Instance;
        var sold = _item.IsSoldOut;
        var canAffordGold = eco != null && eco.CanAfford(_item.CalculatedPrice);
        var economyMissing = eco == null;

        if (soldOutStamp != null)
            soldOutStamp.SetActive(sold);
        if (buyButton != null)
            buyButton.interactable = !sold && (economyMissing || canAffordGold);

        if (priceText != null)
        {
            priceText.text = _item.CalculatedPrice.ToString();
            priceText.color = sold ? affordablePriceColor : (canAffordGold ? affordablePriceColor : unaffordablePriceColor);
        }

        if (r == null)
        {
            if (titleText != null) titleText.text = "";
            if (descriptionText != null) descriptionText.text = "";
            if (iconImage != null)
            {
                iconImage.sprite = null;
                iconImage.enabled = false;
            }
            return;
        }

        if (titleText != null)
            titleText.text = r.title;
        if (descriptionText != null)
            descriptionText.text = r.description;
        if (iconImage != null)
        {
            iconImage.sprite = r.icon;
            iconImage.enabled = r.icon != null;
            iconImage.color = r.icon != null ? Color.white : new Color(1f, 1f, 1f, 0f);
        }
    }

    private void OnBuyClicked()
    {
        if (_item == null || _item.IsSoldOut) return;
        _window?.TryBuy(_item);
    }
}
