using System;
using System.Collections.Generic;

/// <summary>
/// Pre-roll odds for Perfect Cast and Cast Overload from uniformly random face outcomes on selected dice.
/// </summary>
public static class RollCastOddsCalculator
{
    public readonly struct Result
    {
        public readonly float PerfectPercent;
        public readonly float BustPercent;

        public Result(float perfectPercent, float bustPercent)
        {
            PerfectPercent = perfectPercent;
            BustPercent = bustPercent;
        }
    }

    public static Result Compute(
        int currentPower,
        int maxPower,
        bool perfectAtMaxMinusOne,
        bool perfectAtMaxPlusOne,
        bool echoSkipsPowerThisBatch,
        IReadOnlyList<DieAssetSO> dice,
        Func<int, int> modifyFaceValue)
    {
        if (dice == null || dice.Count == 0)
            return new Result(0f, 0f);

        if (echoSkipsPowerThisBatch)
        {
            var perfect = IsPerfectPower(currentPower, maxPower, perfectAtMaxMinusOne, perfectAtMaxPlusOne)
                ? 100f
                : 0f;
            var bust = currentPower > maxPower ? 100f : 0f;
            return new Result(perfect, bust);
        }

        var distribution = new Dictionary<int, double> { { 0, 1.0 } };
        for (var d = 0; d < dice.Count; d++)
        {
            var die = dice[d];
            if (die?.faces == null)
                continue;

            var contributions = new List<int>(6);
            for (var i = 0; i < die.faces.Length && i < 6; i++)
            {
                var face = die.faces[i];
                if (face == null)
                    continue;
                contributions.Add(modifyFaceValue != null ? modifyFaceValue(face.value) : face.value);
            }

            if (contributions.Count == 0)
                continue;

            var probPer = 1.0 / contributions.Count;
            var next = new Dictionary<int, double>();
            foreach (var kv in distribution)
            {
                for (var c = 0; c < contributions.Count; c++)
                {
                    var sum = kv.Key + contributions[c];
                    next[sum] = next.GetValueOrDefault(sum) + kv.Value * probPer;
                }
            }

            distribution = next;
        }

        double perfectProb = 0;
        double bustProb = 0;
        foreach (var kv in distribution)
        {
            var totalPower = currentPower + kv.Key;
            if (IsPerfectPower(totalPower, maxPower, perfectAtMaxMinusOne, perfectAtMaxPlusOne))
                perfectProb += kv.Value;
            if (totalPower > maxPower)
                bustProb += kv.Value;
        }

        return new Result((float)(perfectProb * 100.0), (float)(bustProb * 100.0));
    }

    static bool IsPerfectPower(int totalPower, int maxPower, bool perfectAtMaxMinusOne, bool perfectAtMaxPlusOne) =>
        totalPower == maxPower
        || (perfectAtMaxMinusOne && maxPower > 1 && totalPower == maxPower - 1)
        || (perfectAtMaxPlusOne && totalPower == maxPower + 1);
}
