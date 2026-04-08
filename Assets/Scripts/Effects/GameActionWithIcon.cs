using System;
using UnityEngine;

/// <summary>
/// Icon resolved via <see cref="ActionVisualId"/> and <see cref="GameIconIndexSO"/> (no sprite on the face asset).
/// </summary>
[Serializable]
public abstract class GameActionWithIcon : IGameAction
{
    protected virtual ActionVisualId VisualKey => ActionVisualId.None;

    public Sprite ResolveActionIcon() =>
        VisualKey == ActionVisualId.None ? null : GameIconCatalog.GetActionIcon(VisualKey);

    public abstract void Execute(GameActionContext context);
}
