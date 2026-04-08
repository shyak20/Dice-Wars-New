using System;
using UnityEngine;

namespace Effects.GameActions
{
    [Serializable]
    public class MaxHpAction : GameActionWithIcon
    {
        [SerializeField] private int amount = 1;

        protected override ActionVisualId VisualKey => ActionVisualId.MaxHp;

        public override void Execute(GameActionContext context)
        {
            if (GameActionDebug.Enabled)
                Debug.Log($"[MaxHpAction] Add {amount}");

            context.CombatManager.QueueTurnEndAction(ctx =>
            {
                var finalAmount = amount * ctx.CombatManager.GetAppliedMultiplier();
                ctx.Player.AddMaxHP(finalAmount);
                ctx.Player.Heal(finalAmount);
            });
        }
    }
}
