using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Serializable set of die face values (1–6) for roll-triggered bonuses.
/// Used on <see cref="AddValueBasedOnRollAction"/> and relics so lists draw correctly inside <see cref="SerializeReference"/> actions.
/// </summary>
[Serializable]
public class FaceValueMatchSet
{
    [SerializeField] private List<int> values = new List<int>();

    public IReadOnlyList<int> Values
    {
        get
        {
            EnsureInitialized();
            return values;
        }
    }

    public bool IsEmpty
    {
        get
        {
            EnsureInitialized();
            return values.Count == 0;
        }
    }

    public bool Matches(int faceValue)
    {
        EnsureInitialized();
        for (var i = 0; i < values.Count; i++)
        {
            if (values[i] == faceValue)
                return true;
        }

        return false;
    }

    public void EnsureInitialized()
    {
        values ??= new List<int>();
    }

    public void CopyTo(List<int> destination)
    {
        if (destination == null)
            throw new ArgumentNullException(nameof(destination));

        destination.Clear();
        EnsureInitialized();
        destination.AddRange(values);
    }

    public void MigrateLegacySingleValue(int legacy)
    {
        if (legacy == 0)
            return;

        EnsureInitialized();
        if (!values.Contains(legacy))
            values.Add(legacy);
    }

    public void MigrateLegacyIntArray(int[] legacy)
    {
        if (legacy == null || legacy.Length == 0)
            return;

        EnsureInitialized();
        for (var i = 0; i < legacy.Length; i++)
        {
            var v = legacy[i];
            if (v != 0 && !values.Contains(v))
                values.Add(v);
        }
    }

    public static bool MatchesAny(int faceValue, FaceValueMatchSet set, bool matchAnyFaceValue)
    {
        if (matchAnyFaceValue)
            return true;
        return set != null && set.Matches(faceValue);
    }

    public static FaceValueMatchSet FromValues(params int[] source)
    {
        var set = new FaceValueMatchSet();
        set.MigrateLegacyIntArray(source);
        return set;
    }

    public static bool MatchesAny(int faceValue, IReadOnlyList<int> values, bool matchAnyFaceValue)
    {
        if (matchAnyFaceValue)
            return true;
        if (values == null || values.Count == 0)
            return false;
        for (var i = 0; i < values.Count; i++)
        {
            if (values[i] == faceValue)
                return true;
        }

        return false;
    }
}
