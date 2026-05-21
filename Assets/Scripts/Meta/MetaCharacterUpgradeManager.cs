using System;
using System.Collections.Generic;
using UnityEngine;

public enum MetaCharacterUpgradePurchaseResult
{
    Success,
    MaxLevelReached,
    InvalidCharacter,
    ConfigMissing,
    InvalidLockedDie
}

/// <summary>
/// Per-character meta upgrades purchased with ruby shards. Progress persists in <see cref="PlayerPrefs"/>.
/// Applies upgraded starting stats and unlocked dice when <see cref="PlayerDataContainer"/> loads a character.
/// </summary>
public sealed class MetaCharacterUpgradeManager : MonoBehaviour
{
    const string SaveKeyPrefix = "DiceWars_Meta_CharUpgrade_";

    public static MetaCharacterUpgradeManager Instance { get; private set; }

    [SerializeField] private CharacterMetaUpgradePricesSO upgradePrices;

    public CharacterMetaUpgradePricesSO UpgradePrices => upgradePrices;

    public static event Action<PlayerDataSO> OnCharacterUpgradesChanged;

    static CharacterMetaUpgradePricesSO _pendingBootstrapPrices;

    void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        if (upgradePrices == null && _pendingBootstrapPrices != null)
            upgradePrices = _pendingBootstrapPrices;

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (upgradePrices == null)
            Debug.LogError("MetaCharacterUpgradeManager: assign upgradePrices.", this);
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public static MetaCharacterUpgradeManager TryGetRuntime()
    {
        if (Instance != null)
            return Instance;

        return FindObjectOfType<MetaCharacterUpgradeManager>(true);
    }

    /// <summary>
    /// Returns an existing manager or creates a DontDestroyOnLoad fallback when <paramref name="bootstrapPrices"/> is set.
    /// </summary>
    public static MetaCharacterUpgradeManager EnsureRuntime(CharacterMetaUpgradePricesSO bootstrapPrices)
    {
        var existing = TryGetRuntime();
        if (existing != null)
            return existing;

        if (bootstrapPrices == null)
            return null;

        Debug.LogWarning(
            "MetaCharacterUpgradeManager: No manager in scene — creating a runtime fallback. " +
            "Add MetaCharacterUpgradeManager next to RunManager in your bootstrap scene.");

        _pendingBootstrapPrices = bootstrapPrices;
        var go = new GameObject(nameof(MetaCharacterUpgradeManager));
        var created = go.AddComponent<MetaCharacterUpgradeManager>();
        _pendingBootstrapPrices = null;
        return created;
    }

    public int GetHealthUpgradeLevel(PlayerDataSO character) => LoadState(character).healthLevel;

    public int GetMaxPowerUpgradeLevel(PlayerDataSO character) => LoadState(character).maxPowerLevel;

    public int GetStartingDiceUnlockLevel(PlayerDataSO character) => LoadState(character).startingDiceUnlockLevel;

    public int GetEffectiveStartingMaxHealth(PlayerDataSO characterTemplate)
    {
        if (characterTemplate == null || upgradePrices == null)
            return 1;

        var level = GetHealthUpgradeLevel(characterTemplate);
        return characterTemplate.startingMaxHealth + level * upgradePrices.HealthIncreasePerUpgrade;
    }

    public int GetEffectiveBaseMaxPower(PlayerDataSO characterTemplate)
    {
        if (characterTemplate == null || upgradePrices == null)
            return 1;

        var level = GetMaxPowerUpgradeLevel(characterTemplate);
        return characterTemplate.baseMaxPower + level * upgradePrices.MaxPowerIncreasePerUpgrade;
    }

    public bool IsHealthFullyUpgraded(PlayerDataSO character) =>
        upgradePrices != null && GetHealthUpgradeLevel(character) >= upgradePrices.MaxHealthUpgrades;

    public bool IsMaxPowerFullyUpgraded(PlayerDataSO character) =>
        upgradePrices != null && GetMaxPowerUpgradeLevel(character) >= upgradePrices.MaxMaxPowerUpgrades;

    public bool IsStartingDiceFullyUnlocked(PlayerDataSO character) =>
        upgradePrices != null && GetStartingDiceUnlockLevel(character) >= upgradePrices.MaxStartingDiceUnlocks;

    public MetaCharacterUpgradePurchaseResult TryPurchaseHealthUpgrade(PlayerDataSO character)
    {
        if (!ValidatePurchaseSetup(character, out var state))
            return MetaCharacterUpgradePurchaseResult.InvalidCharacter;

        if (upgradePrices == null)
            return MetaCharacterUpgradePurchaseResult.ConfigMissing;

        if (state.healthLevel >= upgradePrices.MaxHealthUpgrades)
            return MetaCharacterUpgradePurchaseResult.MaxLevelReached;

        if (!upgradePrices.HasNextHealthUpgradeTier(state.healthLevel))
            return MetaCharacterUpgradePurchaseResult.MaxLevelReached;

        state.healthLevel++;
        SaveState(character, state);
        NotifyChanged(character);
        return MetaCharacterUpgradePurchaseResult.Success;
    }

    public MetaCharacterUpgradePurchaseResult TryPurchaseMaxPowerUpgrade(PlayerDataSO character)
    {
        if (!ValidatePurchaseSetup(character, out var state))
            return MetaCharacterUpgradePurchaseResult.InvalidCharacter;

        if (upgradePrices == null)
            return MetaCharacterUpgradePurchaseResult.ConfigMissing;

        if (state.maxPowerLevel >= upgradePrices.MaxMaxPowerUpgrades)
            return MetaCharacterUpgradePurchaseResult.MaxLevelReached;

        if (!upgradePrices.HasNextMaxPowerUpgradeTier(state.maxPowerLevel))
            return MetaCharacterUpgradePurchaseResult.MaxLevelReached;

        state.maxPowerLevel++;
        SaveState(character, state);
        NotifyChanged(character);
        return MetaCharacterUpgradePurchaseResult.Success;
    }

    public MetaCharacterUpgradePurchaseResult TryPurchaseStartingDiceUnlock(PlayerDataSO character)
    {
        if (!ValidatePurchaseSetup(character, out var state))
            return MetaCharacterUpgradePurchaseResult.InvalidCharacter;

        if (upgradePrices == null)
            return MetaCharacterUpgradePurchaseResult.ConfigMissing;

        if (state.startingDiceUnlockLevel >= upgradePrices.MaxStartingDiceUnlocks)
            return MetaCharacterUpgradePurchaseResult.MaxLevelReached;

        if (character.lockedStartingDice == null
            || state.startingDiceUnlockLevel >= character.lockedStartingDice.Count
            || character.lockedStartingDice[state.startingDiceUnlockLevel] == null)
        {
            return MetaCharacterUpgradePurchaseResult.InvalidLockedDie;
        }

        if (!upgradePrices.HasNextStartingDiceUnlockTier(state.startingDiceUnlockLevel))
            return MetaCharacterUpgradePurchaseResult.MaxLevelReached;

        state.startingDiceUnlockLevel++;
        SaveState(character, state);
        NotifyChanged(character);
        return MetaCharacterUpgradePurchaseResult.Success;
    }

    /// <summary>
    /// Applies purchased meta upgrades onto a runtime <see cref="PlayerDataSO"/> clone.
    /// </summary>
    public void ApplyUpgradesToRuntimeProfile(PlayerDataSO runtimeProfile, PlayerDataSO template)
    {
        if (runtimeProfile == null || template == null)
        {
            Debug.LogError("MetaCharacterUpgradeManager.ApplyUpgradesToRuntimeProfile: runtimeProfile or template is null.");
            return;
        }

        if (upgradePrices == null)
        {
            Debug.LogError("MetaCharacterUpgradeManager.ApplyUpgradesToRuntimeProfile: upgradePrices is not assigned.", this);
            return;
        }

        var state = LoadState(template);
        runtimeProfile.startingMaxHealth = template.startingMaxHealth + state.healthLevel * upgradePrices.HealthIncreasePerUpgrade;
        runtimeProfile.baseMaxPower = template.baseMaxPower + state.maxPowerLevel * upgradePrices.MaxPowerIncreasePerUpgrade;

        if (runtimeProfile.currentDeck == null)
            runtimeProfile.currentDeck = new List<DieAssetSO>();

        AppendUnlockedStartingDice(runtimeProfile, template, state.startingDiceUnlockLevel);
    }

    /// <summary>Deck order: base <see cref="PlayerDataSO.currentDeck"/> then unlocked meta dice.</summary>
    public List<DieAssetSO> BuildEffectiveStartingDeckTemplates(PlayerDataSO template)
    {
        var deck = new List<DieAssetSO>();
        if (template == null)
            return deck;

        if (template.currentDeck != null)
        {
            for (var i = 0; i < template.currentDeck.Count; i++)
            {
                var die = template.currentDeck[i];
                if (die != null)
                    deck.Add(die);
            }
        }

        var unlockLevel = GetStartingDiceUnlockLevel(template);
        AppendUnlockedStartingDiceTemplates(deck, template, unlockLevel);
        return deck;
    }

    void AppendUnlockedStartingDice(PlayerDataSO runtimeProfile, PlayerDataSO template, int unlockLevel)
    {
        if (template.lockedStartingDice == null || unlockLevel <= 0)
            return;

        var count = Mathf.Min(unlockLevel, template.lockedStartingDice.Count);
        for (var i = 0; i < count; i++)
        {
            var dieTemplate = template.lockedStartingDice[i];
            if (dieTemplate == null)
            {
                Debug.LogError(
                    $"MetaCharacterUpgradeManager: '{template.name}' lockedStartingDice slot {i} is null.",
                    template);
                continue;
            }

            var clone = Instantiate(dieTemplate);
            clone.name = dieTemplate.name;
            runtimeProfile.currentDeck.Add(clone);
        }
    }

    static void AppendUnlockedStartingDiceTemplates(List<DieAssetSO> deck, PlayerDataSO template, int unlockLevel)
    {
        if (template.lockedStartingDice == null || unlockLevel <= 0)
            return;

        var count = Mathf.Min(unlockLevel, template.lockedStartingDice.Count);
        for (var i = 0; i < count; i++)
        {
            var die = template.lockedStartingDice[i];
            if (die != null)
                deck.Add(die);
        }
    }

    bool ValidatePurchaseSetup(PlayerDataSO character, out CharacterUpgradeSaveData state)
    {
        state = default;
        if (character == null)
            return false;

        state = LoadState(character);
        return true;
    }

    void NotifyChanged(PlayerDataSO character) => OnCharacterUpgradesChanged?.Invoke(character);

    CharacterUpgradeSaveData LoadState(PlayerDataSO character)
    {
        var key = BuildSaveKey(character);
        if (string.IsNullOrEmpty(key))
            return new CharacterUpgradeSaveData();

        var json = PlayerPrefs.GetString(key, string.Empty);
        if (string.IsNullOrEmpty(json))
            return new CharacterUpgradeSaveData();

        try
        {
            return JsonUtility.FromJson<CharacterUpgradeSaveData>(json);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"MetaCharacterUpgradeManager: failed to parse save for '{character.name}': {ex.Message}", this);
            return new CharacterUpgradeSaveData();
        }
    }

    void SaveState(PlayerDataSO character, CharacterUpgradeSaveData state)
    {
        var key = BuildSaveKey(character);
        if (string.IsNullOrEmpty(key))
            return;

        PlayerPrefs.SetString(key, JsonUtility.ToJson(state));
        PlayerPrefs.Save();
    }

    static string BuildSaveKey(PlayerDataSO character) =>
        character != null ? SaveKeyPrefix + character.MetaSaveId : null;

    [Serializable]
    struct CharacterUpgradeSaveData
    {
        public int healthLevel;
        public int maxPowerLevel;
        public int startingDiceUnlockLevel;
    }
}
