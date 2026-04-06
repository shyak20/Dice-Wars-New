using System;

/// <summary>
/// Semantic element for socketing faces onto dice. Maps 1:1 with <see cref="DieType"/> (Physical = Damage, Defense = Armor).
/// </summary>
public enum ElementType
{
    Physical,
    Defense,
    Fire,
    Ice,
    Nature
}

public static class ElementTypeExtensions
{
    public static ElementType FromDieType(DieType t)
    {
        switch (t)
        {
            case DieType.Damage: return ElementType.Physical;
            case DieType.Armor: return ElementType.Defense;
            case DieType.Fire: return ElementType.Fire;
            case DieType.Ice: return ElementType.Ice;
            case DieType.Nature: return ElementType.Nature;
            default: throw new ArgumentOutOfRangeException(nameof(t), t, null);
        }
    }

    public static DieType ToDieType(ElementType e)
    {
        switch (e)
        {
            case ElementType.Physical: return DieType.Damage;
            case ElementType.Defense: return DieType.Armor;
            case ElementType.Fire: return DieType.Fire;
            case ElementType.Ice: return DieType.Ice;
            case ElementType.Nature: return DieType.Nature;
            default: throw new ArgumentOutOfRangeException(nameof(e), e, null);
        }
    }

    public static bool MatchesDie(this DieFaceSO face, DieAssetSO die)
    {
        if (face == null || die == null) return false;
        return FromDieType(die.dieType) == FromDieType(face.type);
    }
}
