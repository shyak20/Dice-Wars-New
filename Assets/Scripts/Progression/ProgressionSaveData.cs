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

[Serializable]
public class ProgressionProfileSaveData
{
    public int saveVersion = 5;
    public int currentRankIndex;
    public List<TrialSaveData> activeTrialStates = new List<TrialSaveData>();
    /// <summary>Lifetime totals per <see cref="TrialType"/> (and exact roll value for <see cref="TrialType.ExactRoll"/>).</summary>
    public List<TrialTypeLifetimeCounter> lifetimeTrialCounters = new List<TrialTypeLifetimeCounter>();
    public List<string> unlockedContentIDs = new List<string>();
    /// <summary>One entry per <see cref="ProgressionAddStartingDieReward"/> grant (die asset name). Duplicate names mean multiple copies.</summary>
    public List<string> addedStartingDieIds = new List<string>();
    /// <summary>How many <see cref="addedStartingDieIds"/> entries are already reflected on <see cref="PlayerDataSO.currentDeck"/>.</summary>
    public int grantedDiceDeckEntriesApplied;
    public List<string> completedTrialIDs = new List<string>();
    /// <summary>Trials completed in gameplay but not yet acknowledged on the Dice Select celebration popups.</summary>
    public List<string> unacknowledgedTrialIds = new List<string>();
    /// <summary>All trials on the current rank are done; rank-up is deferred until the Dice Select level-up popup is dismissed.</summary>
    public bool pendingRankUpCelebration;
    /// <summary>Highest rank index whose <c>rankUpRewards</c> have been applied (-1 = none).</summary>
    public int rankUpRewardsAppliedThroughRankIndex = -1;
}
