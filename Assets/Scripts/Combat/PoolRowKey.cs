using System;
using UnityEngine;

/// <summary>
/// Stable identity for one row in the element pool bar. Die faces use <see cref="FromDieType"/>;
/// actions / status applies use <see cref="Custom"/> (matches across rolls within the turn).
/// </summary>
[Serializable]
public struct PoolRowKey : IEquatable<PoolRowKey>
{
    public const string DieTypePrefix = "DieType.";

    [SerializeField] string stableId;

    public string StableId => stableId ?? string.Empty;

    public static PoolRowKey FromDieType(DieType t) =>
        new PoolRowKey { stableId = DieTypePrefix + t };

    public static PoolRowKey Custom(string id)
    {
        var s = string.IsNullOrWhiteSpace(id) ? "Unknown" : id.Trim();
        return new PoolRowKey { stableId = s };
    }

    /// <summary>Inspector / YAML strings: use enum names like Fire or Damage to share a row with rolled dice of that type.</summary>
    public static PoolRowKey FromInspectorString(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return Custom("Unknown");
        var o = raw.Trim();
        if (Enum.TryParse<DieType>(o, true, out var dt))
            return FromDieType(dt);
        return Custom(o);
    }

    /// <summary>Friendly label for tooltips (strips <see cref="DieTypePrefix"/> when present).</summary>
    public string DisplayLabel
    {
        get
        {
            if (StableId.StartsWith(DieTypePrefix, StringComparison.Ordinal))
                return StableId.Substring(DieTypePrefix.Length);
            return StableId;
        }
    }

    public static bool TryGetDieType(PoolRowKey k, out DieType dt)
    {
        dt = default;
        if (!k.StableId.StartsWith(DieTypePrefix, StringComparison.Ordinal))
            return false;
        return Enum.TryParse(k.StableId.Substring(DieTypePrefix.Length), out dt);
    }

    /// <summary>DieType-backed rows first (Damage→Nature order), then alphabetical for custom keys.</summary>
    public static int Compare(PoolRowKey a, PoolRowKey b)
    {
        int Tier(PoolRowKey k)
        {
            if (TryGetDieType(k, out var dt))
                return (int)dt;
            return 100;
        }

        var ta = Tier(a);
        var tb = Tier(b);
        if (ta != tb) return ta.CompareTo(tb);
        return string.CompareOrdinal(a.StableId, b.StableId);
    }

    public bool Equals(PoolRowKey other) => StableId == other.StableId;

    public override bool Equals(object obj) => obj is PoolRowKey other && Equals(other);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(StableId);

    public override string ToString() => StableId;

    public static bool operator ==(PoolRowKey a, PoolRowKey b) => a.Equals(b);

    public static bool operator !=(PoolRowKey a, PoolRowKey b) => !a.Equals(b);
}
