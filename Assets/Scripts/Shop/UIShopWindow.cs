using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shop storefront: gold header, face offers under a layout group, dice offers under another, leave button.
/// </summary>
public class UIShopWindow : MonoBehaviour
{
    [SerializeField] private ShopGenerator shopGenerator;
    [SerializeField] private ShopFaceSocketFlow socketFlow;
    [SerializeField] private TMP_Text goldHeaderText;
    [Header("Layouts (required)")]
    [Tooltip("RectTransform that has a Horizontal/Vertical/Grid Layout Group for face offer rows.")]
    [SerializeField] private Transform faceSlotsContainer;
    [Tooltip("RectTransform that has a Layout Group for full-die offers.")]
    [SerializeField] private Transform diceSlotsContainer;
    [Tooltip("Face offers prefab root must have a UIShopSlot component.")]
    [SerializeField] private UIShopSlot slotPrefab;
    [Tooltip("Optional full-die offers prefab root. If null, falls back to Slot Prefab.")]
    [SerializeField] private UIShopSlot diceSlotPrefab;
    [SerializeField] private Button leaveShopButton;

    private readonly List<UIShopSlot> _slots = new List<UIShopSlot>();

    private void Start()
    {
        Debug.Log($"[UIShopWindow] Start on '{name}' — shopGenerator={(shopGenerator != null)}, faceSlotPrefab={(slotPrefab != null)}, diceSlotPrefab={(diceSlotPrefab != null)}, faceContainer={(faceSlotsContainer != null)}, diceContainer={(diceSlotsContainer != null)}", this);

        if (leaveShopButton != null)
            leaveShopButton.onClick.AddListener(OnLeaveShop);

        ShopGenerator.InventoryChanged += OnInventoryChanged;
        RunEconomyManager.OnGoldChanged += OnGoldChanged;

        if (shopGenerator == null)
        {
            Debug.LogError("UIShopWindow: Assign Shop Generator (drag the GameObject that has ShopGenerator). Nothing will show until this is set.", this);
            return;
        }

        shopGenerator.GenerateShopInventory();

        if (RunEconomyManager.Instance != null)
            OnGoldChanged(RunEconomyManager.Instance.CurrentGold);
    }

    private void OnDestroy()
    {
        ShopGenerator.InventoryChanged -= OnInventoryChanged;
        RunEconomyManager.OnGoldChanged -= OnGoldChanged;
    }

    private void OnInventoryChanged() => RebuildSlots();

    private void OnGoldChanged(int newTotal)
    {
        if (goldHeaderText != null)
            goldHeaderText.text = newTotal.ToString();

        foreach (var s in _slots)
        {
            if (s != null)
                s.Refresh();
        }
    }

    private void RebuildSlots()
    {
        if (shopGenerator == null)
        {
            Debug.LogError("UIShopWindow.RebuildSlots: shopGenerator is null.", this);
            return;
        }

        if (slotPrefab == null)
        {
            Debug.LogError("UIShopWindow: Slot Prefab is not assigned. Drag your UIShopSlot prefab from the Project window into the UIShopWindow component.", this);
            return;
        }

        if (faceSlotsContainer == null)
            Debug.LogError("UIShopWindow: assign Face Slots Container (empty UI object with a Layout Group).", this);
        if (diceSlotsContainer == null)
            Debug.LogError("UIShopWindow: assign Dice Slots Container.", this);

        foreach (var s in _slots)
        {
            if (s != null && s.gameObject != null)
                Destroy(s.gameObject);
        }

        _slots.Clear();

        foreach (var item in shopGenerator.FaceOffers)
            AddSlot(faceSlotsContainer, item, slotPrefab);

        foreach (var item in shopGenerator.DiceOffers)
            AddSlot(diceSlotsContainer, item, diceSlotPrefab != null ? diceSlotPrefab : slotPrefab);
    }

    private void AddSlot(Transform parent, ShopItem item, UIShopSlot prefab)
    {
        if (parent == null)
        {
            Debug.LogError("UIShopWindow: faceSlotsContainer or diceSlotsContainer is not assigned — shop items cannot be instantiated.");
            return;
        }

        if (prefab == null)
        {
            Debug.LogError("UIShopWindow: slot prefab is not assigned for this section.");
            return;
        }

        var go = Instantiate(prefab, parent);
        var slot = go.GetComponent<UIShopSlot>();
        if (slot == null)
        {
            Debug.LogError("UIShopWindow: assigned slot prefab needs UIShopSlot.");
            Destroy(go);
            return;
        }

        slot.Bind(this, item);
        _slots.Add(slot);
    }

    public void TryBuy(ShopItem item)
    {
        if (shopGenerator == null || item == null || item.IsSoldOut) return;

        if (RunEconomyManager.Instance == null)
        {
            Debug.LogError("UIShopWindow: RunEconomyManager is missing (needs DontDestroyOnLoad in a loaded scene). Buy cannot complete.", this);
            return;
        }

        if (item.ItemKind == ShopItem.Kind.Face && item.Face != null)
        {
            var data = PlayerDataContainer.Instance != null ? PlayerDataContainer.Instance.RuntimeData : null;
            if (data == null || !PlayerInventory.HasDieSupportingFace(data, item.Face))
            {
                Debug.LogWarning("UIShopWindow: Face purchase blocked — no die in deck supports this face's element.");
                return;
            }
        }

        if (!shopGenerator.PurchaseItem(item))
        {
            Debug.LogWarning($"UIShopWindow: Purchase failed (gold={RunEconomyManager.Instance.CurrentGold}, price={item.CalculatedPrice}).", this);
            return;
        }

        if (item.ItemKind == ShopItem.Kind.FullDie && item.Die != null)
        {
            if (PlayerDataContainer.Instance != null)
                PlayerDataContainer.Instance.AddDieToDeck(item.Die);
            ShopToastUI.Show("New Die Acquired");
        }

        RefreshAllSlots();

        if (item.ItemKind == ShopItem.Kind.Face)
        {
            if (socketFlow == null)
            {
                Debug.LogError("UIShopWindow: assign ShopFaceSocketFlow for face purchases.");
                shopGenerator.RefundAndRestock(item);
                RefreshAllSlots();
                return;
            }

            socketFlow.BeginInstallFace(item.Face, item, shopGenerator, RefreshSlotsAfterSocket);
        }
    }

    private void RefreshSlotsAfterSocket()
    {
        RefreshAllSlots();
    }

    private void RefreshAllSlots()
    {
        foreach (var s in _slots)
        {
            if (s != null)
                s.Refresh();
        }

        if (RunEconomyManager.Instance != null)
            OnGoldChanged(RunEconomyManager.Instance.CurrentGold);
    }

    private void OnLeaveShop()
    {
        if (RunManager.Instance != null)
            RunManager.Instance.AdvanceToNextRoom();
        else
            Debug.LogError("UIShopWindow: RunManager missing — cannot leave shop.");
    }
}
