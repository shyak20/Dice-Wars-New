using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ProgressionCatalog", menuName = "DiceGame/Progression/Progression Catalog")]
public class ProgressionCatalogSO : ScriptableObject
{
    [Tooltip("Ordered rank ladder (Novice = 0, etc.). Assign this catalog on the owning PlayerDataSO.progressionCatalog.")]
    public List<PlayerRankSO> ranks = new List<PlayerRankSO>();

    [Tooltip("Face/gem/relic/die ids always eligible in loot pools even before horizontal unlocks.")]
    public List<string> baseAlwaysAvailableIds = new List<string>();

    public bool TryGetRank(int rankIndex, out PlayerRankSO rank)
    {
        rank = null;
        if (ranks == null)
            return false;

        for (var i = 0; i < ranks.Count; i++)
        {
            var r = ranks[i];
            if (r != null && r.rankIndex == rankIndex)
            {
                rank = r;
                return true;
            }
        }

        return false;
    }

    public PlayerRankSO GetRankOrNull(int rankIndex)
    {
        TryGetRank(rankIndex, out var rank);
        return rank;
    }

    public bool TryGetNextRank(int currentRankIndex, out PlayerRankSO next)
    {
        next = null;
        var best = int.MaxValue;
        PlayerRankSO candidate = null;

        if (ranks == null)
            return false;

        for (var i = 0; i < ranks.Count; i++)
        {
            var r = ranks[i];
            if (r == null || r.rankIndex <= currentRankIndex)
                continue;
            if (r.rankIndex < best)
            {
                best = r.rankIndex;
                candidate = r;
            }
        }

        if (candidate == null)
            return false;

        next = candidate;
        return true;
    }

    public int MaxRankIndex()
    {
        var max = 0;
        if (ranks == null)
            return 0;

        for (var i = 0; i < ranks.Count; i++)
        {
            if (ranks[i] != null)
                max = Mathf.Max(max, ranks[i].rankIndex);
        }

        return max;
    }

    public bool IsAlwaysAvailable(string contentId)
    {
        if (ProgressionContentIds.IsNullOrEmpty(contentId) || baseAlwaysAvailableIds == null)
            return false;

        for (var i = 0; i < baseAlwaysAvailableIds.Count; i++)
        {
            if (string.Equals(baseAlwaysAvailableIds[i], contentId, System.StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    void OnValidate()
    {
        if (ranks == null)
            return;

        ranks.Sort((a, b) =>
        {
            if (a == null && b == null) return 0;
            if (a == null) return 1;
            if (b == null) return -1;
            return a.rankIndex.CompareTo(b.rankIndex);
        });

        ValidateUniqueTrialIds(logAsError: false);
    }

    /// <summary>Each <see cref="PlayerTrialSO.trialID"/> must be unique across the whole catalog (save keys use trialID, not type).</summary>
    public bool ValidateUniqueTrialIds(bool logAsError = true)
    {
        var seen = new Dictionary<string, PlayerTrialSO>(StringComparer.Ordinal);
        var valid = true;

        if (ranks == null)
            return true;

        for (var r = 0; r < ranks.Count; r++)
        {
            var rank = ranks[r];
            if (rank?.associatedTrials == null)
                continue;

            for (var t = 0; t < rank.associatedTrials.Count; t++)
            {
                var trial = rank.associatedTrials[t];
                if (trial == null)
                    continue;

                var id = trial.TrialId;
                if (ProgressionContentIds.IsNullOrEmpty(id))
                {
                    valid = false;
                    LogTrialValidation(
                        logAsError,
                        $"ProgressionCatalog '{name}': trial asset '{trial.name}' has an empty trialID.",
                        trial);
                    continue;
                }

                if (seen.TryGetValue(id, out var other) && other != trial)
                {
                    valid = false;
                    LogTrialValidation(
                        logAsError,
                        $"ProgressionCatalog '{name}': duplicate trialID '{id}' on '{trial.name}' and '{other.name}'. " +
                        "Each trial needs a unique trialID or completion will carry over between trials.",
                        trial);
                }
                else
                {
                    seen[id] = trial;
                }
            }
        }

        return valid;
    }

    static void LogTrialValidation(bool asError, string message, PlayerTrialSO trial)
    {
        if (asError)
            Debug.LogError(message, trial);
        else
            Debug.LogWarning(message, trial);
    }
}
