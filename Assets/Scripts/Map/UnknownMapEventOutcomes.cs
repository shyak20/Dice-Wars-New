using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Runtime context for executing <see cref="UnknownMapEventOutcomeBase"/> after the player picks an option.</summary>
public readonly struct UnknownMapEventOutcomeContext
{
    public UnknownMapEventOutcomeContext(
        UnknownMapEventEvaluationContext evaluation,
        UnknownMapEventSO sourceEvent,
        MapGrid combatGrid,
        Vector2Int playerCell,
        int movesTaken,
        DieAssetSO chosenDie = null,
        IUnknownMapEventOutcomeHost host = null)
    {
        Evaluation = evaluation;
        SourceEvent = sourceEvent;
        CombatGrid = combatGrid;
        PlayerCell = playerCell;
        MovesTaken = movesTaken;
        ChosenDie = chosenDie;
        Host = host;
    }

    public UnknownMapEventEvaluationContext Evaluation { get; }
    public UnknownMapEventSO SourceEvent { get; }
    public MapGrid CombatGrid { get; }
    public Vector2Int PlayerCell { get; }
    public int MovesTaken { get; }
    public DieAssetSO ChosenDie { get; }
    internal IUnknownMapEventOutcomeHost Host { get; }

    public UnknownMapEventOutcomeContext WithChosenDie(DieAssetSO die) =>
        new UnknownMapEventOutcomeContext(Evaluation, SourceEvent, CombatGrid, PlayerCell, MovesTaken, die, Host);
}

/// <summary>Map panel hook for outcomes that chain into another unknown event without closing the UI.</summary>
public interface IUnknownMapEventOutcomeHost
{
    void RequestOpenChainedEvent(UnknownMapEventSO nextEvent);
}

/// <summary>Polymorphic effect run when the player selects an unknown event option (SerializeReference on <see cref="UnknownMapEventOptionEntry"/>).</summary>
[Serializable]
public abstract class UnknownMapEventOutcomeBase
{
    public abstract void Execute(UnknownMapEventOutcomeContext ctx);
}

/// <summary>Which deck dice appear in the map unknown-event die picker.</summary>
public enum UnknownMapEventDieChoiceFilter
{
    AnyDeckDie,
    HasCurseFace,
}

/// <summary>
/// Outcome root that opens the die picker on the map panel; child steps run only after <see cref="UnknownMapEventOutcomeContext.ChosenDie"/> is set.
/// </summary>
[Serializable]
public sealed class UnknownMapEventOutcomeAfterDieChoice : UnknownMapEventOutcomeBase
{
    public UnknownMapEventDieChoiceFilter dieFilter = UnknownMapEventDieChoiceFilter.AnyDeckDie;

    [SerializeReference]
    public List<UnknownMapEventOutcomeBase> steps = new List<UnknownMapEventOutcomeBase>();

    public override void Execute(UnknownMapEventOutcomeContext ctx)
    {
        if (ctx.ChosenDie == null)
        {
            Debug.LogError("UnknownMapEventOutcomeAfterDieChoice: ChosenDie is required.");
            return;
        }

        if (steps == null)
            return;
        for (var i = 0; i < steps.Count; i++)
            steps[i]?.Execute(ctx);
    }

    public bool DiePassesFilter(DieAssetSO die)
    {
        if (die == null)
            return false;
        switch (dieFilter)
        {
            case UnknownMapEventDieChoiceFilter.HasCurseFace:
                return UnknownMapEventDieChoiceUtility.DieHasCurseFace(die);
            default:
                return true;
        }
    }
}

static class UnknownMapEventDieChoiceUtility
{
    public static bool OutcomeRequiresDieChoice(UnknownMapEventOutcomeBase outcome) =>
        outcome is UnknownMapEventOutcomeAfterDieChoice;

    public static bool StepAppliesChosenDieFaceSwap(UnknownMapEventOutcomeBase step) =>
        step is UnknownMapEventOutcomeAddCurseFaceToChosenDie
        || step is UnknownMapEventOutcomeReplaceRandomFaceWithRarityOnChosenDie
        || step is UnknownMapEventOutcomeReplaceFirstCurseOnChosenDieWithBaseForDieLine;

    public static bool StepMutatesDeckComposition(UnknownMapEventOutcomeBase step) =>
        step is UnknownMapEventOutcomeDuplicateChosenDeckDie
        || step is UnknownMapEventOutcomeRemoveChosenDeckDie;

    public static bool StepAddsDeckDie(UnknownMapEventOutcomeBase step) =>
        step is UnknownMapEventOutcomeDuplicateChosenDeckDie
        || step is UnknownMapEventOutcomeDuplicateRandomDeckDie;

    public static bool DieHasCurseFace(DieAssetSO die)
    {
        if (die?.faces == null)
            return false;
        for (var slot = 0; slot < die.faces.Length && slot < 6; slot++)
        {
            var face = die.faces[slot];
            if (face != null && face.type == DieType.Curse)
                return true;
        }

        return false;
    }
}

/// <summary>Adds run gold (no world pop-up).</summary>
[Serializable]
public sealed class UnknownMapEventOutcomeGrantGold : UnknownMapEventOutcomeBase
{
    [Min(0)] public int amount;

    public override void Execute(UnknownMapEventOutcomeContext ctx)
    {
        if (amount <= 0)
            return;
        var eco = RunEconomyManager.TryGetRuntime();
        if (eco == null)
        {
            Debug.LogError("UnknownMapEventOutcomeGrantGold: RunEconomyManager missing.");
            return;
        }

        eco.GrantGold(amount, null);
    }
}

/// <summary>Spends run gold when the player can afford it; otherwise logs and does nothing.</summary>
[Serializable]
public sealed class UnknownMapEventOutcomeSpendGold : UnknownMapEventOutcomeBase
{
    [Min(0)] public int amount;

    public override void Execute(UnknownMapEventOutcomeContext ctx)
    {
        if (amount <= 0)
            return;
        var eco = RunEconomyManager.TryGetRuntime();
        if (eco == null)
        {
            Debug.LogError("UnknownMapEventOutcomeSpendGold: RunEconomyManager missing.");
            return;
        }

        if (!eco.TrySpend(amount))
        {
            Debug.LogWarning($"UnknownMapEventOutcomeSpendGold: could not spend {amount} (current {eco.CurrentGold}).");
            return;
        }

        ProgressionEventBridge.NotifyCoinsSpent(amount, ProgressionCoinSpendSource.UnknownMapEvent);
    }
}

/// <summary>Starts a fight using <see cref="UnknownMapEventSO.ResolveEnemyForCombat"/> and this event’s boss flag.</summary>
[Serializable]
public sealed class UnknownMapEventOutcomeStartCombat : UnknownMapEventOutcomeBase
{
    public override void Execute(UnknownMapEventOutcomeContext ctx)
    {
        var rm = RunManager.Instance;
        if (rm == null || !rm.UseMapBasedRun)
        {
            Debug.LogError("UnknownMapEventOutcomeStartCombat: invalid RunManager or not map run.");
            return;
        }

        if (ctx.SourceEvent == null || ctx.CombatGrid == null)
        {
            Debug.LogError("UnknownMapEventOutcomeStartCombat: missing source event or grid.");
            return;
        }

        if (!rm.TryBeginUnknownMapEventCombat(
                ctx.SourceEvent,
                ctx.CombatGrid,
                ctx.PlayerCell,
                ctx.MovesTaken,
                registerCompletedOnEnter: false))
            Debug.LogError("UnknownMapEventOutcomeStartCombat: could not start combat.", ctx.SourceEvent);
    }
}

/// <summary>Same as shrine max-power bonus (map HUD budget).</summary>
[Serializable]
public sealed class UnknownMapEventOutcomeApplyShrineMaxPowerBonus : UnknownMapEventOutcomeBase
{
    [Min(1)] public int amount = 2;

    public override void Execute(UnknownMapEventOutcomeContext ctx)
    {
        RunManager.Instance?.ApplyShrineMaxPowerBonus(amount);
    }
}

/// <summary>Sets current run HP to max (map runs).</summary>
[Serializable]
public sealed class UnknownMapEventOutcomeHealRunToFull : UnknownMapEventOutcomeBase
{
    public override void Execute(UnknownMapEventOutcomeContext ctx)
    {
        RunManager.Instance?.HealRunVitalityToFull();
    }
}

/// <summary>Applies a delta to current run HP (negative = damage; 0 HP ends the run).</summary>
[Serializable]
public sealed class UnknownMapEventOutcomeApplyRunCurrentHpDelta : UnknownMapEventOutcomeBase
{
    public int hpDelta;

    public override void Execute(UnknownMapEventOutcomeContext ctx)
    {
        RunManager.Instance?.ApplyRunCurrentHpDelta(hpDelta);
    }
}

/// <summary>Map run: changes max HP; positive delta also heals current HP by that amount (capped at new max).</summary>
[Serializable]
public sealed class UnknownMapEventOutcomeApplyRunMaxHpDelta : UnknownMapEventOutcomeBase
{
    public int maxHpDelta;

    public override void Execute(UnknownMapEventOutcomeContext ctx)
    {
        RunManager.Instance?.ApplyRunMaxHpDelta(maxHpDelta);
    }
}

/// <summary>Raises run max HP and heals current by the same positive amount.</summary>
[Serializable]
public sealed class UnknownMapEventOutcomeApplyRunMaxHpIncreaseAndHeal : UnknownMapEventOutcomeBase
{
    [Min(1)] public int amount = 1;

    public override void Execute(UnknownMapEventOutcomeContext ctx)
    {
        RunManager.Instance?.ApplyRunMaxHpIncreaseAndHeal(amount);
    }
}

/// <summary>Removes the first die in the deck matching <see cref="DieType"/>.</summary>
[Serializable]
public sealed class UnknownMapEventOutcomeRemoveFirstDeckDieOfType : UnknownMapEventOutcomeBase
{
    public DieType dieType;

    public override void Execute(UnknownMapEventOutcomeContext ctx)
    {
        var pdc = PlayerDataContainer.Instance;
        if (pdc == null)
        {
            Debug.LogError("UnknownMapEventOutcomeRemoveFirstDeckDieOfType: PlayerDataContainer missing.");
            return;
        }

        if (!pdc.TryRemoveFirstDieOfTypeFromDeck(dieType))
            Debug.LogWarning($"UnknownMapEventOutcomeRemoveFirstDeckDieOfType: no die of type {dieType} in deck.");
    }
}

/// <summary>Removes one random die from the run deck.</summary>
[Serializable]
public sealed class UnknownMapEventOutcomeRemoveRandomDeckDie : UnknownMapEventOutcomeBase
{
    public override void Execute(UnknownMapEventOutcomeContext ctx)
    {
        var pdc = PlayerDataContainer.Instance;
        if (pdc == null)
        {
            Debug.LogError("UnknownMapEventOutcomeRemoveRandomDeckDie: PlayerDataContainer missing.");
            return;
        }

        if (!pdc.TryRemoveRandomDieFromDeck())
            Debug.LogWarning("UnknownMapEventOutcomeRemoveRandomDeckDie: deck had no dice to remove.");
    }
}

/// <summary>Weighted random pick: executes exactly one child outcome.</summary>
[Serializable]
public sealed class UnknownMapEventRandomBranchSlot
{
    [Min(0)] public int weight;
    [SerializeReference] public UnknownMapEventOutcomeBase outcome;
}

[Serializable]
public sealed class UnknownMapEventOutcomeRandomWeightedBranch : UnknownMapEventOutcomeBase
{
    [SerializeReference]
    public List<UnknownMapEventRandomBranchSlot> branches = new List<UnknownMapEventRandomBranchSlot>();

    public override void Execute(UnknownMapEventOutcomeContext ctx)
    {
        if (branches == null || branches.Count == 0)
        {
            Debug.LogError("UnknownMapEventOutcomeRandomWeightedBranch: no branches configured.");
            return;
        }

        var total = 0;
        for (var i = 0; i < branches.Count; i++)
        {
            var s = branches[i];
            if (s != null)
                total += Mathf.Max(0, s.weight);
        }

        if (total <= 0)
        {
            Debug.LogError("UnknownMapEventOutcomeRandomWeightedBranch: total weight is 0.");
            return;
        }

        var r = UnityEngine.Random.Range(0, total);
        for (var i = 0; i < branches.Count; i++)
        {
            var s = branches[i];
            if (s == null)
                continue;
            var w = Mathf.Max(0, s.weight);
            if (r < w)
            {
                s.outcome?.Execute(ctx);
                return;
            }

            r -= w;
        }
    }
}

/// <summary>Registers a completed unknown event id (defaults to <see cref="UnknownMapEventSO.ResolvedEventId"/>).</summary>
[Serializable]
public sealed class UnknownMapEventOutcomeMarkEventCompleted : UnknownMapEventOutcomeBase
{
    [Tooltip("Leave empty to use the source event’s ResolvedEventId.")]
    public string explicitEventId;

    public override void Execute(UnknownMapEventOutcomeContext ctx)
    {
        var id = !string.IsNullOrWhiteSpace(explicitEventId)
            ? explicitEventId.Trim()
            : (ctx.SourceEvent != null ? ctx.SourceEvent.ResolvedEventId : string.Empty);
        RunManager.Instance?.RegisterUnknownMapEventCompleted(id);
    }
}

/// <summary>Immediately opens another unknown map event on the active panel (step chain, e.g. Resonating Fountain 1 → 2).</summary>
[Serializable]
public sealed class UnknownMapEventOutcomeOpenUnknownMapEvent : UnknownMapEventOutcomeBase
{
    public UnknownMapEventSO nextEvent;

    public override void Execute(UnknownMapEventOutcomeContext ctx)
    {
        if (nextEvent == null)
        {
            Debug.LogError("UnknownMapEventOutcomeOpenUnknownMapEvent: nextEvent is not assigned.");
            return;
        }

        ctx.Host?.RequestOpenChainedEvent(nextEvent);
    }
}

/// <summary>Runs child outcomes in order.</summary>
[Serializable]
public sealed class UnknownMapEventOutcomeComposite : UnknownMapEventOutcomeBase
{
    [SerializeReference]
    public List<UnknownMapEventOutcomeBase> steps = new List<UnknownMapEventOutcomeBase>();

    public override void Execute(UnknownMapEventOutcomeContext ctx)
    {
        if (steps == null)
            return;
        for (var i = 0; i < steps.Count; i++)
            steps[i]?.Execute(ctx);
    }
}

/// <summary>No gameplay effect (e.g. Leave / dismiss).</summary>
[Serializable]
public sealed class UnknownMapEventOutcomeNoOp : UnknownMapEventOutcomeBase
{
    public override void Execute(UnknownMapEventOutcomeContext ctx) { }
}

/// <summary>Permanent run bonus: each combat start applies this many Strength stacks (see RunManager map strength definition).</summary>
[Serializable]
public sealed class UnknownMapEventOutcomeAddRunPermanentStrengthStacks : UnknownMapEventOutcomeBase
{
    [Min(1)] public int stacks = 1;

    public override void Execute(UnknownMapEventOutcomeContext ctx)
    {
        if (stacks <= 0)
            return;
        RunManager.Instance?.AddRunPermanentStrengthStacks(stacks);
    }
}

/// <summary>Spends a rounded percent of a run currency (see <see cref="UnknownMapEventRunCurrencyUtility"/>).</summary>
[Serializable]
public sealed class UnknownMapEventOutcomeSpendRunCurrencyPercent : UnknownMapEventOutcomeBase
{
    public UnknownMapEventRunCurrency currency = UnknownMapEventRunCurrency.Coins;

    [Range(0f, 100f)]
    public float percent = 50f;

    public override void Execute(UnknownMapEventOutcomeContext ctx)
    {
        if (percent <= 0f)
            return;

        if (!UnknownMapEventRunCurrencyUtility.TrySpendPercent(currency, percent, out var spent))
        {
            Debug.LogWarning(
                $"UnknownMapEventOutcomeSpendRunCurrencyPercent: could not spend {percent}% of {currency} (cost {spent}, current {UnknownMapEventRunCurrencyUtility.GetCurrentAmount(currency)}).");
        }
    }
}

/// <summary>Duplicates <see cref="UnknownMapEventOutcomeContext.ChosenDie"/> in the run deck.</summary>
[Serializable]
public sealed class UnknownMapEventOutcomeDuplicateChosenDeckDie : UnknownMapEventOutcomeBase
{
    public override void Execute(UnknownMapEventOutcomeContext ctx)
    {
        if (ctx.ChosenDie == null)
        {
            Debug.LogError("UnknownMapEventOutcomeDuplicateChosenDeckDie: ChosenDie is required.");
            return;
        }

        var pdc = PlayerDataContainer.Instance;
        if (pdc == null)
        {
            Debug.LogError("UnknownMapEventOutcomeDuplicateChosenDeckDie: PlayerDataContainer missing.");
            return;
        }

        if (pdc.TryDuplicateDeckDie(ctx.ChosenDie) == null)
            Debug.LogWarning("UnknownMapEventOutcomeDuplicateChosenDeckDie: chosen die was not duplicated.");
    }
}

/// <summary>Duplicates one random non-null deck die (runtime clone).</summary>
[Serializable]
public sealed class UnknownMapEventOutcomeDuplicateRandomDeckDie : UnknownMapEventOutcomeBase
{
    public override void Execute(UnknownMapEventOutcomeContext ctx)
    {
        var pdc = PlayerDataContainer.Instance;
        if (pdc == null)
        {
            Debug.LogError("UnknownMapEventOutcomeDuplicateRandomDeckDie: PlayerDataContainer missing.");
            return;
        }

        pdc.TryDuplicateRandomDeckDie();
    }
}

/// <summary>Adds <see cref="curseFace"/> to a random deck die that can socket it (curse matches any die).</summary>
[Serializable]
public sealed class UnknownMapEventOutcomeAddCurseFaceToRandomDie : UnknownMapEventOutcomeBase
{
    public DieFaceSO curseFace;

    public override void Execute(UnknownMapEventOutcomeContext ctx)
    {
        if (curseFace == null)
        {
            Debug.LogError("UnknownMapEventOutcomeAddCurseFaceToRandomDie: curseFace is not assigned.");
            return;
        }

        var pdc = PlayerDataContainer.Instance;
        var data = pdc?.RuntimeData;
        if (data?.currentDeck == null)
        {
            Debug.LogError("UnknownMapEventOutcomeAddCurseFaceToRandomDie: no runtime deck.");
            return;
        }

        var indices = new List<int>();
        for (var i = 0; i < data.currentDeck.Count; i++)
        {
            if (data.currentDeck[i] != null)
                indices.Add(i);
        }

        if (indices.Count == 0)
        {
            Debug.LogWarning("UnknownMapEventOutcomeAddCurseFaceToRandomDie: deck empty.");
            return;
        }

        UnknownMapEventOutcomeShuffle.ShuffleInPlace(indices);
        foreach (var dieIdx in indices)
        {
            var die = data.currentDeck[dieIdx];
            if (die?.faces == null)
                continue;
            var slotOrder = new List<int> { 0, 1, 2, 3, 4, 5 };
            UnknownMapEventOutcomeShuffle.ShuffleInPlace(slotOrder);
            foreach (var slot in slotOrder)
            {
                if (slot < 0 || slot >= die.faces.Length)
                    continue;
                if (!curseFace.MatchesDie(die))
                    continue;
                if (!SameValueFaceCapUtility.CanReplaceFaceWithoutViolatingCap(die, slot, curseFace))
                    continue;
                die.SwapFace(slot, curseFace);
                PlayerDataContainer.NotifyRuntimeDeckChanged();
                return;
            }
        }

        Debug.LogWarning("UnknownMapEventOutcomeAddCurseFaceToRandomDie: no valid face slot found.");
    }
}

/// <summary>Replaces one random face on a random die with a face from <see cref="facesLootTable"/> at <see cref="rarity"/> that matches the die.</summary>
[Serializable]
public sealed class UnknownMapEventOutcomeReplaceRandomFaceWithRarityOnRandomDie : UnknownMapEventOutcomeBase
{
    public FaceLootTableSO facesLootTable;
    public FaceRarity rarity = FaceRarity.Legendary;

    public override void Execute(UnknownMapEventOutcomeContext ctx)
    {
        if (facesLootTable == null)
        {
            Debug.LogError("UnknownMapEventOutcomeReplaceRandomFaceWithRarityOnRandomDie: facesLootTable is not assigned.");
            return;
        }

        var data = PlayerDataContainer.Instance?.RuntimeData;
        if (data?.currentDeck == null)
        {
            Debug.LogError("UnknownMapEventOutcomeReplaceRandomFaceWithRarityOnRandomDie: no deck.");
            return;
        }

        var dieOrder = new List<int>();
        for (var i = 0; i < data.currentDeck.Count; i++)
        {
            if (data.currentDeck[i] != null)
                dieOrder.Add(i);
        }

        UnknownMapEventOutcomeShuffle.ShuffleInPlace(dieOrder);
        foreach (var dIdx in dieOrder)
        {
            var die = data.currentDeck[dIdx];
            if (die?.faces == null)
                continue;

            var candidates = facesLootTable.GetCandidatesForDieAndRarity(die, rarity);
            if (candidates.Count == 0)
                continue;

            var slotOrder = new List<int> { 0, 1, 2, 3, 4, 5 };
            UnknownMapEventOutcomeShuffle.ShuffleInPlace(slotOrder);
            foreach (var slot in slotOrder)
            {
                if (slot < 0 || slot >= die.faces.Length)
                    continue;
                var pick = candidates[UnityEngine.Random.Range(0, candidates.Count)];
                if (!SameValueFaceCapUtility.CanReplaceFaceWithoutViolatingCap(die, slot, pick))
                    continue;
                die.SwapFace(slot, pick);
                PlayerDataContainer.NotifyRuntimeDeckChanged();
                return;
            }
        }

        Debug.LogWarning($"UnknownMapEventOutcomeReplaceRandomFaceWithRarityOnRandomDie: could not place a {rarity} face.");
    }
}

/// <summary>Adds <see cref="curseFace"/> to a random legal slot on <see cref="UnknownMapEventOutcomeContext.ChosenDie"/>.</summary>
[Serializable]
public sealed class UnknownMapEventOutcomeAddCurseFaceToChosenDie : UnknownMapEventOutcomeBase
{
    public DieFaceSO curseFace;

    public override void Execute(UnknownMapEventOutcomeContext ctx)
    {
        if (curseFace == null)
        {
            Debug.LogError("UnknownMapEventOutcomeAddCurseFaceToChosenDie: curseFace is not assigned.");
            return;
        }

        var die = ctx.ChosenDie;
        if (die == null)
        {
            Debug.LogError("UnknownMapEventOutcomeAddCurseFaceToChosenDie: ChosenDie is required.");
            return;
        }

        if (!TrySwapAtRandomSlotInternal(die, curseFace, excludedSlots: null, out _))
            Debug.LogWarning("UnknownMapEventOutcomeAddCurseFaceToChosenDie: no valid face slot found.");
    }

    public bool TrySwapAtSlot(DieAssetSO die, int slot, DieFaceSO face) =>
        TrySwapFaceAtSlot(die, slot, face);

    public bool TrySwapAtRandomSlot(DieAssetSO die, DieFaceSO face, ICollection<int> excludedSlots, out int slotUsed) =>
        TrySwapAtRandomSlotInternal(die, face, excludedSlots, out slotUsed);

    /// <summary>Picks a random legal slot (optionally excluding slots already used this event) and swaps the face.</summary>
    internal static bool TrySwapAtRandomSlotInternal(DieAssetSO die, DieFaceSO face, ICollection<int> excludedSlots, out int slotUsed)
    {
        slotUsed = PickRandomLegalSlot(die, face, excludedSlots);
        if (slotUsed < 0)
            return false;
        return TrySwapFaceAtSlot(die, slotUsed, face);
    }

    internal static bool TrySwapFaceAtSlot(DieAssetSO die, int slot, DieFaceSO face)
    {
        if (die?.faces == null || face == null)
            return false;
        if (slot < 0 || slot >= die.faces.Length)
            return false;
        if (!face.MatchesDie(die))
            return false;
        if (!SameValueFaceCapUtility.CanReplaceFaceWithoutViolatingCap(die, slot, face))
            return false;

        die.SwapFace(slot, face);
        PlayerDataContainer.NotifyRuntimeDeckChanged();
        return true;
    }

    internal static int PickRandomLegalSlot(DieAssetSO die, DieFaceSO face, ICollection<int> excludedSlots = null)
    {
        if (die?.faces == null || face == null)
            return -1;

        var slotOrder = new List<int> { 0, 1, 2, 3, 4, 5 };
        UnknownMapEventOutcomeShuffle.ShuffleInPlace(slotOrder);
        foreach (var slot in slotOrder)
        {
            if (slot < 0 || slot >= die.faces.Length)
                continue;
            if (excludedSlots != null && excludedSlots.Contains(slot))
                continue;
            if (!face.MatchesDie(die))
                continue;
            if (SameValueFaceCapUtility.CanReplaceFaceWithoutViolatingCap(die, slot, face))
                return slot;
        }

        return -1;
    }
}

/// <summary>Replaces one random face on <see cref="UnknownMapEventOutcomeContext.ChosenDie"/> with a face from <see cref="facesLootTable"/> at <see cref="rarity"/> that matches the die.</summary>
[Serializable]
public sealed class UnknownMapEventOutcomeReplaceRandomFaceWithRarityOnChosenDie : UnknownMapEventOutcomeBase
{
    public FaceLootTableSO facesLootTable;
    public FaceRarity rarity = FaceRarity.Legendary;

    public override void Execute(UnknownMapEventOutcomeContext ctx)
    {
        if (facesLootTable == null)
        {
            Debug.LogError("UnknownMapEventOutcomeReplaceRandomFaceWithRarityOnChosenDie: facesLootTable is not assigned.");
            return;
        }

        var die = ctx.ChosenDie;
        if (die == null)
        {
            Debug.LogError("UnknownMapEventOutcomeReplaceRandomFaceWithRarityOnChosenDie: ChosenDie is required.");
            return;
        }

        var pick = PickFaceForDie(die);
        if (pick == null || !UnknownMapEventOutcomeAddCurseFaceToChosenDie.TrySwapAtRandomSlotInternal(die, pick, excludedSlots: null, out _))
            Debug.LogWarning($"UnknownMapEventOutcomeReplaceRandomFaceWithRarityOnChosenDie: could not place a {rarity} face.");
    }

    public DieFaceSO PickFaceForDie(DieAssetSO die) =>
        facesLootTable != null ? facesLootTable.PickRandomForDieAndRarity(die, rarity) : null;

    public bool TrySwapAtSlot(DieAssetSO die, int slot, DieFaceSO face) =>
        UnknownMapEventOutcomeAddCurseFaceToChosenDie.TrySwapFaceAtSlot(die, slot, face);

    public bool TrySwapAtRandomSlot(DieAssetSO die, DieFaceSO face, ICollection<int> excludedSlots, out int slotUsed) =>
        UnknownMapEventOutcomeAddCurseFaceToChosenDie.TrySwapAtRandomSlotInternal(die, face, excludedSlots, out slotUsed);
}

/// <summary>Removes <see cref="UnknownMapEventOutcomeContext.ChosenDie"/> from the run deck.</summary>
[Serializable]
public sealed class UnknownMapEventOutcomeRemoveChosenDeckDie : UnknownMapEventOutcomeBase
{
    public override void Execute(UnknownMapEventOutcomeContext ctx)
    {
        if (ctx.ChosenDie == null)
        {
            Debug.LogError("UnknownMapEventOutcomeRemoveChosenDeckDie: ChosenDie is required.");
            return;
        }

        var pdc = PlayerDataContainer.Instance;
        if (pdc == null)
        {
            Debug.LogError("UnknownMapEventOutcomeRemoveChosenDeckDie: PlayerDataContainer missing.");
            return;
        }

        if (!pdc.TryRemoveDieFromDeck(ctx.ChosenDie))
            Debug.LogWarning("UnknownMapEventOutcomeRemoveChosenDeckDie: die was not in the deck.");
    }
}

/// <summary>Replaces the first curse face on <see cref="UnknownMapEventOutcomeContext.ChosenDie"/> with the base face for that die line.</summary>
[Serializable]
public sealed class UnknownMapEventOutcomeReplaceFirstCurseOnChosenDieWithBaseForDieLine : UnknownMapEventOutcomeBase
{
    public DieFaceSO replacementForDamageDie;
    public DieFaceSO replacementForArmorDie;
    public DieFaceSO replacementForFireDie;
    public DieFaceSO replacementForIceDie;
    public DieFaceSO replacementForNatureDie;

    public override void Execute(UnknownMapEventOutcomeContext ctx)
    {
        var die = ctx.ChosenDie;
        if (die == null)
        {
            Debug.LogError("UnknownMapEventOutcomeReplaceFirstCurseOnChosenDieWithBaseForDieLine: ChosenDie is required.");
            return;
        }

        if (die.faces == null)
            return;

        for (var slot = 0; slot < die.faces.Length && slot < 6; slot++)
        {
            var face = die.faces[slot];
            if (face == null || face.type != DieType.Curse)
                continue;

            var rep = ReplacementFor(die);
            if (rep == null)
            {
                Debug.LogError("UnknownMapEventOutcomeReplaceFirstCurseOnChosenDieWithBaseForDieLine: missing replacement for die type " + die.dieType);
                return;
            }

            if (!rep.MatchesDie(die))
                continue;
            if (!SameValueFaceCapUtility.CanReplaceFaceWithoutViolatingCap(die, slot, rep))
                continue;
            die.SwapFace(slot, rep);
            PlayerDataContainer.NotifyRuntimeDeckChanged();
            return;
        }

        Debug.LogWarning("UnknownMapEventOutcomeReplaceFirstCurseOnChosenDieWithBaseForDieLine: no curse face replaced.");
    }

    /// <summary>Replaces the first legal curse slot on the die (lowest index).</summary>
    public bool TryReplaceFirstCurse(DieAssetSO die, out int slotUsed)
    {
        slotUsed = -1;
        if (die?.faces == null)
            return false;

        for (var slot = 0; slot < die.faces.Length && slot < 6; slot++)
        {
            if (!IsLegalCurseReplacementSlot(die, slot))
                continue;
            if (!TrySwapCurseAtSlot(die, slot))
                continue;
            slotUsed = slot;
            return true;
        }

        return false;
    }

    public bool TrySwapCurseAtSlot(DieAssetSO die, int slot)
    {
        if (die?.faces == null || slot < 0 || slot >= die.faces.Length)
            return false;

        var face = die.faces[slot];
        if (face == null || face.type != DieType.Curse)
            return false;

        var rep = ReplacementFor(die);
        if (rep == null)
        {
            Debug.LogError("UnknownMapEventOutcomeReplaceFirstCurseOnChosenDieWithBaseForDieLine: missing replacement for die type " + die.dieType);
            return false;
        }

        return UnknownMapEventOutcomeAddCurseFaceToChosenDie.TrySwapFaceAtSlot(die, slot, rep);
    }

    public static bool IsCurseSlot(DieAssetSO die, int slot)
    {
        if (die?.faces == null || slot < 0 || slot >= die.faces.Length)
            return false;
        var face = die.faces[slot];
        return face != null && face.type == DieType.Curse;
    }

    public bool IsLegalCurseReplacementSlot(DieAssetSO die, int slot)
    {
        if (!IsCurseSlot(die, slot))
            return false;

        var rep = ReplacementFor(die);
        return rep != null && rep.MatchesDie(die)
            && SameValueFaceCapUtility.CanReplaceFaceWithoutViolatingCap(die, slot, rep);
    }

    DieFaceSO ReplacementFor(DieAssetSO die)
    {
        if (die == null)
            return null;
        return die.dieType switch
        {
            DieType.Damage => replacementForDamageDie,
            DieType.Armor => replacementForArmorDie,
            DieType.Fire => replacementForFireDie,
            DieType.Ice => replacementForIceDie,
            DieType.Nature => replacementForNatureDie,
            _ => replacementForDamageDie,
        };
    }
}

/// <summary>Replaces the first curse-type face found on the deck with <see cref="replacement"/> (must match die line).</summary>
[Serializable]
public sealed class UnknownMapEventOutcomeReplaceFirstCurseFaceWith : UnknownMapEventOutcomeBase
{
    public DieFaceSO replacement;

    public override void Execute(UnknownMapEventOutcomeContext ctx)
    {
        if (replacement == null)
        {
            Debug.LogError("UnknownMapEventOutcomeReplaceFirstCurseFaceWith: replacement is null.");
            return;
        }

        var data = PlayerDataContainer.Instance?.RuntimeData;
        if (data?.currentDeck == null)
        {
            Debug.LogError("UnknownMapEventOutcomeReplaceFirstCurseFaceWith: no deck.");
            return;
        }

        for (var d = 0; d < data.currentDeck.Count; d++)
        {
            var die = data.currentDeck[d];
            if (die?.faces == null)
                continue;
            for (var slot = 0; slot < die.faces.Length && slot < 6; slot++)
            {
                var face = die.faces[slot];
                if (face == null || face.type != DieType.Curse)
                    continue;
                if (!replacement.MatchesDie(die))
                    continue;
                if (!SameValueFaceCapUtility.CanReplaceFaceWithoutViolatingCap(die, slot, replacement))
                    continue;
                die.SwapFace(slot, replacement);
                PlayerDataContainer.NotifyRuntimeDeckChanged();
                return;
            }
        }

        Debug.LogWarning("UnknownMapEventOutcomeReplaceFirstCurseFaceWith: no curse face replaced.");
    }
}

/// <summary>Upgrades one random socketed gem by swapping it for the paired <see cref="to"/> when the socket holds <see cref="from"/>.</summary>
[Serializable]
public sealed class UnknownMapEventGemUpgradePair
{
    public GemSO from;
    public GemSO to;
}

[Serializable]
public sealed class UnknownMapEventOutcomeUpgradeRandomSocketedGemFromTable : UnknownMapEventOutcomeBase
{
    public List<UnknownMapEventGemUpgradePair> pairs = new List<UnknownMapEventGemUpgradePair>();

    public override void Execute(UnknownMapEventOutcomeContext ctx)
    {
        if (pairs == null || pairs.Count == 0)
        {
            Debug.LogError("UnknownMapEventOutcomeUpgradeRandomSocketedGemFromTable: no pairs.");
            return;
        }

        var data = PlayerDataContainer.Instance?.RuntimeData;
        if (data?.currentDeck == null)
        {
            Debug.LogError("UnknownMapEventOutcomeUpgradeRandomSocketedGemFromTable: no deck.");
            return;
        }

        var sockets = new List<(DieAssetSO die, int socket, GemSO current, GemSO upgrade)>();
        for (var d = 0; d < data.currentDeck.Count; d++)
        {
            var die = data.currentDeck[d];
            if (die == null)
                continue;
            for (var s = 0; s < DieAssetSO.GemSocketCount; s++)
            {
                var g = die.GetSocketedGemAt(s);
                if (g == null)
                    continue;
                for (var p = 0; p < pairs.Count; p++)
                {
                    var pair = pairs[p];
                    if (pair?.from == null || pair.to == null)
                        continue;
                    if (pair.from != g)
                        continue;
                    sockets.Add((die, s, g, pair.to));
                    break;
                }
            }
        }

        if (sockets.Count == 0)
        {
            Debug.LogWarning("UnknownMapEventOutcomeUpgradeRandomSocketedGemFromTable: no matching socketed gem.");
            return;
        }

        var pick = sockets[UnityEngine.Random.Range(0, sockets.Count)];
        pick.die.SetSocketedGemForMapEvent(pick.socket, pick.upgrade);
        PlayerDataContainer.NotifyRuntimeDeckChanged();
    }
}

[Serializable]
public sealed class UnknownMapEventOutcomeAddRunRelic : UnknownMapEventOutcomeBase
{
    public RelicSO relic;

    public override void Execute(UnknownMapEventOutcomeContext ctx)
    {
        if (relic == null)
        {
            Debug.LogError("UnknownMapEventOutcomeAddRunRelic: relic is null.");
            return;
        }

        RunManager.Instance?.AddRunRelic(relic);
    }
}

/// <summary>Replaces the first curse face on the deck with a base face chosen from the die’s <see cref="DieType"/> line.</summary>
[Serializable]
public sealed class UnknownMapEventOutcomeReplaceFirstCurseWithBaseForDieLine : UnknownMapEventOutcomeBase
{
    public DieFaceSO replacementForDamageDie;
    public DieFaceSO replacementForArmorDie;
    public DieFaceSO replacementForFireDie;
    public DieFaceSO replacementForIceDie;
    public DieFaceSO replacementForNatureDie;

    public override void Execute(UnknownMapEventOutcomeContext ctx)
    {
        var data = PlayerDataContainer.Instance?.RuntimeData;
        if (data?.currentDeck == null)
        {
            Debug.LogError("UnknownMapEventOutcomeReplaceFirstCurseWithBaseForDieLine: no deck.");
            return;
        }

        for (var d = 0; d < data.currentDeck.Count; d++)
        {
            var die = data.currentDeck[d];
            if (die?.faces == null)
                continue;
            for (var slot = 0; slot < die.faces.Length && slot < 6; slot++)
            {
                var face = die.faces[slot];
                if (face == null || face.type != DieType.Curse)
                    continue;

                var rep = ReplacementFor(die);
                if (rep == null)
                {
                    Debug.LogError("UnknownMapEventOutcomeReplaceFirstCurseWithBaseForDieLine: missing replacement for die type " + die.dieType);
                    return;
                }

                if (!rep.MatchesDie(die))
                    continue;
                if (!SameValueFaceCapUtility.CanReplaceFaceWithoutViolatingCap(die, slot, rep))
                    continue;
                die.SwapFace(slot, rep);
                PlayerDataContainer.NotifyRuntimeDeckChanged();
                return;
            }
        }

        Debug.LogWarning("UnknownMapEventOutcomeReplaceFirstCurseWithBaseForDieLine: no curse face replaced.");
    }

    DieFaceSO ReplacementFor(DieAssetSO die)
    {
        if (die == null)
            return null;
        return die.dieType switch
        {
            DieType.Damage => replacementForDamageDie,
            DieType.Armor => replacementForArmorDie,
            DieType.Fire => replacementForFireDie,
            DieType.Ice => replacementForIceDie,
            DieType.Nature => replacementForNatureDie,
            _ => replacementForDamageDie,
        };
    }
}

static class UnknownMapEventOutcomeShuffle
{
    internal static void ShuffleInPlace<T>(IList<T> list)
    {
        if (list == null || list.Count < 2)
            return;
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = UnityEngine.Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
