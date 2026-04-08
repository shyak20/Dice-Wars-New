using UnityEngine;

public static class GameActionIconUtility
{
    /// <summary>Icon for flyouts / immediate bar: status index for apply, else action index.</summary>
    public static Sprite GetDisplayIcon(IGameAction action)
    {
        if (action == null) return null;
        if (action is ApplyStatusEffectAction apply)
            return apply.ResolveStatusIcon();
        if (action is GameActionWithIcon withIcon)
            return withIcon.ResolveActionIcon();
        return null;
    }
}
