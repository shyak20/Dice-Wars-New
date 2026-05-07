using System;
using UnityEngine;

/// <summary>
/// Icon resolved via <see cref="ActionVisualId"/> and <see cref="GameIconIndexSO"/> (no sprite on the face asset).
/// </summary>
[Serializable]
public abstract class GameActionWithIcon : IGameAction
{
    [SerializeField, Tooltip("If off, this action runs at turn end (before or after physical damage per status timing rules) instead of when the die settles.")]
    private bool activateImmediately = false;

    /// <inheritdoc />
    public bool ActivateImmediately => activateImmediately;

    protected virtual ActionVisualId VisualKey => ActionVisualId.None;

    /// <summary>Key for <see cref="GameIconIndexSO"/> icons and optional tooltip copy.</summary>
    public ActionVisualId GetActionVisualId() => VisualKey;

    public Sprite ResolveActionIcon() =>
        VisualKey == ActionVisualId.None ? null : GameIconCatalog.GetActionIcon(VisualKey);

    public abstract void Execute(GameActionContext context);
}
