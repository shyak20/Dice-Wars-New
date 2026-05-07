using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class RequestedRelicAssetGenerator
{
    const string OutputFolder = "Assets/Data/Relics/Requested";
    const string BurnStatusPath = "Assets/Data/Status/Burn.asset";
    const string StrengthStatusPath = "Assets/Data/Status/Strength.asset";

    [MenuItem("Tools/Dice Wars/Relics/Generate Requested Relics")]
    public static void Generate()
    {
        EnsureFolder("Assets/Data", "Relics");
        EnsureFolder("Assets/Data/Relics", "Requested");

        var burn = AssetDatabase.LoadAssetAtPath<BurnEffectSO>(BurnStatusPath);
        var strength = AssetDatabase.LoadAssetAtPath<StrengthEffectSO>(StrengthStatusPath);
        if (burn == null || strength == null)
        {
            Debug.LogError("RequestedRelicAssetGenerator: missing Burn or Strength status assets.");
            return;
        }

        Create("Iron-Bound Pendulum", FaceRarity.Rare,
            "Your Power Meter is increased by +X for every 6 face you have equipped across all dice.",
            new List<RelicGameActionBase> { new RelicMaxPowerPerSixFaceAction { bonusPerSixFace = 1 } });
        Create("Scrap Metal", FaceRarity.Rare,
            "All Armor result give +X to their outcome for every Armor Dice you have.",
            new List<RelicGameActionBase> { new RelicArmorBonusPerArmorDieAction { bonusArmorPerArmorDie = 1 } });
        Create("Arcane Sigil", FaceRarity.Legendary,
            "You can land 1 less than the Power max to gain Perfect Cast.",
            new List<RelicGameActionBase> { new RelicPerfectAtMaxMinusOneAction() });
        Create("Safety Net", FaceRarity.Rare,
            "The first Bust in each combat is ignored.",
            new List<RelicGameActionBase> { new RelicOncePerCombatFreeBustAction() });
        Create("Moonlit Seal", FaceRarity.Legendary,
            "Once per Combat. if you bust by 1 over the Max Power - count as perfect Strike.",
            new List<RelicGameActionBase> { new RelicPerfectAtMaxPlusOneOncePerCombatAction() });
        Create("Storm Orb", FaceRarity.Legendary,
            "Has X stacks. when you bust don't cancel any action, instead lower 1 stack. When stacks are 0, destroy Artifact.",
            new List<RelicGameActionBase> { new RelicStormOrbBustShieldAction { startingStacks = 3 } },
            barBenefitDisplayValue: 3);
        Create("Rune Tablet", FaceRarity.Legendary,
            "The first dice you roll each turn does not add its value to the Power Meter",
            new List<RelicGameActionBase> { new RelicSkipFirstRollPowerEachTurnAction() });
        Create("Magic Wand", FaceRarity.Rare,
            "Get +1 Permanent roll",
            new List<RelicGameActionBase> { new RelicPermanentRollsPerTurnBonusAction { bonusRolls = 1 } });
        Create("Bone Fragment", FaceRarity.Rare,
            "Everytime you score Perfect Cast get +X Max HP",
            new List<RelicGameActionBase> { new RelicMaxHpOnPerfectAction { maxHpGain = 2 } });
        Create("Magic Mirror", FaceRarity.Common,
            "Whenever you roll a list of numbers add +Y damage to the Element Container",
            new List<RelicGameActionBase>
            {
                new RelicAddValueOnFaceListAction { requiredFaceValues = new[] { 2, 4, 6 }, bonusType = RollBonusType.Damage, amount = 2 }
            });
        Create("Ember Charm", FaceRarity.Common,
            "Whenever you roll X add Y Burn to the Element Container.",
            new List<RelicGameActionBase>
            {
                new RelicAddValueOnFaceListAction { requiredFaceValues = new[] { 3 }, bonusType = RollBonusType.Burn, amount = 2, burnDefinition = burn }
            });
        Create("Vampire Fang", FaceRarity.Rare,
            "While your HP is below X% your attacks are doubled",
            new List<RelicGameActionBase> { new RelicDamageBelowHpPercentAction { hpThresholdPercent = 50f, multiplier = 2 } });
        Create("Leather Strap", FaceRarity.Rare,
            "Whenever you roll a list of numbers add +Y Armor",
            new List<RelicGameActionBase>
            {
                new RelicAddValueOnFaceListAction { requiredFaceValues = new[] { 1, 2, 3 }, bonusType = RollBonusType.Armor, amount = 2 }
            });
        Create("Iron Talisman", FaceRarity.Common,
            "Start each Combat with +1 Strength",
            new List<RelicGameActionBase> { new RelicStartCombatStrengthAction { strengthDefinition = strength, stacks = 1 } });
        Create("Shoe", FaceRarity.Common,
            "Gain 2 extra steps in every floor",
            new List<RelicGameActionBase> { new RelicPermanentMapMovesAction { bonusMoves = 2 } });
        Create("Lucky Coin", FaceRarity.Common,
            "Gain bonus X gold when you win a combat",
            new List<RelicGameActionBase> { new RelicBonusGoldOnCombatVictoryAction { bonusGold = 10 } });
        Create("Oil Flask", FaceRarity.Common,
            "Adds +1 Burn to every Burn Die",
            new List<RelicGameActionBase> { new RelicBurnOnEveryFireRollAction { burnDefinition = burn, burnStacks = 1 } });
        Create("Crystal Ball", FaceRarity.Rare,
            "Every time you roll a 3, gain 1 Strength",
            new List<RelicGameActionBase> { new RelicStrengthOnSpecificFaceValueAction { faceValue = 3, strengthDefinition = strength, stacksPerRoll = 1 } });
        Create("Rune Die", FaceRarity.Rare,
            "Every time you roll a 5, your next 5 you roll will deal double damage.",
            new List<RelicGameActionBase> { new RelicDoubleNextFiveAfterFiveAction() });
        Create("Spell Book", FaceRarity.Rare,
            "Perfect Strike multiply Outcome by X",
            new List<RelicGameActionBase> { new RelicPerfectStrikeMultiplierAction { multiplier = 4 } });
        Create("Blood Vial", FaceRarity.Common,
            "Heal 2 HP at the end of each combat.",
            new List<RelicGameActionBase> { new RelicHealOnCombatVictoryAction { healAmount = 2 } });
        Create("Beating Heart", FaceRarity.Legendary,
            "Gain X Max HP at the end of each combat",
            new List<RelicGameActionBase> { new RelicMaxHpOnCombatVictoryAction { maxHpGain = 3 } });
        Create("Phantom Cloak", FaceRarity.Rare,
            "Reduces HP loss from Map Corruption by X%.",
            new List<RelicGameActionBase> { new RelicMapCorruptionDamageReductionPercentAction { reductionPercent = 30 } });
        Create("Iron Buckle", FaceRarity.Rare,
            "Gain X armor for every roll you have left",
            new List<RelicGameActionBase> { new RelicArmorFromRemainingRollsAction { armorPerRemainingRoll = 2 } });
        Create("Discount Ticket", FaceRarity.Common,
            "Shop Offers are X% off",
            new List<RelicGameActionBase> { new RelicShopDiscountPercentAction { discountPercent = 25 } });

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("RequestedRelicAssetGenerator: requested relic assets created/updated in Assets/Data/Relics/Requested.");
    }

    static void Create(string name, FaceRarity rarity, string description, List<RelicGameActionBase> actions, int barBenefitDisplayValue = 0)
    {
        var path = $"{OutputFolder}/{name}.asset";
        var relic = AssetDatabase.LoadAssetAtPath<RelicSO>(path);
        if (relic == null)
        {
            relic = ScriptableObject.CreateInstance<RelicSO>();
            AssetDatabase.CreateAsset(relic, path);
        }

        relic.title = name;
        relic.description = description;
        relic.rarity = rarity;
        relic.barBenefitDisplayValue = barBenefitDisplayValue;
        relic.actions = actions;
        EditorUtility.SetDirty(relic);
    }

    static void EnsureFolder(string parent, string child)
    {
        var combined = $"{parent}/{child}";
        if (!AssetDatabase.IsValidFolder(combined))
            AssetDatabase.CreateFolder(parent, child);
    }
}
