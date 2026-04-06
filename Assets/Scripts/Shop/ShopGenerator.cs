using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Builds shop inventory when entering a shop room; handles purchase transactions.</summary>
public class ShopGenerator : MonoBehaviour
{
    [SerializeField] private FaceLootTableSO faceLootTable;
    [SerializeField] private DieLootTableSO dieLootTable;
    [SerializeField] private int defaultFullDiePrice = 100;
    [SerializeField, Range(0f, 1f)] private float preferredElementWeight = 0.7f;

    [Header("Offer counts")]
    [SerializeField, Min(0)] private int faceOfferCount = 4;
    [SerializeField, Min(0)] private int diceOfferCount = 1;
    [Tooltip("If true, the same die asset cannot appear twice in one shop refresh.")]
    [SerializeField] private bool uniqueDicePerShopRefresh = true;

    public IReadOnlyList<ShopItem> FaceOffers => _faceItems;
    public IReadOnlyList<ShopItem> DiceOffers => _diceItems;

    private readonly List<ShopItem> _faceItems = new List<ShopItem>();
    private readonly List<ShopItem> _diceItems = new List<ShopItem>();

    public static event Action InventoryChanged;

    private void Awake()
    {
        if (faceLootTable == null)
            Debug.LogError("ShopGenerator: assign faceLootTable.");
        if (dieLootTable == null)
            Debug.LogError("ShopGenerator: assign dieLootTable.");
    }

    public void GenerateShopInventory()
    {
        _faceItems.Clear();
        _diceItems.Clear();

        var preferredTypes = BuildPreferredDieTypes();

        if (faceLootTable != null)
        {
            for (var i = 0; i < faceOfferCount; i++)
            {
                var usePreferred = preferredTypes.Count > 0 && UnityEngine.Random.value < preferredElementWeight;
                var roll = faceLootTable.GetRandomRewards(1, usePreferred ? preferredTypes : null);
                if (roll.Count == 0) continue;

                var face = roll[0];
                var price = FacePriceUtility.GetFaceGoldPrice(face);
                _faceItems.Add(ShopItem.CreateFace(face, price));
            }
        }

        if (dieLootTable != null && diceOfferCount > 0)
        {
            var dice = dieLootTable.GetRandomDice(
                diceOfferCount,
                preferredTypes.Count > 0 ? preferredTypes : null,
                preferredElementWeight,
                uniqueDicePerShopRefresh);

            foreach (var die in dice)
            {
                if (die == null) continue;
                var price = die.shopGoldPrice > 0 ? die.shopGoldPrice : defaultFullDiePrice;
                _diceItems.Add(ShopItem.CreateDie(die, price));
            }

            if (_diceItems.Count == 0 && (dieLootTable.allPossibleDice == null || dieLootTable.allPossibleDice.Count == 0))
                Debug.LogWarning("ShopGenerator: DieLootTableSO has no dice in allPossibleDice — shop will show no die offers.");
            else if (_diceItems.Count == 0 && diceOfferCount > 0)
                Debug.LogWarning("ShopGenerator: requested dice offers but none were rolled. Check DieLootTableSO has valid DieAssetSO entries.");
        }
        else if (diceOfferCount > 0 && dieLootTable == null)
            Debug.LogWarning("ShopGenerator: dieLootTable is not assigned.");

        if (_faceItems.Count == 0 && faceOfferCount > 0)
            Debug.LogWarning("ShopGenerator: no face offers generated. Check FaceLootTableSO (faces list + Rarity Config) on the assigned asset.");
        if (_diceItems.Count == 0 && diceOfferCount > 0)
            Debug.LogWarning("ShopGenerator: no die offers. Check DieLootTableSO has at least one DieAssetSO and Dice Offer Count > 0.");

        Debug.Log($"[ShopGenerator] Generated shop: {_faceItems.Count} face offer(s), {_diceItems.Count} die offer(s).", this);

        InventoryChanged?.Invoke();
    }

    private static HashSet<DieType> BuildPreferredDieTypes()
    {
        var set = new HashSet<DieType>();
        var data = PlayerDataContainer.Instance != null ? PlayerDataContainer.Instance.RuntimeData : null;
        if (data?.currentDeck == null) return set;

        foreach (var d in data.currentDeck)
        {
            if (d != null)
                set.Add(d.dieType);
        }

        return set;
    }

    /// <summary>Deducts gold and marks the item sold. Returns false if unaffordable or already sold.</summary>
    public bool PurchaseItem(ShopItem item)
    {
        if (item == null || item.IsSoldOut) return false;

        var eco = RunEconomyManager.Instance;
        if (eco == null)
        {
            Debug.LogError("ShopGenerator.PurchaseItem: RunEconomyManager missing.");
            return false;
        }

        if (!eco.CanAfford(item.CalculatedPrice)) return false;
        if (!eco.TrySpend(item.CalculatedPrice)) return false;

        item.IsSoldOut = true;
        return true;
    }

    /// <summary>Refunds gold and clears sold state when a face purchase cannot be socketed (e.g. no matching die).</summary>
    public void RefundAndRestock(ShopItem item)
    {
        if (item == null || !item.IsSoldOut) return;

        var eco = RunEconomyManager.Instance;
        if (eco != null)
            eco.GrantGold(item.CalculatedPrice, null);

        item.IsSoldOut = false;
        InventoryChanged?.Invoke();
    }
}
