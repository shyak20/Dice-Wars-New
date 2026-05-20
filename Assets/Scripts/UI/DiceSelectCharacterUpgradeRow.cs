using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum DiceSelectUpgradeKind
{
    StartingMaxHealth,
    BaseMaxPower,
    StartingDiceUnlock
}

/// <summary>
/// One meta upgrade purchase row on the dice-select screen.
/// </summary>
public sealed class DiceSelectCharacterUpgradeRow : MonoBehaviour
{
    [SerializeField] private DiceSelectUpgradeKind upgradeKind;
    [SerializeField] private Button purchaseButton;
    [SerializeField] private TMP_Text valueLabel;
    [Tooltip("HP / Max Power only: effective stat after the next purchase (current + increment).")]
    [SerializeField] private TMP_Text valueNextLabel;
    [SerializeField] private TMP_Text priceLabel;
    [Tooltip("Shown when this upgrade track is fully purchased.")]
    [SerializeField] private GameObject maxedVisual;
    [SerializeField] private string valueFormat = "{0}";
    [SerializeField] private string valueNextFormat = "{0}";
    [SerializeField] private string valueStartingDiceFormat = "Unlock {0}";
    [SerializeField] private string priceFormat = "{0}";

    public DiceSelectUpgradeKind UpgradeKind => upgradeKind;

    void Awake()
    {
        if (purchaseButton == null)
            purchaseButton = GetComponent<Button>();

        if (purchaseButton == null)
            Debug.LogError($"DiceSelectCharacterUpgradeRow on '{name}': assign purchaseButton.", this);
        else
            purchaseButton.onClick.AddListener(OnPurchaseClicked);
    }

    void OnDestroy()
    {
        if (purchaseButton != null)
            purchaseButton.onClick.RemoveListener(OnPurchaseClicked);
    }

    public void Refresh(PlayerDataSO character, MetaCharacterUpgradeManager upgrades, MetaProgressionManager metaCurrency)
    {
        if (character == null || upgrades == null || upgrades.UpgradePrices == null)
        {
            SetUnavailable("—");
            return;
        }

        var isMaxed = IsFullyUpgraded(character, upgrades);
        var hasValidNextPurchase = TryGetNextPrice(character, upgrades, out var price);

        ApplyValueLabels(character, upgrades, isMaxed);

        if (maxedVisual != null)
            maxedVisual.SetActive(isMaxed);

        if (priceLabel != null)
        {
            if (isMaxed)
                priceLabel.text = string.Empty;
            else if (hasValidNextPurchase)
                priceLabel.text = string.Format(priceFormat, price);
            else
                priceLabel.text = "—";
        }

        if (purchaseButton != null)
            purchaseButton.interactable = !isMaxed && hasValidNextPurchase;
    }

    void ApplyValueLabels(PlayerDataSO character, MetaCharacterUpgradeManager upgrades, bool isMaxed)
    {
        switch (upgradeKind)
        {
            case DiceSelectUpgradeKind.StartingMaxHealth:
            {
                var current = upgrades.GetEffectiveStartingMaxHealth(character);
                if (valueLabel != null)
                    valueLabel.text = string.Format(valueFormat, current);

                ApplyNextValueLabel(
                    isMaxed,
                    isMaxed ? (int?)null : current + upgrades.UpgradePrices.HealthIncreasePerUpgrade);
                break;
            }

            case DiceSelectUpgradeKind.BaseMaxPower:
            {
                var current = upgrades.GetEffectiveBaseMaxPower(character);
                if (valueLabel != null)
                    valueLabel.text = string.Format(valueFormat, current);

                ApplyNextValueLabel(
                    isMaxed,
                    isMaxed ? (int?)null : current + upgrades.UpgradePrices.MaxPowerIncreasePerUpgrade);
                break;
            }

            case DiceSelectUpgradeKind.StartingDiceUnlock:
            {
                if (valueLabel != null)
                {
                    if (isMaxed)
                        valueLabel.text = "All unlocked";
                    else
                    {
                        var nextDie = GetNextLockedDie(character, upgrades);
                        valueLabel.text = nextDie != null
                            ? string.Format(valueStartingDiceFormat, nextDie.dieName)
                            : "—";
                    }
                }

                ClearNextValueLabel();
                break;
            }
        }
    }

    void ApplyNextValueLabel(bool isMaxed, int? nextValue)
    {
        if (valueNextLabel == null)
            return;

        if (isMaxed || !nextValue.HasValue)
        {
            valueNextLabel.gameObject.SetActive(false);
            valueNextLabel.text = string.Empty;
            return;
        }

        valueNextLabel.gameObject.SetActive(true);
        valueNextLabel.text = string.Format(valueNextFormat, nextValue.Value);
    }

    void ClearNextValueLabel()
    {
        if (valueNextLabel == null)
            return;

        valueNextLabel.gameObject.SetActive(false);
        valueNextLabel.text = string.Empty;
    }

    void OnPurchaseClicked()
    {
        var panel = GetComponentInParent<DiceSelectCharacterUpgradePanel>();
        var character = ResolveCharacterForPurchase(panel);
        if (character == null)
        {
            Debug.LogWarning("DiceSelectCharacterUpgradeRow: no selected character to upgrade.", this);
            return;
        }

        var upgrades = panel != null ? panel.GetUpgradeManager() : MetaCharacterUpgradeManager.TryGetRuntime();
        if (upgrades == null)
        {
            Debug.LogError("DiceSelectCharacterUpgradeRow: MetaCharacterUpgradeManager missing.", this);
            return;
        }

        var result = upgradeKind switch
        {
            DiceSelectUpgradeKind.StartingMaxHealth => upgrades.TryPurchaseHealthUpgrade(character),
            DiceSelectUpgradeKind.BaseMaxPower => upgrades.TryPurchaseMaxPowerUpgrade(character),
            DiceSelectUpgradeKind.StartingDiceUnlock => upgrades.TryPurchaseStartingDiceUnlock(character),
            _ => MetaCharacterUpgradePurchaseResult.ConfigMissing
        };

        if (result != MetaCharacterUpgradePurchaseResult.Success)
        {
            Debug.LogWarning($"DiceSelectCharacterUpgradeRow ({upgradeKind}): purchase failed — {result}.", this);
            return;
        }

        var metaCurrency = MetaProgressionManager.TryGetRuntime();
        Refresh(character, upgrades, metaCurrency);
    }

    static PlayerDataSO ResolveCharacterForPurchase(DiceSelectCharacterUpgradePanel panel)
    {
        if (panel != null && panel.CurrentCharacter != null)
            return panel.CurrentCharacter;

        var controller = Object.FindObjectOfType<DiceSelectSceneController>();
        return controller != null ? controller.SelectedCharacterTemplate : null;
    }

    bool IsFullyUpgraded(PlayerDataSO character, MetaCharacterUpgradeManager upgrades) => upgradeKind switch
    {
        DiceSelectUpgradeKind.StartingMaxHealth => upgrades.IsHealthFullyUpgraded(character),
        DiceSelectUpgradeKind.BaseMaxPower => upgrades.IsMaxPowerFullyUpgraded(character),
        DiceSelectUpgradeKind.StartingDiceUnlock => upgrades.IsStartingDiceFullyUnlocked(character),
        _ => true
    };

    bool TryGetNextPrice(PlayerDataSO character, MetaCharacterUpgradeManager upgrades, out int price)
    {
        price = 0;
        var prices = upgrades.UpgradePrices;
        if (prices == null)
            return false;

        switch (upgradeKind)
        {
            case DiceSelectUpgradeKind.StartingMaxHealth:
                return prices.TryGetHealthUpgradePrice(upgrades.GetHealthUpgradeLevel(character), out price);

            case DiceSelectUpgradeKind.BaseMaxPower:
                return prices.TryGetMaxPowerUpgradePrice(upgrades.GetMaxPowerUpgradeLevel(character), out price);

            case DiceSelectUpgradeKind.StartingDiceUnlock:
                if (GetNextLockedDie(character, upgrades) == null)
                    return false;
                return prices.TryGetStartingDiceUnlockPrice(
                    upgrades.GetStartingDiceUnlockLevel(character),
                    out price);

            default:
                return false;
        }
    }

    static DieAssetSO GetNextLockedDie(PlayerDataSO character, MetaCharacterUpgradeManager upgrades)
    {
        if (character?.lockedStartingDice == null)
            return null;

        var index = upgrades.GetStartingDiceUnlockLevel(character);
        if (index < 0 || index >= character.lockedStartingDice.Count)
            return null;

        return character.lockedStartingDice[index];
    }

    void SetUnavailable(string priceText)
    {
        if (valueLabel != null)
            valueLabel.text = "—";

        ClearNextValueLabel();

        if (priceLabel != null)
            priceLabel.text = priceText;

        if (maxedVisual != null)
            maxedVisual.SetActive(false);

        if (purchaseButton != null)
            purchaseButton.interactable = false;
    }
}
