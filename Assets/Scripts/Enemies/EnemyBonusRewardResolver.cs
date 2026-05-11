using System.Collections.Generic;
using UnityEngine;

/// <summary>Rolls and grants extra enemy rewards configured on <see cref="EnemyTypeSO"/>.</summary>
public static class EnemyBonusRewardResolver
{
    public static void RollAndGrant(EnemyTypeSO enemy)
    {
        if (enemy == null || enemy.additionalRewardDrops == null || enemy.additionalRewardDrops.Count == 0)
            return;

        foreach (var drop in enemy.additionalRewardDrops)
        {
            if (drop == null) continue;
            var rolls = Mathf.Max(1, drop.rolls);
            for (var i = 0; i < rolls; i++)
            {
                if (Random.value > Mathf.Clamp01(drop.dropChance))
                    continue;
                GrantOne(enemy, drop.pool);
            }
        }
    }

    private static void GrantOne(EnemyTypeSO enemy, EnemyBonusRewardPool pool)
    {
        switch (pool)
        {
            case EnemyBonusRewardPool.DieFaces:
                GrantRandomFace(enemy);
                break;
            case EnemyBonusRewardPool.Dice:
                GrantRandomDie(enemy);
                break;
            case EnemyBonusRewardPool.Relics:
                GrantRandomRelic(enemy);
                break;
            case EnemyBonusRewardPool.Gems:
                GrantRandomGem(enemy);
                break;
        }
    }

    private static void GrantRandomFace(EnemyTypeSO enemy)
    {
        if (enemy.faceRewardPool == null) return;
        var faceRoll = enemy.faceRewardPool.GetRandomRewards(1, null);
        if (faceRoll.Count == 0 || faceRoll[0] == null) return;
        var face = faceRoll[0];

        var data = PlayerDataContainer.Instance != null ? PlayerDataContainer.Instance.RuntimeData : null;
        if (data == null) return;
        var matchingDice = PlayerInventory.GetDiceEligibleForFaceReplacement(data, face);
        if (matchingDice == null || matchingDice.Count == 0)
        {
            Debug.Log($"Enemy bonus reward: rolled face '{face.name}' but no matching die in deck.");
            return;
        }

        var die = matchingDice[Random.Range(0, matchingDice.Count)];
        if (die == null || die.faces == null || die.faces.Length != 6) return;
        var valid = new List<int>(6);
        for (var i = 0; i < die.faces.Length; i++)
        {
            if (SameValueFaceCapUtility.CanReplaceFaceWithoutViolatingCap(die, i, face))
                valid.Add(i);
        }

        if (valid.Count == 0)
        {
            Debug.Log($"Enemy bonus reward: cannot install face '{face.name}' on die '{die.dieName}' — same-value face cap leaves no legal slot.");
            return;
        }

        var idx = valid[Random.Range(0, valid.Count)];
        die.SwapFace(idx, face);
        Debug.Log($"Enemy bonus reward: installed face '{face.name}' into die '{die.dieName}' slot {idx}.");
    }

    private static void GrantRandomDie(EnemyTypeSO enemy)
    {
        if (enemy.dieRewardPool == null) return;
        var roll = enemy.dieRewardPool.GetRandomDice(1, null, 0f, uniqueInBatch: false);
        if (roll.Count == 0 || roll[0] == null) return;
        VictoryRewardBuffer.PendingDice.Add(roll[0]);
        Debug.Log($"Enemy bonus reward: queued die '{roll[0].name}' for win-stage collection.");
    }

    private static void GrantRandomRelic(EnemyTypeSO enemy)
    {
        if (enemy.relicRewardPool == null) return;
        var roll = enemy.relicRewardPool.GetRandomRelics(1);
        if (roll.Count == 0 || roll[0] == null) return;
        VictoryRewardBuffer.PendingRelics.Add(roll[0]);
        Debug.Log($"Enemy bonus reward: queued relic '{roll[0].title}' for win-stage collection.");
    }

    private static void GrantRandomGem(EnemyTypeSO enemy)
    {
        if (enemy.gemRewardPool == null) return;
        var roll = enemy.gemRewardPool.GetRandomGems(1);
        if (roll.Count == 0 || roll[0] == null) return;
        var gem = roll[0];

        var data = PlayerDataContainer.Instance != null ? PlayerDataContainer.Instance.RuntimeData : null;
        var candidates = PlayerInventory.GetDiceWithEmptyGemSocket(data);
        if (candidates == null || candidates.Count == 0)
        {
            Debug.Log($"Enemy bonus reward: rolled gem '{gem.name}' but no free sockets.");
            return;
        }

        VictoryRewardBuffer.PendingGems.Add(gem);
        Debug.Log($"Enemy bonus reward: queued gem '{gem.name}' for win-stage collection.");
    }
}
