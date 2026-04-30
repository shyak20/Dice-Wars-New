using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UIShopOfferCardView : MonoBehaviour
{
    [SerializeField] Image iconImage;
    [SerializeField] TMP_Text nameText;
    [SerializeField] TMP_Text descriptionText;
    [SerializeField] TMP_Text priceText;
    [SerializeField] Color affordablePriceColor = Color.white;
    [SerializeField] Color unaffordablePriceColor = new(1f, 0.35f, 0.35f);
    [SerializeField] Button buyButton;
    [SerializeField] GameObject soldStamp;
    [Tooltip("Shown while the pointer hovers the buy button (requires raycast target on the button Image).")]
    [SerializeField] GameObject hoverOnBuyButton;

    int _price;
    bool _sold;
    Action _onBuy;

    void Awake()
    {
        if (buyButton != null)
        {
            buyButton.onClick.AddListener(() => _onBuy?.Invoke());
            SetupBuyButtonHover();
        }

        SetHoverVisible(false);
    }

    void OnDisable() => SetHoverVisible(false);

    void SetupBuyButtonHover()
    {
        if (hoverOnBuyButton == null)
            return;

        var go = buyButton.gameObject;
        var et = go.GetComponent<EventTrigger>() ?? go.AddComponent<EventTrigger>();
        et.triggers.RemoveAll(e => e.eventID == EventTriggerType.PointerEnter || e.eventID == EventTriggerType.PointerExit);

        var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enter.callback.AddListener(_ => SetHoverVisible(true));
        et.triggers.Add(enter);

        var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exit.callback.AddListener(_ => SetHoverVisible(false));
        et.triggers.Add(exit);
    }

    void SetHoverVisible(bool visible)
    {
        if (hoverOnBuyButton != null)
            hoverOnBuyButton.SetActive(visible);
    }

    public void Bind(string title, string desc, Sprite icon, int price, bool canAfford, bool sold, Action onBuy)
    {
        _price = price;
        _sold = sold;
        _onBuy = onBuy;

        if (iconImage != null)
        {
            iconImage.sprite = icon;
            iconImage.enabled = icon != null;
            iconImage.color = icon != null ? Color.white : new Color(1f, 1f, 1f, 0f);
        }
        if (nameText != null) nameText.text = title ?? "";
        if (descriptionText != null) descriptionText.text = desc ?? "";
        if (priceText != null) priceText.text = price.ToString();
        if (soldStamp != null) soldStamp.SetActive(sold);
        if (buyButton != null) buyButton.interactable = !sold && canAfford;
        if (priceText != null) priceText.color = sold || canAfford ? affordablePriceColor : unaffordablePriceColor;
        SetHoverVisible(false);
    }

    public void RefreshAffordability()
    {
        var canAfford = RunEconomyManager.Instance != null && RunEconomyManager.Instance.CanAfford(_price);
        if (buyButton != null) buyButton.interactable = !_sold && canAfford;
        if (priceText != null) priceText.color = _sold || canAfford ? affordablePriceColor : unaffordablePriceColor;
    }
}
