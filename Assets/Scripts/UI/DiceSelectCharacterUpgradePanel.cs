using UnityEngine;

/// <summary>
/// Shows meta upgrade purchase rows for the character currently selected on the dice-select screen.
/// </summary>
public sealed class DiceSelectCharacterUpgradePanel : MonoBehaviour
{
    [SerializeField] private CharacterMetaUpgradePricesSO upgradePricesBootstrap;
    [SerializeField] private DiceSelectCharacterUpgradeRow healthUpgradeRow;
    [SerializeField] private DiceSelectCharacterUpgradeRow maxPowerUpgradeRow;
    [SerializeField] private DiceSelectCharacterUpgradeRow startingDiceUpgradeRow;
    [SerializeField] private DiceSelectSceneController sceneController;

    public PlayerDataSO CurrentCharacter { get; private set; }

    public MetaCharacterUpgradeManager GetUpgradeManager() => ResolveUpgradeManager();

    void Awake()
    {
        MetaProgressionManager.TryGetRuntime();

        if (upgradePricesBootstrap != null)
            MetaCharacterUpgradeManager.EnsureRuntime(upgradePricesBootstrap);

        if (sceneController == null)
            sceneController = GetComponentInParent<DiceSelectSceneController>();
        if (sceneController == null)
            sceneController = FindObjectOfType<DiceSelectSceneController>();
    }

    void OnEnable()
    {
        MetaCharacterUpgradeManager.OnCharacterUpgradesChanged += OnCharacterUpgradesChanged;
        MetaProgressionManager.OnRubyShardsChanged += OnRubyShardsChanged;
    }

    void OnDisable()
    {
        MetaCharacterUpgradeManager.OnCharacterUpgradesChanged -= OnCharacterUpgradesChanged;
        MetaProgressionManager.OnRubyShardsChanged -= OnRubyShardsChanged;
    }

    public void Refresh(PlayerDataSO character)
    {
        CurrentCharacter = character;
        ApplyRefresh();
    }

    void OnCharacterUpgradesChanged(PlayerDataSO character)
    {
        if (character != CurrentCharacter)
            return;

        ApplyRefresh();
        if (sceneController != null)
            sceneController.RefreshSelectedCharacterPreview();
    }

    void OnRubyShardsChanged(int _)
    {
        if (CurrentCharacter == null)
            return;

        ApplyRefresh();
    }

    void ApplyRefresh()
    {
        var upgrades = ResolveUpgradeManager();
        var metaCurrency = MetaProgressionManager.TryGetRuntime();

        healthUpgradeRow?.Refresh(CurrentCharacter, upgrades, metaCurrency);
        maxPowerUpgradeRow?.Refresh(CurrentCharacter, upgrades, metaCurrency);
        startingDiceUpgradeRow?.Refresh(CurrentCharacter, upgrades, metaCurrency);
    }

    MetaCharacterUpgradeManager ResolveUpgradeManager()
    {
        var upgrades = MetaCharacterUpgradeManager.TryGetRuntime();
        if (upgrades != null)
            return upgrades;

        if (upgradePricesBootstrap == null)
        {
            Debug.LogError(
                "DiceSelectCharacterUpgradePanel: assign upgradePricesBootstrap (Character Meta Upgrade Prices asset).",
                this);
            return null;
        }

        return MetaCharacterUpgradeManager.EnsureRuntime(upgradePricesBootstrap);
    }
}
