using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>Optional hover data for shop offers (gem / relic / die). Wired from <see cref="UIShopWindow"/>.</summary>
public sealed class ShopOfferTooltipBindings
{
    public RelicSO Relic;
    public GemSO Gem;
    public DieAssetSO DieOffer;
    public DieTooltipOverlayUI DieTooltipOverlay;
    public HoverTooltipPanelUI HoverTooltipPanel;
}

public class UIShopOfferCardView : MonoBehaviour
{
    [SerializeField] Image iconImage;
    [Tooltip("Optional shadow holder. When assigned, receives the same icon sprite/visibility as iconImage.")]
    [SerializeField] GameObject iconShadowObject;
    [SerializeField] TMP_Text nameText;
    [SerializeField] TMP_Text descriptionText;
    [SerializeField] TMP_Text priceText;
    [SerializeField] Color affordablePriceColor = Color.white;
    [SerializeField] Color unaffordablePriceColor = new(1f, 0.35f, 0.35f);
    [SerializeField] Button buyButton;
    [SerializeField] GameObject soldStamp;
    [Tooltip("Shown while the pointer hovers the buy button (requires raycast target on the button Image).")]
    [SerializeField] GameObject hoverOnBuyButton;
    [Tooltip("Optional. If unset, tooltips use the Buy Button’s Target Graphic (typical full-card hit area), then the Icon Image. Assign only when your layout needs a custom hover region.")]
    [SerializeField] Graphic tooltipHoverTarget;

    int _price;
    bool _sold;
    Action _onBuy;
    Image _iconShadowImage;

    void Awake()
    {
        if (iconShadowObject != null)
        {
            _iconShadowImage = iconShadowObject.GetComponent<Image>();
            if (_iconShadowImage == null)
                Debug.LogError("UIShopOfferCardView: iconShadowObject must have an Image component.", this);
        }

        if (buyButton != null)
        {
            buyButton.onClick.AddListener(() => _onBuy?.Invoke());
            SetupBuyButtonHover();
        }

        EnsureHoverVisualDoesNotBlockRaycasts();

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

    void EnsureHoverVisualDoesNotBlockRaycasts()
    {
        if (hoverOnBuyButton == null) return;

        var graphics = new List<Graphic>();
        hoverOnBuyButton.GetComponentsInChildren(true, graphics);
        for (var i = 0; i < graphics.Count; i++)
            graphics[i].raycastTarget = false;
    }

    public void Bind(string title, string desc, Sprite icon, int price, bool canAfford, bool sold, Action onBuy, ShopOfferTooltipBindings tooltips = null)
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
        if (_iconShadowImage != null)
        {
            _iconShadowImage.sprite = icon;
            _iconShadowImage.enabled = icon != null;
        }
        if (nameText != null) nameText.text = title ?? "";
        if (descriptionText != null) descriptionText.text = desc ?? "";
        if (priceText != null) priceText.text = price.ToString();
        if (soldStamp != null) soldStamp.SetActive(sold);
        if (buyButton != null) buyButton.interactable = !sold && canAfford;
        if (priceText != null) priceText.color = sold || canAfford ? affordablePriceColor : unaffordablePriceColor;
        SetHoverVisible(false);
        ApplyOfferTooltips(tooltips);
    }

    public void RefreshAffordability()
    {
        var canAfford = RunEconomyManager.Instance != null && RunEconomyManager.Instance.CanAfford(_price);
        if (buyButton != null) buyButton.interactable = !_sold && canAfford;
        if (priceText != null) priceText.color = _sold || canAfford ? affordablePriceColor : unaffordablePriceColor;
    }

    Graphic ResolveTooltipHitGraphic()
    {
        if (tooltipHoverTarget != null)
            return tooltipHoverTarget;
        if (buyButton != null && buyButton.targetGraphic is Graphic targetGraphic)
            return targetGraphic;
        return iconImage;
    }

    RectTransform ResolveDieTooltipAlignRect(Graphic hit)
    {
        if (iconImage != null)
            return iconImage.rectTransform;
        return hit != null ? hit.rectTransform : null;
    }

    void ApplyOfferTooltips(ShopOfferTooltipBindings t)
    {
        if (t == null)
            return;

        var hit = ResolveTooltipHitGraphic();
        if (hit == null)
            return;

        hit.raycastTarget = true;

        if (t.Relic != null)
        {
            var go = hit.gameObject;
            var trigger = go.GetComponent<RelicTooltipTrigger>() ?? go.AddComponent<RelicTooltipTrigger>();
            trigger.SetRelic(t.Relic);
            return;
        }

        if (t.Gem != null)
        {
            var panel = t.HoverTooltipPanel != null ? t.HoverTooltipPanel : FindObjectOfType<HoverTooltipPanelUI>(true);
            if (panel == null)
            {
                Debug.LogError("UIShopOfferCardView: gem tooltip needs HoverTooltipPanelUI in the scene or assigned on UIShopWindow.", this);
                return;
            }

            var go = hit.gameObject;
            var hover = go.GetComponent<HoverTooltipTargetUI>() ?? go.AddComponent<HoverTooltipTargetUI>();
            hover.Configure(panel, t.Gem.DisplayLabel, t.Gem.description);
            return;
        }

        if (t.DieOffer != null && t.DieTooltipOverlay != null)
            RegisterDieOfferIconHover(t.DieOffer, t.DieTooltipOverlay, hit);
    }

    void RegisterDieOfferIconHover(DieAssetSO die, DieTooltipOverlayUI overlay, Graphic hit)
    {
        var go = hit.gameObject;
        var et = go.GetComponent<EventTrigger>() ?? go.AddComponent<EventTrigger>();
        var align = ResolveDieTooltipAlignRect(hit);
        var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enter.callback.AddListener(_ => overlay.ShowDie(die, false, null, align));
        var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exit.callback.AddListener(_ => overlay.Hide());
        et.triggers.Add(enter);
        et.triggers.Add(exit);
    }
}
