using UnityEngine;

namespace Effects.GameActions
{
    public class MaxHpAction : IGameAction
    {
        [SerializeField] private int amount = 1;

        public void Execute(GameActionContext context)
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