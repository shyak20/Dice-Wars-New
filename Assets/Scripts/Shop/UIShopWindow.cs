using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UIShopWindow : MonoBehaviour
{
    enum OfferKind { Face, Gem, Relic, Die }
    sealed class OfferData
    {
        public OfferKind Kind; public DieFaceSO Face; public GemSO Gem; public RelicSO Relic; public DieAssetSO Die;
        public int Price; public bool Sold;
    }

    [Header("Loot")] [SerializeField] FaceLootTableSO faceLootTable; [SerializeField] DieLootTableSO dieLootTable;
    [SerializeField] RelicLootTableSO relicLootTable; [SerializeField] GemLootTableSO gemLootTable;
    [Header("Counts")] [SerializeField, Min(0)] int faceOfferCount = 4; [SerializeField, Min(0)] int gemOfferCount = 2;
    [SerializeField, Min(0)] int relicOfferCount = 2; [SerializeField, Min(0)] int dieOfferCount = 1;
    [Header("Header")] [SerializeField] TMP_Text goldHeaderText; [SerializeField] Button leaveShopButton;
    [Header("Offer Containers")] [SerializeField] Transform faceOffersContainer; [SerializeField] Transform gemOffersContainer;
    [SerializeField] Transform relicOffersContainer; [SerializeField] Transform dieOffersContainer;
    [Header("Offer Prefabs")] [SerializeField] UIShopOfferCardView dieFaceOfferPrefab; [SerializeField] UIShopOfferCardView gemOfferPrefab;
    [SerializeField] UIShopOfferCardView relicOfferPrefab; [SerializeField] UIShopOfferCardView dieOfferPrefab;
    [Header("Player Containers")] [SerializeField] Transform playerDiceContainer; [SerializeField] GameObject playerDieButtonPrefab;
    [SerializeField] Transform playerRelicsContainer; [SerializeField] RunRelicSlotView playerRelicSlotPrefab;
    [Header("Pricing")]
    [SerializeField] ShopPricingManager pricingManager;
    [Header("Tooltips")]
    [SerializeField] DieTooltipOverlayUI dieTooltipOverlay;
    [Tooltip("Hover text for gem shop offers. Leave empty to use the first HoverTooltipPanelUI in the scene.")]
    [SerializeField] HoverTooltipPanelUI shopGemHoverTooltipPanel;
    [Header("Popup")] [SerializeField] ShopDieChoicePopupView dieChoicePopup;

    readonly List<OfferData> _face = new(); readonly List<OfferData> _gem = new(); readonly List<OfferData> _relic = new(); readonly List<OfferData> _die = new();
    readonly List<UIShopOfferCardView> _cards = new();
    readonly List<RunRelicSlotView> _spawnedPlayerRelicSlots = new();

    void Start()
    {
        if (pricingManager == null)
            Debug.LogError("UIShopWindow: assign pricingManager.");
        if (leaveShopButton != null) leaveShopButton.onClick.AddListener(OnLeaveShop);
        RunEconomyManager.OnGoldChanged += OnGoldChanged;
        if (RunManager.Instance != null) RunManager.Instance.OnRunRelicsChanged += RebuildPlayerRelics;
        BuildOffers(); RebuildOfferUi(); RebuildPlayerDice(); RebuildPlayerRelics();
        if (RunEconomyManager.Instance != null) OnGoldChanged(RunEconomyManager.Instance.CurrentGold);
    }

    void OnDestroy()
    {
        RunEconomyManager.OnGoldChanged -= OnGoldChanged;
        if (RunManager.Instance != null) RunManager.Instance.OnRunRelicsChanged -= RebuildPlayerRelics;
    }

    void BuildOffers()
    {
        _face.Clear(); _gem.Clear(); _relic.Clear(); _die.Clear();
        if (pricingManager == null)
            return;
        var shopDiscountPercent = Mathf.Clamp(RelicActionRunner.QueryIntMax(RelicPhases.QueryShopDiscountPercent), 0, 95);
        var preferred = BuildPreferredTypes();
        if (faceLootTable != null) foreach (var f in faceLootTable.GetRandomRewards(faceOfferCount, preferred)) if (f != null) _face.Add(new OfferData { Kind = OfferKind.Face, Face = f, Price = ApplyDiscount(pricingManager.GetDieFacePrice(f), shopDiscountPercent) });
        if (gemLootTable != null) foreach (var g in gemLootTable.GetRandomGems(gemOfferCount)) if (g != null) _gem.Add(new OfferData { Kind = OfferKind.Gem, Gem = g, Price = ApplyDiscount(pricingManager.GetGemPrice(g), shopDiscountPercent) });
        if (relicLootTable != null) foreach (var r in relicLootTable.GetRandomRelics(relicOfferCount)) if (r != null) _relic.Add(new OfferData { Kind = OfferKind.Relic, Relic = r, Price = ApplyDiscount(pricingManager.GetRelicPrice(r), shopDiscountPercent) });
        if (dieLootTable != null) foreach (var d in dieLootTable.GetRandomDice(dieOfferCount, preferred, 0.7f, true)) if (d != null) _die.Add(new OfferData { Kind = OfferKind.Die, Die = d, Price = ApplyDiscount(pricingManager.GetDiePrice(d), shopDiscountPercent) });
    }

    static int ApplyDiscount(int basePrice, int discountPercent)
    {
        if (basePrice <= 0) return 0;
        if (discountPercent <= 0) return basePrice;
        return Mathf.Max(1, Mathf.CeilToInt(basePrice * (1f - discountPercent / 100f)));
    }

    HashSet<DieType> BuildPreferredTypes()
    {
        var set = new HashSet<DieType>(); var runtime = PlayerDataContainer.Instance != null ? PlayerDataContainer.Instance.RuntimeData : null;
        if (runtime?.currentDeck == null) return set; foreach (var d in runtime.currentDeck) if (d != null) set.Add(d.dieType); return set;
    }

    void RebuildOfferUi()
    {
        foreach (var c in _cards) if (c != null) Destroy(c.gameObject); _cards.Clear();
        Spawn(faceOffersContainer, dieFaceOfferPrefab, _face); Spawn(gemOffersContainer, gemOfferPrefab, _gem); Spawn(relicOffersContainer, relicOfferPrefab, _relic); Spawn(dieOffersContainer, dieOfferPrefab, _die);
    }

    void Spawn(Transform parent, UIShopOfferCardView prefab, List<OfferData> list)
    {
        if (parent == null || prefab == null) return;
        foreach (var o in list)
        {
            var v = Instantiate(prefab, parent); _cards.Add(v);
            var canAfford = RunEconomyManager.Instance != null && RunEconomyManager.Instance.CanAfford(o.Price);
            v.Bind(NameOf(o), DescOf(o), IconOf(o), o.Price, canAfford, o.Sold, () => TryBuy(o), BuildOfferTooltipBindings(o));
        }
    }

    ShopOfferTooltipBindings BuildOfferTooltipBindings(OfferData o)
    {
        if (o == null) return null;
        switch (o.Kind)
        {
            case OfferKind.Relic when o.Relic != null:
                return new ShopOfferTooltipBindings { Relic = o.Relic };
            case OfferKind.Gem when o.Gem != null:
                return new ShopOfferTooltipBindings { Gem = o.Gem, HoverTooltipPanel = shopGemHoverTooltipPanel };
            case OfferKind.Die when o.Die != null:
                return new ShopOfferTooltipBindings { DieOffer = o.Die, DieTooltipOverlay = dieTooltipOverlay };
            default:
                return null;
        }
    }

    static string NameOf(OfferData o) => o.Kind switch { OfferKind.Face => o.Face != null ? o.Face.Title : "Face", OfferKind.Gem => o.Gem != null ? o.Gem.DisplayLabel : "Gem", OfferKind.Relic => o.Relic != null ? o.Relic.title : "Relic", OfferKind.Die => o.Die != null ? o.Die.dieName : "Die", _ => "" };
    static string DescOf(OfferData o) => o.Kind switch
    {
        OfferKind.Face => o.Face != null ? NormalizeSingleLineBreaks(o.Face.Description) : "",
        OfferKind.Gem => o.Gem != null ? o.Gem.description : "",
        OfferKind.Relic => o.Relic != null ? o.Relic.description : "",
        OfferKind.Die => o.Die != null ? $"Type: {o.Die.dieType}" : "",
        _ => ""
    };
    static Sprite IconOf(OfferData o) => o.Kind switch { OfferKind.Face => o.Face != null ? o.Face.uiIcon : null, OfferKind.Gem => o.Gem != null ? o.Gem.icon : null, OfferKind.Relic => o.Relic != null ? o.Relic.icon : null, OfferKind.Die => o.Die != null ? o.Die.uiIcon : null, _ => null };

    static string NormalizeSingleLineBreaks(string text)
    {
        if (string.IsNullOrEmpty(text))
            return "";
        var normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
        while (normalized.Contains("\n\n"))
            normalized = normalized.Replace("\n\n", "\n");
        return normalized;
    }

    void TryBuy(OfferData o)
    {
        if (o == null || o.Sold) return;
        switch (o.Kind)
        {
            case OfferKind.Face: StartFaceFlow(o); break;
            case OfferKind.Gem: StartGemFlow(o); break;
            case OfferKind.Relic: BuyRelic(o); break;
            case OfferKind.Die: BuyDie(o); break;
        }
    }

    void StartFaceFlow(OfferData o)
    {
        if (dieChoicePopup == null || o.Face == null) return;
        dieChoicePopup.ShowForFaceReplacement(o.Face, (die, idx) =>
        {
            if (die == null || !SameValueFaceCapUtility.CanReplaceFaceWithoutViolatingCap(die, idx, o.Face))
            {
                dieTooltipOverlay?.ShowFaceReplacementRuleError();
                return false;
            }

            if (!SpendGold(o.Price)) return false;
            try
            {
                die.SwapFace(idx, o.Face);
                o.Sold = true;
                RebuildOfferUi();
                // Keep die tooltip open for ShopDieChoicePopupView face-swap close delay (preview on slot).
                RebuildPlayerDice(hideDieTooltipOverlay: false);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError(e, this);
                if (RunEconomyManager.Instance != null) RunEconomyManager.Instance.GrantGold(o.Price, null);
                return false;
            }
        }, null);
    }

    void StartGemFlow(OfferData o)
    {
        if (dieChoicePopup == null || o.Gem == null) return;
        dieChoicePopup.ShowForGemSocket(o.Gem, die =>
        {
            if (die == null || !SpendGold(o.Price)) return false;
            if (!die.TrySocketGem(o.Gem)) { if (RunEconomyManager.Instance != null) RunEconomyManager.Instance.GrantGold(o.Price, null); return false; }
            o.Sold = true; RebuildOfferUi(); RebuildPlayerDice(); return true;
        }, null);
    }

    void BuyRelic(OfferData o) { if (o.Relic == null || !SpendGold(o.Price) || RunManager.Instance == null) return; RunManager.Instance.AddRunRelic(o.Relic); o.Sold = true; RebuildOfferUi(); RebuildPlayerRelics(); }
    void BuyDie(OfferData o) { if (o.Die == null || !SpendGold(o.Price) || PlayerDataContainer.Instance == null) return; PlayerDataContainer.Instance.AddDieToDeck(o.Die); o.Sold = true; RebuildOfferUi(); RebuildPlayerDice(); }
    bool SpendGold(int amount) { var eco = RunEconomyManager.Instance; return eco != null && eco.CanAfford(amount) && eco.TrySpend(amount); }

    void RebuildPlayerDice(bool hideDieTooltipOverlay = true)
    {
        if (playerDiceContainer == null || PlayerDataContainer.Instance?.RuntimeData == null) return;
        foreach (Transform c in playerDiceContainer) Destroy(c.gameObject);
        if (hideDieTooltipOverlay)
            dieTooltipOverlay?.Hide();
        foreach (var die in PlayerDataContainer.Instance.RuntimeData.currentDeck)
        {
            if (die == null || playerDieButtonPrefab == null) continue;
            var go = Instantiate(playerDieButtonPrefab, playerDiceContainer); var txt = go.GetComponentInChildren<TMP_Text>(); if (txt != null) txt.text = die.dieName;
            var tray = go.GetComponent<DiceTrayButtonView>(); if (tray != null) { tray.SetIcon(die.uiIcon); tray.SetSelected(false); tray.SetSelectedIconShakeEnabled(false); }
            RegisterPlayerDieHover(go, die, tray != null ? tray.IconRectTransform : null);
        }
    }

    void RegisterPlayerDieHover(GameObject target, DieAssetSO die, RectTransform iconRect)
    {
        if (target == null || die == null || dieTooltipOverlay == null)
            return;
        var et = target.GetComponent<EventTrigger>() ?? target.AddComponent<EventTrigger>();
        var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enter.callback.AddListener(_ => dieTooltipOverlay.ShowDie(die, false, null, iconRect));
        var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exit.callback.AddListener(_ => dieTooltipOverlay.Hide());
        et.triggers.Add(enter);
        et.triggers.Add(exit);
    }

    void RebuildPlayerRelics()
    {
        if (playerRelicsContainer == null || playerRelicSlotPrefab == null || RunManager.Instance == null) return;
        foreach (var slot in _spawnedPlayerRelicSlots)
        {
            if (slot != null)
                Destroy(slot.gameObject);
        }

        _spawnedPlayerRelicSlots.Clear();
        foreach (var relic in RunManager.Instance.RunRelics)
        {
            if (relic == null) continue;
            var slot = Instantiate(playerRelicSlotPrefab, playerRelicsContainer);
            slot.Bind(relic);
            _spawnedPlayerRelicSlots.Add(slot);
        }
    }

    void OnGoldChanged(int gold) { if (goldHeaderText != null) goldHeaderText.text = gold.ToString(); RebuildOfferUi(); }

    void OnLeaveShop()
    {
        if (RunManager.Instance == null) return;
        if (RunManager.Instance.UseMapBasedRun) RunManager.Instance.ReturnToMapFromSubScene();
        else RunManager.Instance.AdvanceToNextRoom();
    }
}
