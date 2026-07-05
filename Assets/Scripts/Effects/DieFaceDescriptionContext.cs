/// <summary>Optional runtime context for resolving live values in <see cref="DieFaceSO"/> tooltip copy.</summary>
public sealed class DieFaceDescriptionContext
{
    public CombatManager Combat { get; }

    public DieFaceDescriptionContext(CombatManager combat)
    {
        Combat = combat;
    }

    public static DieFaceDescriptionContext FromCombat(CombatManager combat) =>
        combat != null ? new DieFaceDescriptionContext(combat) : null;

    public static DieFaceDescriptionContext ResolveActive()
    {
        var combat = UnityEngine.Object.FindObjectOfType<CombatManager>();
        return FromCombat(combat);
    }
}
