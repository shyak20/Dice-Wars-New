using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIShopOfferCardView : MonoBehaviour
{
    [SerializeField] TMP_Text nameText;
    [SerializeField] TMP_Text descriptionText;
    [SerializeField] TMP_Text priceText;
    [SerializeField] Color affordablePriceColor = Color.white;
    [SerializeField] Color unaffordablePriceColor = new(1f, 0.35f, 0.35f);
    [SerializeField] Button buyButton;
    [SerializeField] GameObject soldStamp;

    int _price;
    bool _sold;
    Action _onBuy;

    void Awake()
    {
        if (buyButton != null)
            buyButton.onClick.AddListener(() => _onBuy?.Invoke());
    }

    public void Bind(string title, string desc, int price, bool canAfford, bool sold, Action onBuy)
    {
        _price = price;
        _sold = sold;
        _onBuy = onBuy;

        if (nameText != null) nameText.text = title ?? "";
        if (descriptionText != null) descriptionText.text = desc ?? "";
        if (priceText != null) priceText.text = price.ToString();
        if (soldStamp != null) soldStamp.SetActive(sold);
        if (buyButton != null) buyButton.interactable = !sold && canAfford;
        if (priceText != null) priceText.color = sold || canAfford ? affordablePriceColor : unaffordablePriceColor;
    }

    public void RefreshAffordability()
    {
        var canAfford = RunEconomyManager.Instance != null && RunEconomyManager.Instance.CanAfford(_price);
        if (buyButton != null) buyButton.interactable = !_sold && canAfford;
        if (priceText != null) priceText.color = _sold || canAfford ? affordablePriceColor : unaffordablePriceColor;
    }
}
