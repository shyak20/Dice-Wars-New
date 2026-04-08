using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class FirstRollAction : GameActionWithIcon
{
    [SerializeField] private int bonusDamage;
    [SerializeField] private int bonusArmor = 8;
    [SerializeReference] private List<IGameAction> actions = new List<IGameAction>();

    protected override ActionVisualId VisualKey => ActionVisualId.FirstRollAction;

    public override void Execute(GameActionContext context)
    {
        if (context.CombatManager == null) return;
        if (!context.CombatManager.IsResolvingFirstRollOfTurn()) return;

        if (bonusDamage > 0)
            context.CombatManager.AddBonusDamageFromAction(bonusDamage);

        if (bonusArmor > 0)
            context.CombatManager.AddBonusArmorFromAction(bonusArmor);

        if (actions != null)
        {
            foreach (var action in actions)
            {
                if (action == null || action is FaceResolveModifierBase || ReferenceEquals(action, this)) continue;
                action.Execute(context);
            }
        }

        if (GameActionDebug.Enabled)
            Debug.Log($"[FirstRollAction] Triggered first-roll bonuses (damage +{bonusDamage}, armor +{bonusArmor}, nested actions: {(actions != null ? actions.Count : 0)})");
    }
}
