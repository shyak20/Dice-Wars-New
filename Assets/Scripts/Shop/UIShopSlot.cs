using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>One row in the shop: icon, name, stats, price, buy / sold-out.</summary>
public class UIShopSlot : MonoBehaviour
{
    private static readonly int[] DieFacesGridTemplate = { -1, 0, -1, -1, 1, 2, 3, 4, -1, 5 };

    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text statsText;
    [SerializeField] private TMP_Text priceText;
    [SerializeField] private Color affordablePriceColor = Color.white;
    [SerializeField] private Color unaffordablePriceColor = new Color(1f, 0.35f, 0.35f);
    [SerializeField] private Button buyButton;
    [SerializeField] private GameObject soldOutStamp;
    [Header("Optional Full-Die Faces UI")]
    [Tooltip("Parent for runtime-created face entries (used by full-die shop slots).")]
    [SerializeField] private Transform dieFacesContainer;
    [Tooltip("Prefab for each face entry. Must include UIRewardSlot.")]
    [SerializeField] private UIRewardSlot dieFaceEntryPrefab;

    private UIShopWindow _window;
    private ShopItem _item;
    private readonly List<UIRewardSlot> _spawnedDieFaceEntries = new List<UIRewardSlot>();
    private readonly List<GameObject> _spawnedDieFaceSpacers = new List<GameObject>();

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
            priceText.text = _item.CalculatedPrice.ToString();
            if (sold)
                priceText.color = affordablePriceColor;
            else
                priceText.color = canAffordGold ? affordablePriceColor : unaffordablePriceColor;
        }

        if (_item.ItemKind == ShopItem.Kind.Face)
        {
            ClearDieFacesUi();
            var f = _item.Face;
            if (f == null) return;
            if (nameText != null) nameText.text = f.Title;
            if (statsText != null) statsText.text = $"Value {f.value} · {f.type}";
            if (iconImage != null)
            {
                var spr = f.uiIcon;
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

            RebuildDieFacesUi(d);
        }
    }

    private void OnBuyClicked()
    {
        if (_item == null || _item.IsSoldOut) return;
        _window?.TryBuy(_item);
    }

    private void RebuildDieFacesUi(DieAssetSO die)
    {
        ClearDieFacesUi();
        if (die == null || dieFacesContainer == null || dieFaceEntryPrefab == null) return;
        if (die.faces == null || die.faces.Length == 0) return;

        for (var i = 0; i < DieFacesGridTemplate.Length; i++)
        {
            var faceIndex = DieFacesGridTemplate[i];
            if (faceIndex < 0 || faceIndex >= die.faces.Length)
            {
                CreateDieFaceSpacer();
                continue;
            }

            var face = die.faces[faceIndex];
            if (face == null)
            {
                CreateDieFaceSpacer();
                continue;
            }

            var entry = Instantiate(dieFaceEntryPrefab, dieFacesContainer);
            entry.Bind(face, null);
            entry.SetInteractable(false);
            _spawnedDieFaceEntries.Add(entry);
        }
    }

    private void CreateDieFaceSpacer()
    {
        if (dieFacesContainer == null) return;

        var spacer = new GameObject("Empty", typeof(RectTransform), typeof(LayoutElement));
        spacer.transform.SetParent(dieFacesContainer, false);

        var layout = spacer.GetComponent<LayoutElement>();
        var sourceRect = dieFaceEntryPrefab != null ? dieFaceEntryPrefab.transform as RectTransform : null;
        if (layout != null && sourceRect != null)
        {
            var w = sourceRect.rect.width;
            var h = sourceRect.rect.height;
            if (w > 0f) layout.preferredWidth = w;
            if (h > 0f) layout.preferredHeight = h;
        }

        _spawnedDieFaceSpacers.Add(spacer);
    }

    private void ClearDieFacesUi()
    {
        if (dieFacesContainer == null) return;
        for (var i = 0; i < _spawnedDieFaceEntries.Count; i++)
        {
            var entry = _spawnedDieFaceEntries[i];
            if (entry != null)
                Destroy(entry.gameObject);
        }
        _spawnedDieFaceEntries.Clear();

        for (var i = 0; i < _spawnedDieFaceSpacers.Count; i++)
        {
            var spacer = _spawnedDieFaceSpacers[i];
            if (spacer != null)
                Destroy(spacer);
        }
        _spawnedDieFaceSpacers.Clear();
    }
}
