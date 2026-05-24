using System;
using UnityEngine;

/// <summary>Deferred: damages the player when the turn is submitted (before enemy physical damage).</summary>
[Serializable]
public class DealPlayerDamageOnSubmitAction : GameActionWithIcon
{
    [SerializeField, Min(0)] private int damage = 1;

    public int Damage => damage;

    protected override ActionVisualId VisualKey => ActionVisualId.DealPlayerDamageOnSubmit;

    public override void Execute(GameActionContext context)
    {
        if (context?.Player == null || damage <= 0)
            return;

        context.Player.TakeDamage(damage, PlayerDamageSource.CurseFace);

        if (GameActionDebug.Enabled)
            Debug.Log($"[DealPlayerDamageOnSubmit] Player took {damage} damage.");
    }
}
