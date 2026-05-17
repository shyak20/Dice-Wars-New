using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections.Generic;

public class UIShopOfferCardView : MonoBehaviour
{
    [SerializeField] Image iconImage;
    [Tooltip("Optional shadow holder. Uses an Image on this object or its first child Image (excluding the main icon). Re-resolved on each Bind so shop refreshes always pick it up.")]
    [SerializeField] GameObject iconShadowObject;
    [SerializeField] Color iconShadowColor = Color.black;
    [SerializeField] TMP_Text nameText;
    [SerializeField] TMP_Text descriptionText;
    [SerializeField] TMP_Text rarityText;
    [Header("Rarity text colors")]
    [SerializeField] Color commonRarityColor = Color.white;
    [SerializeField] Color rareRarityColor = new(0.2f, 0.6f, 1f);
    [SerializeField] Color legendaryRarityColor = new(1f, 0.474f, 0.052f);
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
    Image _iconShadowImage;
    bool _loggedIconShadowResolveFailure;

    void Awake()
    {
        EnsureIconShadowImageResolved();

        if (buyButton != null)
        {
            buyButton.onClick.AddListener(() => _onBuy?.Invoke());
            SetupBuyButtonHover();
        }

        EnsureHoverVisualDoesNotBlockRaycasts();

        SetHoverVisible(false);
    }

    /// <summary>
    /// Resolves the shadow <see cref="Image"/> from <see cref="iconShadowObject"/> (root or child). Called from Awake and
    /// every <see cref="Bind"/> so shop refreshes still pick it up.
    /// </summary>
    void EnsureIconShadowImageResolved()
    {
        _iconShadowImage = null;
        if (iconShadowObject == null)
            return;

        _iconShadowImage = iconShadowObject.GetComponent<Image>();
        if (_iconShadowImage == null)
        {
            var imgs = iconShadowObject.GetComponentsInChildren<Image>(true);
            for (var i = 0; i < imgs.Length; i++)
            {
                var im = imgs[i];
                if (im == null || im == iconImage)
                    continue;
                _iconShadowImage = im;
                break;
            }
        }

        if (_iconShadowImage == null && !_loggedIconShadowResolveFailure)
        {
            _loggedIconShadowResolveFailure = true;
            Debug.LogError("UIShopOfferCardView: iconShadowObject must contain an Image (on the object or a child).", this);
        }
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

    void EnsureHoverVisualDoesNotBlockRaycasts()
    {
        if (hoverOnBuyButton == null) return;

        var graphics = new List<Graphic>();
        hoverOnBuyButton.GetComponentsInChildren(true, graphics);
        for (var i = 0; i < graphics.Count; i++)
            graphics[i].raycastTarget = false;
    }

    /// <summary>Graphic <see cref="UIShopWindow"/> uses for relic / gem / die hover wiring (buy target graphic, else icon).</summary>
    public Graphic GetOfferHoverGraphic()
    {
        if (buyButton != null && buyButton.targetGraphic is Graphic targetGraphic)
            return targetGraphic;
        return iconImage;
    }

    /// <summary>Alignment rect for die overlay hovers (icon rect when available).</summary>
    public RectTransform GetDieTooltipAlignRect(Graphic hit)
    {
        if (iconImage != null)
            return iconImage.rectTransform;
        return hit != null ? hit.rectTransform : null;
    }

    public void Bind(string title, string desc, Sprite icon, int price, bool canAfford, bool sold, Action onBuy, FaceRarity? rarity = null)
    {
        _price = price;
        _sold = sold;
        _onBuy = onBuy;

        EnsureIconShadowImageResolved();

        if (iconImage != null)
        {
            iconImage.sprite = icon;
            iconImage.enabled = icon != null;
            iconImage.color = icon != null ? Color.white : new Color(1f, 1f, 1f, 0f);
        }
        if (_iconShadowImage != null)
        {
            _iconShadowImage.sprite = icon;
            _iconShadowImage.enabled = icon != null;
            _iconShadowImage.color = icon != null
                ? iconShadowColor
                : new Color(iconShadowColor.r, iconShadowColor.g, iconShadowColor.b, 0f);
        }
        if (nameText != null) nameText.text = title ?? "";
        if (descriptionText != null) descriptionText.text = desc ?? "";
        ApplyRarityText(rarity);
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

    void ApplyRarityText(FaceRarity? rarity)
    {
        if (rarityText == null)
            return;

        if (rarity == null)
        {
            rarityText.gameObject.SetActive(false);
            return;
        }

        rarityText.gameObject.SetActive(true);
        rarityText.text = rarity.Value.ToString();
        rarityText.color = rarity.Value switch
        {
            FaceRarity.Rare => rareRarityColor,
            FaceRarity.Legendary => legendaryRarityColor,
            _ => commonRarityColor,
        };
    }
}
