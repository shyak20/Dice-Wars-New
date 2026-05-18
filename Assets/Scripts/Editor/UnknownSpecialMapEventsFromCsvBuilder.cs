#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// One-shot builder: creates <see cref="UnknownMapEventSO"/> assets matching
/// "Dice Design for Game - Special Events (1).csv" and appends them to Act 1’s unknown pool.
/// Menu: <b>DiceGame / Map / Build Special Unknown Events From CSV Design</b>.
/// </summary>
public static class UnknownSpecialMapEventsFromCsvBuilder
{
    const string OutputFolder = "Assets/Data/Unknown Events/Special";
    const string Act1MapPath = "Assets/Data/Run/Act 1 Map.asset";

    [MenuItem("DiceGame/Map/Build Special Unknown Events From CSV Design")]
    public static void Build()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Data/Unknown Events"))
            AssetDatabase.CreateFolder("Assets/Data", "Unknown Events");
        if (!AssetDatabase.IsValidFolder(OutputFolder))
            AssetDatabase.CreateFolder("Assets/Data/Unknown Events", "Special");

        File.WriteAllText(
            Path.Combine(OutputFolder, "Special Events Source.csv"),
            EmbeddedCsv);

        var curse = Load<DieFaceSO>("Assets/Data/Faces/Curses/Curse Test.asset");
        var strRelic = Load<RelicSO>("Assets/Data/Relics/Requested/Mystic Cube.asset");
        var statueArt = Load<Sprite>("Assets/Visuals/UI/Unknown Event Assets/Statue.png");
        var atkBase = Load<DieFaceSO>("Assets/Data/Faces/Physical/Base Physical/2 - Attack Base.asset");
        var defBase = Load<DieFaceSO>("Assets/Data/Faces/Defense/Stating Defense/1 - Defense Base.asset");
        var fireBase = Load<DieFaceSO>("Assets/Data/Faces/Fire/Base Fire/1 - Fire Base.asset");

        var facesLootTable = Load<FaceLootTableSO>("Assets/Data/Faces/Faces Loot Table.asset");

        var gemPairs = new List<UnknownMapEventGemUpgradePair>
        {
            Pair("Assets/Data/Gems/Heal/Heal_Level1.asset", "Assets/Data/Gems/Heal/Heal_Level2.asset"),
            Pair("Assets/Data/Gems/Damage/Damage_Level1.asset", "Assets/Data/Gems/Damage/Damage_Level2.asset"),
            Pair("Assets/Data/Gems/Armor/Armor_Level1.asset", "Assets/Data/Gems/Armor/Armor_Level2.asset"),
            Pair("Assets/Data/Gems/Burn/Burn_Level1.asset", "Assets/Data/Gems/Burn/Burn_Level2.asset"),
            Pair("Assets/Data/Gems/Coins/Coins_Level1.asset", "Assets/Data/Gems/Coins/Coins_Level2.asset"),
            Pair("Assets/Data/Gems/MaxHp/MaxHp_Level1.asset", "Assets/Data/Gems/MaxHp/MaxHp_Level2.asset"),
        };

        var created = new List<UnknownMapEventSO>();

        created.Add(Save("UE_AltarOfGreed", BuildAltarOfGreed()));
        created.Add(Save("UE_CursedAnvil", BuildCursedAnvil(curse, facesLootTable)));
        created.Add(Save("UE_FossilizedD6", BuildFossilizedD6(curse)));
        var f1Ev = BuildResonatingFountain1();
        f1Ev.excludeFromDrawIfCompletedThisRun = true;
        var f1 = Save("UE_ResonatingFountain1", f1Ev);
        created.Add(f1);
        var f2Ev = BuildResonatingFountain2(f1);
        f2Ev.excludeFromDrawIfCompletedThisRun = true;
        var f2 = Save("UE_ResonatingFountain2", f2Ev);
        created.Add(f2);
        var f3Ev = BuildResonatingFountain3(f2);
        f3Ev.excludeFromDrawIfCompletedThisRun = true;
        created.Add(Save("UE_ResonatingFountain3", f3Ev));
        created.Add(Save("UE_SplittingPrism", BuildSplittingPrism()));
        created.Add(Save("UE_StarlightBasin", BuildStarlightBasin()));
        created.Add(Save("UE_CatalystOfSacrifice", BuildCatalystOfSacrifice()));
        created.Add(Save("UE_ScouredObelisk", BuildScouredObelisk(atkBase, defBase, fireBase)));
        created.Add(Save("UE_ArcanistsPolishingWheel", BuildPolishingWheel(gemPairs)));
        created.Add(Save("UE_MerlinsMonument", BuildMerlinsMonument(strRelic, curse, statueArt)));

        foreach (var ev in created)
        {
            if (ev == null)
                continue;
            EditorUtility.SetDirty(ev);
        }

        AssetDatabase.SaveAssets();
        AppendToAct1UnknownPool(created);
        AssetDatabase.SaveAssets();
        Debug.Log($"UnknownSpecialMapEventsFromCsvBuilder: wrote {created.Count} assets under {OutputFolder} and appended to Act 1 Map.");
    }

    static UnknownMapEventGemUpgradePair Pair(string fromPath, string toPath)
    {
        return new UnknownMapEventGemUpgradePair { from = Load<GemSO>(fromPath), to = Load<GemSO>(toPath) };
    }

    static T Load<T>(string path) where T : Object
    {
        var a = AssetDatabase.LoadAssetAtPath<T>(path);
        if (a == null)
            Debug.LogError($"UnknownSpecialMapEventsFromCsvBuilder: missing asset at {path}");
        return a;
    }

    static UnknownMapEventSO Save(string assetFileName, UnknownMapEventSO ev)
    {
        if (ev == null)
            return null;
        ev.name = assetFileName;
        var path = $"{OutputFolder}/{assetFileName}.asset";
        if (File.Exists(path))
            AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(ev, path);
        return AssetDatabase.LoadAssetAtPath<UnknownMapEventSO>(path);
    }

    static void AppendToAct1UnknownPool(List<UnknownMapEventSO> additions)
    {
        var map = AssetDatabase.LoadAssetAtPath<MapActDefinitionSO>(Act1MapPath);
        if (map == null)
        {
            Debug.LogError("UnknownSpecialMapEventsFromCsvBuilder: Act 1 Map asset not found.");
            return;
        }

        var existing = new HashSet<UnknownMapEventSO>();
        if (map.possibleUnknownEvents != null)
        {
            foreach (var u in map.possibleUnknownEvents)
            {
                if (u != null)
                    existing.Add(u);
            }
        }

        var toAdd = new List<UnknownMapEventSO>();
        foreach (var a in additions)
        {
            if (a == null || existing.Contains(a))
                continue;
            toAdd.Add(a);
            existing.Add(a);
        }

        if (toAdd.Count == 0)
            return;

        var so = new SerializedObject(map);
        var prop = so.FindProperty("possibleUnknownEvents");
        var start = prop.arraySize;
        prop.arraySize += toAdd.Count;
        for (var i = 0; i < toAdd.Count; i++)
        {
            prop.GetArrayElementAtIndex(start + i).objectReferenceValue = toAdd[i];
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(map);
    }

    static UnknownMapEventSO BuildAltarOfGreed()
    {
        var ev = ScriptableObject.CreateInstance<UnknownMapEventSO>();
        ev.displayName = "The Altar of Greed";
        ev.description =
            "A jagged stone altar, stained with the ink of a thousand discarded runes. It whispers of wealth in exchange for your tools. The air around it smells of copper and regret.";
        ev.choices = new[]
        {
            Row(
                "[Cost: 1 Die] Sacrifice a die to gain +150 Gold",
                new List<UnknownMapEventConditionBase> { new UnknownMapEventConditionDeckDiceCountMin { minimumCount = 2 } },
                new UnknownMapEventOutcomeAfterDieChoice
                {
                    steps = new List<UnknownMapEventOutcomeBase>
                    {
                        new UnknownMapEventOutcomeRemoveChosenDeckDie(),
                        new UnknownMapEventOutcomeGrantGold { amount = 150 },
                    },
                }),
            Row("Leave", new List<UnknownMapEventConditionBase> { new UnknownMapEventConditionAlwaysTrue() }, new UnknownMapEventOutcomeNoOp()),
        };
        return ev;
    }

    static UnknownMapEventSO BuildCursedAnvil(DieFaceSO curse, FaceLootTableSO facesLootTable)
    {
        var ev = ScriptableObject.CreateInstance<UnknownMapEventSO>();
        ev.displayName = "The Cursed Anvil";
        ev.description =
            "A heavy anvil made of dark, pulsating obsidian. Hammers made of shadow beat against it in a rhythmic trance. It can forge greatness, but it always demands a dark price in return.";
        ev.choices = new[]
        {
            Row(
                "[Penalty: Add 1 Curse] Choose a die and add a Legendary Face of that type.",
                new List<UnknownMapEventConditionBase> { new UnknownMapEventConditionDeckDiceCountMin { minimumCount = 1 } },
                new UnknownMapEventOutcomeAfterDieChoice
                {
                    steps = new List<UnknownMapEventOutcomeBase>
                    {
                        new UnknownMapEventOutcomeAddCurseFaceToChosenDie { curseFace = curse },
                        new UnknownMapEventOutcomeReplaceRandomFaceWithRarityOnChosenDie
                        {
                            facesLootTable = facesLootTable,
                            rarity = FaceRarity.Legendary,
                        },
                    },
                }),
            Row("Leave", new List<UnknownMapEventConditionBase> { new UnknownMapEventConditionAlwaysTrue() }, new UnknownMapEventOutcomeNoOp()),
        };
        return ev;
    }

    static UnknownMapEventSO BuildFossilizedD6(DieFaceSO curse)
    {
        var ev = ScriptableObject.CreateInstance<UnknownMapEventSO>();
        ev.displayName = "The Fossilized D6";
        ev.description =
            "In a corner of the vault, you find a massive stone die half-buried in crystalline growth. It vibrates with an ancient, heavy frequency. It isn't dead—it's waiting for a new spark.";
        ev.choices = new[]
        {
            Row(
                "[Cost: Add 1 Curse] Gain +2 Strength permanently (for the entire run).",
                new List<UnknownMapEventConditionBase> { new UnknownMapEventConditionHasDieOfType { dieType = DieType.Damage } },
                new UnknownMapEventOutcomeComposite
                {
                    steps = new List<UnknownMapEventOutcomeBase>
                    {
                        new UnknownMapEventOutcomeAddCurseFaceToRandomDie { curseFace = curse },
                        new UnknownMapEventOutcomeAddRunPermanentStrengthStacks { stacks = 2 },
                    },
                }),
            Row(
                "[Cost: 10 HP] Gain 150 Coins.",
                new List<UnknownMapEventConditionBase> { new UnknownMapEventConditionRunCurrentHpMin { minimumHp = 11 } },
                new UnknownMapEventOutcomeComposite
                {
                    steps = new List<UnknownMapEventOutcomeBase>
                    {
                        new UnknownMapEventOutcomeApplyRunCurrentHpDelta { hpDelta = -10 },
                        new UnknownMapEventOutcomeGrantGold { amount = 150 },
                    },
                }),
            Row("Leave", new List<UnknownMapEventConditionBase> { new UnknownMapEventConditionAlwaysTrue() }, new UnknownMapEventOutcomeNoOp()),
        };
        return ev;
    }

    static UnknownMapEventSO BuildResonatingFountain1()
    {
        var ev = ScriptableObject.CreateInstance<UnknownMapEventSO>();
        ev.displayName = "The Resonating Fountain 1";
        ev.description =
            "A pool of liquid mercury-mana sits perfectly still. As you approach, your Resonance Meter begins to hum in sympathy. The water reflects not your face, but the potential of your spells.";
        ev.choices = new[]
        {
            Row(
                "[Cost: 10 Max HP] Increase Max Power by 1 (Open The Resonating Fountain 2)",
                new List<UnknownMapEventConditionBase> { new UnknownMapEventConditionRunMaxHpMin { minimumMaxHp = 11 } },
                new UnknownMapEventOutcomeComposite
                {
                    steps = new List<UnknownMapEventOutcomeBase>
                    {
                        new UnknownMapEventOutcomeApplyRunMaxHpDelta { maxHpDelta = -10 },
                        new UnknownMapEventOutcomeApplyShrineMaxPowerBonus { amount = 1 },
                    },
                }),
            Row("Leave", new List<UnknownMapEventConditionBase> { new UnknownMapEventConditionAlwaysTrue() }, new UnknownMapEventOutcomeNoOp()),
        };
        return ev;
    }

    static UnknownMapEventSO BuildResonatingFountain2(UnknownMapEventSO requires)
    {
        var ev = ScriptableObject.CreateInstance<UnknownMapEventSO>();
        ev.displayName = "The Resonating Fountain 2";
        ev.description =
            "A pool of liquid mercury-mana sits perfectly still. As you approach, your Resonance Meter begins to hum in sympathy. The water reflects not your face, but the potential of your spells.";
        ev.visibilityConditions = new List<UnknownMapEventConditionBase>
        {
            new UnknownMapEventConditionCompletedUnknownEvent { requiredCompletedUnknownEvent = requires },
        };
        ev.choices = new[]
        {
            Row(
                "[Cost: 20 Max HP] Increase Max Power by 1 (Open The Resonating Fountain 3)",
                new List<UnknownMapEventConditionBase> { new UnknownMapEventConditionRunMaxHpMin { minimumMaxHp = 21 } },
                new UnknownMapEventOutcomeComposite
                {
                    steps = new List<UnknownMapEventOutcomeBase>
                    {
                        new UnknownMapEventOutcomeApplyRunMaxHpDelta { maxHpDelta = -20 },
                        new UnknownMapEventOutcomeApplyShrineMaxPowerBonus { amount = 1 },
                    },
                }),
            Row("Leave", new List<UnknownMapEventConditionBase> { new UnknownMapEventConditionAlwaysTrue() }, new UnknownMapEventOutcomeNoOp()),
        };
        return ev;
    }

    static UnknownMapEventSO BuildResonatingFountain3(UnknownMapEventSO requires)
    {
        var ev = ScriptableObject.CreateInstance<UnknownMapEventSO>();
        ev.displayName = "The Resonating Fountain 3";
        ev.description =
            "A pool of liquid mercury-mana sits perfectly still. As you approach, your Resonance Meter begins to hum in sympathy. The water reflects not your face, but the potential of your spells.";
        ev.visibilityConditions = new List<UnknownMapEventConditionBase>
        {
            new UnknownMapEventConditionCompletedUnknownEvent { requiredCompletedUnknownEvent = requires },
        };
        ev.choices = new[]
        {
            Row(
                "[Cost: 40 Max HP] Increase Max Power by 1",
                new List<UnknownMapEventConditionBase> { new UnknownMapEventConditionRunMaxHpMin { minimumMaxHp = 41 } },
                new UnknownMapEventOutcomeComposite
                {
                    steps = new List<UnknownMapEventOutcomeBase>
                    {
                        new UnknownMapEventOutcomeApplyRunMaxHpDelta { maxHpDelta = -40 },
                        new UnknownMapEventOutcomeApplyShrineMaxPowerBonus { amount = 1 },
                    },
                }),
            Row("Leave", new List<UnknownMapEventConditionBase> { new UnknownMapEventConditionAlwaysTrue() }, new UnknownMapEventOutcomeNoOp()),
        };
        return ev;
    }

    static UnknownMapEventSO BuildSplittingPrism()
    {
        var ev = ScriptableObject.CreateInstance<UnknownMapEventSO>();
        ev.displayName = "The Splitting Prism";
        ev.description =
            "You find a floating, multi-faceted crystal that hums with a parasitic frequency. It doesn't create—it copies. By feeding it enough gold, the prism refracts the 'soul' of one of your dice, splitting it into two identical twins.";
        ev.choices = new[]
        {
            Row(
                "[Cost: 100 Coins] Duplicate one of your existing dice.",
                new List<UnknownMapEventConditionBase> { new UnknownMapEventConditionGoldMin { minimumGold = 100 } },
                new UnknownMapEventOutcomeComposite
                {
                    steps = new List<UnknownMapEventOutcomeBase>
                    {
                        new UnknownMapEventOutcomeSpendGold { amount = 100 },
                        new UnknownMapEventOutcomeDuplicateRandomDeckDie(),
                    },
                }),
            Row("Skip", new List<UnknownMapEventConditionBase> { new UnknownMapEventConditionAlwaysTrue() }, new UnknownMapEventOutcomeNoOp()),
        };
        return ev;
    }

    static UnknownMapEventSO BuildStarlightBasin()
    {
        var ev = ScriptableObject.CreateInstance<UnknownMapEventSO>();
        ev.displayName = "The Starlight Basin";
        ev.description =
            "Tucked away in a silent alcove is a pool of liquid mana that hasn't yet been curdled by Merlin’s corruption. It smells of ozone and fresh rain. As you submerge your wounds, the jagged tears in your physical form knit back together.";
        ev.choices = new[]
        {
            Row("Heal to Full HP", new List<UnknownMapEventConditionBase> { new UnknownMapEventConditionAlwaysTrue() }, new UnknownMapEventOutcomeHealRunToFull()),
            Row("Add 10 Max Health", new List<UnknownMapEventConditionBase> { new UnknownMapEventConditionAlwaysTrue() }, new UnknownMapEventOutcomeApplyRunMaxHpIncreaseAndHeal { amount = 10 }),
        };
        return ev;
    }

    static UnknownMapEventSO BuildCatalystOfSacrifice()
    {
        var ev = ScriptableObject.CreateInstance<UnknownMapEventSO>();
        ev.displayName = "The Catalyst of Sacrifice";
        ev.description =
            "An altar made of blackened iron sits waiting for a tribute. It is a hungry machine designed to convert physical conduits into raw internal capacity. By crushing one of your precious dice in its maw, you absorb its crystalline essence.";
        ev.choices = new[]
        {
            Row(
                "[Cost: 1 Die] Permanently add +5 to your Resonance Peak (X).",
                new List<UnknownMapEventConditionBase> { new UnknownMapEventConditionDeckDiceCountMin { minimumCount = 2 } },
                new UnknownMapEventOutcomeAfterDieChoice
                {
                    steps = new List<UnknownMapEventOutcomeBase>
                    {
                        new UnknownMapEventOutcomeRemoveChosenDeckDie(),
                        new UnknownMapEventOutcomeApplyShrineMaxPowerBonus { amount = 5 },
                    },
                }),
            Row("Skip", new List<UnknownMapEventConditionBase> { new UnknownMapEventConditionAlwaysTrue() }, new UnknownMapEventOutcomeNoOp()),
        };
        return ev;
    }

    static UnknownMapEventSO BuildScouredObelisk(DieFaceSO atk, DieFaceSO def, DieFaceSO fire)
    {
        var ev = ScriptableObject.CreateInstance<UnknownMapEventSO>();
        ev.displayName = "The Scoured Obelisk";
        ev.description =
            "A pillar of white marble stands incongruously amidst the gloom, vibrating with a high-pitched, cleansing frequency. It is a 'Reset' point a remnant of Merlin’s early attempts to keep his reality perfectly sterile. As you place your die against the cold stone, the jagged, purple corruption of the Curse begins to flake off like dry mud, revealing the smooth, stable mana-surface beneath. The static in your head clears, if only for a moment.";
        ev.choices = new[]
        {
            Row(
                "[Condition: Target 1 Curse Face] Clear a die from 1 curse face and add a base face instead",
                new List<UnknownMapEventConditionBase> { new UnknownMapEventConditionHasAnyCurseFaceOnDeck() },
                new UnknownMapEventOutcomeAfterDieChoice
                {
                    dieFilter = UnknownMapEventDieChoiceFilter.HasCurseFace,
                    steps = new List<UnknownMapEventOutcomeBase>
                    {
                        new UnknownMapEventOutcomeReplaceFirstCurseOnChosenDieWithBaseForDieLine
                        {
                            replacementForDamageDie = atk,
                            replacementForArmorDie = def,
                            replacementForFireDie = fire,
                            replacementForIceDie = fire,
                            replacementForNatureDie = fire,
                        },
                    },
                }),
            Row("Skip", new List<UnknownMapEventConditionBase> { new UnknownMapEventConditionAlwaysTrue() }, new UnknownMapEventOutcomeNoOp()),
        };
        return ev;
    }

    static UnknownMapEventSO BuildPolishingWheel(List<UnknownMapEventGemUpgradePair> pairs)
    {
        var ev = ScriptableObject.CreateInstance<UnknownMapEventSO>();
        ev.displayName = "The Arcanist’s Polishing Wheel";
        ev.description =
            "You stumble upon a mechanical bench laden with fine diamond-dust and specialized grinding tools. It was once used to shape the very gems that power Merlin’s throne, but now it sits abandoned, waiting for someone with enough gold to bribe its ancient gears back to life. You drop your coins into the brass slot, and the machine begins to hum. The facets of your gem are ground to a razor-edge, allowing the mana to flow through it with lethal efficiency. It shines brighter now, hungry for the next roll.";
        ev.choices = new[]
        {
            Row(
                "[Cost: 100 Coins] Upgrade one of your current Gems",
                new List<UnknownMapEventConditionBase>
                {
                    new UnknownMapEventConditionGoldMin { minimumGold = 100 },
                    new UnknownMapEventConditionDeckTotalSocketedGemsMin { minimumGems = 1 },
                },
                new UnknownMapEventOutcomeComposite
                {
                    steps = new List<UnknownMapEventOutcomeBase>
                    {
                        new UnknownMapEventOutcomeSpendGold { amount = 100 },
                        new UnknownMapEventOutcomeUpgradeRandomSocketedGemFromTable { pairs = new List<UnknownMapEventGemUpgradePair>(pairs) },
                    },
                }),
            Row("Skip", new List<UnknownMapEventConditionBase> { new UnknownMapEventConditionAlwaysTrue() }, new UnknownMapEventOutcomeNoOp()),
        };
        return ev;
    }

    static UnknownMapEventSO BuildMerlinsMonument(RelicSO relic, DieFaceSO curse, Sprite art)
    {
        var ev = ScriptableObject.CreateInstance<UnknownMapEventSO>();
        ev.displayName = "Merlin’s Monument";
        ev.description =
            "A statue of the Archmage Merlin standing tall. It is said that anyone who meditate here gains a blessing from Merlin himself.";
        ev.eventArt = art;
        var branch = new UnknownMapEventOutcomeRandomWeightedBranch
        {
            branches = new List<UnknownMapEventRandomBranchSlot>
            {
                new UnknownMapEventRandomBranchSlot { weight = 50, outcome = new UnknownMapEventOutcomeAddRunRelic { relic = relic } },
                new UnknownMapEventRandomBranchSlot
                {
                    weight = 50,
                    outcome = new UnknownMapEventOutcomeAddCurseFaceToRandomDie { curseFace = curse },
                },
            },
        };
        ev.choices = new[]
        {
            Row(
                "[Risk] Try to break the statue - 50% find an Artifact, 50% gain a Face Curse to a random die",
                new List<UnknownMapEventConditionBase> { new UnknownMapEventConditionAlwaysTrue() },
                branch),
            Row("Spit on the Statue and move on", new List<UnknownMapEventConditionBase> { new UnknownMapEventConditionAlwaysTrue() }, new UnknownMapEventOutcomeNoOp()),
        };
        return ev;
    }

    static UnknownMapEventOptionEntry Row(string label, List<UnknownMapEventConditionBase> when, UnknownMapEventOutcomeBase outcome)
    {
        return new UnknownMapEventOptionEntry
        {
            label = label,
            enabledWhen = when,
            registerEventCompletedOnPick = true,
            outcome = outcome,
        };
    }

    const string EmbeddedCsv =
        "Event Name,Condition To show,Description,Condition Option A,Option A,Condition Option B,Option B,Option C\n" +
        "The Altar of Greed,,\"A jagged stone altar, stained with the ink of a thousand discarded runes. It whispers of wealth in exchange for your tools. The air around it smells of copper and regret.\",player.DiceCount > 1,[Cost: 1 Die] Sacrifice a die to gain +150 Gold,TRUE,Leave,\n" +
        "The Cursed Anvil,,\"A heavy anvil made of dark, pulsating obsidian. Hammers made of shadow beat against it in a rhythmic trance. It can forge greatness, but it always demands a dark price in return.\",player.DiceCount > 0,[Penalty: Add 1 Curse] Choose a die and add a Legendary Face of that type.,TRUE,Leave,\n" +
        "The Fossilized D6,,\"In a corner of the vault, you find a massive stone die half-buried in crystalline growth. It vibrates with an ancient, heavy frequency. It isn't dead—it's waiting for a new spark.\",player.GetDiceOfType(DieType.Physical).Count > 0,[Cost: Add 1 Curse] Gain +2 Strength permanently (for the entire run).,player.CurrentHP > 10,[Cost: 10 HP] Gain 150 Coins.,Leave\n" +
        "The Resonating Fountain 1,,\"A pool of liquid mercury-mana sits perfectly still. As you approach, your Resonance Meter begins to hum in sympathy. The water reflects not your face, but the potential of your spells.\",player.MaxHP > 10,[Cost: 10 Max HP] Increase Max Power by 1 (Open The Resonating Fountain 2),TRUE,Leave,\n" +
        "The Resonating Fountain 2,Clear Event: The Resonating Foutain 1,\"A pool of liquid mercury-mana sits perfectly still. As you approach, your Resonance Meter begins to hum in sympathy. The water reflects not your face, but the potential of your spells.\",player.MaxHP > 20,[Cost: 20 Max HP] Increase Max Power by 1 (Open The Resonating Fountain 3),TRUE,Leave,\n" +
        "The Resonating Fountain 3,Clear Event: The Resonating Foutain 2,\"A pool of liquid mercury-mana sits perfectly still. As you approach, your Resonance Meter begins to hum in sympathy. The water reflects not your face, but the potential of your spells.\",player.MaxHP > 40,[Cost: 40 Max HP] Increase Max Power by 1,TRUE,Leave,\n" +
        "The Splitting Prism,,\"You find a floating, multi-faceted crystal that hums with a parasitic frequency. It doesn't create—it copies. By feeding it enough gold, the prism refracts the 'soul' of one of your dice, splitting it into two identical twins.\",player.Gold >= 100,[Cost: 100 Coins] Duplicate one of your existing dice.,TRUE,Skip,\n" +
        "The Starlight Basin,,\"Tucked away in a silent alcove is a pool of liquid mana that hasn't yet been curdled by Merlin’s corruption. It smells of ozone and fresh rain. As you submerge your wounds, the jagged tears in your physical form knit back together.\",TRUE,Heal to Full HP,TRUE,Add 10 Max Health,\n" +
        "The Catalyst of Sacrifice,,\"An altar made of blackened iron sits waiting for a tribute. It is a hungry machine designed to convert physical conduits into raw internal capacity. By crushing one of your precious dice in its maw, you absorb its crystalline essence.\",player.DiceCount > 1,[Cost: 1 Die] Permanently add +5 to your Resonance Peak (X).,TRUE,Skip,\n" +
        "The Scoured Obelisk,,\"A pillar of white marble stands incongruously amidst the gloom, vibrating with a high-pitched, cleansing frequency. It is a 'Reset' point a remnant of Merlin’s early attempts to keep his reality perfectly sterile. As you place your die against the cold stone, the jagged, purple corruption of the Curse begins to flake off like dry mud, revealing the smooth, stable mana-surface beneath. The static in your head clears, if only for a moment.\",player.HasCurse(),[Condition: Target 1 Curse Face] Clear a die from 1 curse face and add a base face instead,TRUE,Skip,\n" +
        "The Arcanist’s Polishing Wheel,,\"You stumble upon a mechanical bench laden with fine diamond-dust and specialized grinding tools. It was once used to shape the very gems that power Merlin’s throne, but now it sits abandoned, waiting for someone with enough gold to bribe its ancient gears back to life. You drop your coins into the brass slot, and the machine begins to hum. The facets of your gem are ground to a razor-edge, allowing the mana to flow through it with lethal efficiency. It shines brighter now, hungry for the next roll.\",player.Gold >= 100 && player.TotalGems > 0,[Cost: 100 Coins] Upgrade one of your current Gems,TRUE,Skip,\n" +
        "Merlin’s Monument,,A statue of the Archmage Merlin standing tall. It is said that anyone who meditate here gains a blessing from Merlin himself.,TRUE,\"[Risk] Try to break the statue - 50% find an Artifact, 50% gain a Face Curse to a random die\",TRUE,Spit on the Statue and move on,\n";
}
#endif
