using System;
using UnityEngine;

/// <summary>
/// Marker action: after this batch settles, the player may pick one physical die to rethrow (see <see cref="CombatManager"/> + <see cref="RerollDieSelectionController"/>).
/// </summary>
[Serializable]
public class RerollDieAction : GameActionWithIcon
{
    protected override ActionVisualId VisualKey => ActionVisualId.RerollDie;

    public override void Execute(GameActionContext context)
    {
        // Grants are counted when the face resolves; nothing to run here.
    }
}
