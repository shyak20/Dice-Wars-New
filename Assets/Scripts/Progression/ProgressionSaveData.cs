using System;
using System.Collections.Generic;

[Serializable]
public struct TrialSaveData
{
    public string trialID;
    public int currentValue;
    public bool isCompleted;
}

[Serializable]
public struct TrialTypeLifetimeCounter
{
    public TrialType type;
    public int exactRollValue;
    public int accumulatedValue;
}

/// <summary>One progression grant of an extra starting die, stored in PlayerPrefs (not on the PlayerDataSO asset).</summary>
[Serializable]
public struct GrantedStartingDieSaveEntry
{
    public string dieAssetId;
    public DieType dieType;
}

[Serializable]
public class ProgressionProfileSaveData
{
    public int saveVersion = 6;
    public int currentRankIndex;
    public List<TrialSaveData> activeTrialStates = new List<TrialSaveData>();
    /// <summary>Lifetime totals per <see cref="TrialType"/> (and exact roll value for <see cref="TrialType.ExactRoll"/>).</summary>
    public List<TrialTypeLifetimeCounter> lifetimeTrialCounters = new List<TrialTypeLifetimeCounter>();
    public List<string> unlockedContentIDs = new List<string>();
    /// <summary>Extra starting dice from trial/rank rewards. Cleared when progression PlayerPrefs are deleted.</summary>
    public List<GrantedStartingDieSaveEntry> grantedStartingDice = new List<GrantedStartingDieSaveEntry>();
    [Obsolete("Migrated to grantedStartingDice. Do not write.")]
    public List<string> addedStartingDieIds = new List<string>();
    [Obsolete("Grants are no longer baked into PlayerDataSO.currentDeck.")]
    public int grantedDiceDeckEntriesApplied;
    public List<string> completedTrialIDs = new List<string>();
    /// <summary>Trials completed in gameplay but not yet acknowledged on the Dice Select celebration popups.</summary>
    public List<string> unacknowledgedTrialIds = new List<string>();
    /// <summary>All trials on the current rank are done; rank-up is deferred until the Dice Select level-up popup is dismissed.</summary>
    public bool pendingRankUpCelebration;
    /// <summary>Highest rank index whose <c>rankUpRewards</c> have been applied (-1 = none).</summary>
    public int rankUpRewardsAppliedThroughRankIndex = -1;
    /// <summary>Legacy grant copies were removed from <see cref="PlayerDataSO.currentDeck"/> on the asset.</summary>
    public bool legacyTemplateGrantsStripped;
}
