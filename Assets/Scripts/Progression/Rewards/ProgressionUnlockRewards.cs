using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class ProgressionUnlockFacesReward : ProgressionRewardBase
{
    public List<DieFaceSO> faces = new List<DieFaceSO>();

    public override void Apply(ProgressionRewardApplyContext context)
    {
        if (context?.Save == null || faces == null)
            return;

        for (var i = 0; i < faces.Count; i++)
        {
            var face = faces[i];
            if (face == null)
                continue;
            ProgressionRewardRegistry.RegisterUnlock(context.Save, ProgressionContentIds.ForFace(face));
        }
    }
}

[Serializable]
public sealed class ProgressionUnlockGemsReward : ProgressionRewardBase
{
    public List<GemSO> gems = new List<GemSO>();

    public override void Apply(ProgressionRewardApplyContext context)
    {
        if (context?.Save == null || gems == null)
            return;

        for (var i = 0; i < gems.Count; i++)
        {
            var gem = gems[i];
            if (gem == null)
                continue;
            ProgressionRewardRegistry.RegisterUnlock(context.Save, ProgressionContentIds.ForGem(gem));
        }
    }
}

[Serializable]
public sealed class ProgressionUnlockRelicsReward : ProgressionRewardBase
{
    public List<RelicSO> relics = new List<RelicSO>();

    public override void Apply(ProgressionRewardApplyContext context)
    {
        if (context?.Save == null || relics == null)
            return;

        for (var i = 0; i < relics.Count; i++)
        {
            var relic = relics[i];
            if (relic == null)
                continue;
            ProgressionRewardRegistry.RegisterUnlock(context.Save, ProgressionContentIds.ForRelic(relic));
        }
    }
}

[Serializable]
public sealed class ProgressionAddStartingDieReward : ProgressionRewardBase
{
    [Tooltip("Die added to the character's PlayerDataSO.currentDeck when this reward is granted.")]
    public DieAssetSO die;

    public override void Apply(ProgressionRewardApplyContext context)
    {
        if (context?.Save == null || die == null)
            return;

        ProgressionStartingDiceUtility.ApplyAddedDie(
            context.Save, die, context.CharacterTemplate, context.Catalog);
    }
}

/// <summary>Legacy reward type — use <see cref="ProgressionAddStartingDieReward"/> instead.</summary>
[Serializable]
public sealed class ProgressionUnlockDiceReward : ProgressionRewardBase
{
    public List<DieAssetSO> dice = new List<DieAssetSO>();

    public override void Apply(ProgressionRewardApplyContext context)
    {
        if (context?.Save == null || dice == null)
            return;

        for (var i = 0; i < dice.Count; i++)
            ProgressionStartingDiceUtility.ApplyAddedDie(
                context.Save, dice[i], context.CharacterTemplate, context.Catalog);
    }
}
