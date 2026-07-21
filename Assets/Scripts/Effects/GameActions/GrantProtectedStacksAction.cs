using System;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Grants Protected stacks to the acting enemy (blocks the next player-caused debuff applications on arrival).
/// Used by enemy intents and by <see cref="EnemyTypeSO.startingBuffActions"/> on appear.
/// </summary>
[Serializable]
public class GrantProtectedStacksAction : GameActionWithIcon
{
    [SerializeField, Min(1)] private int stacks = 1;
    [SerializeField, FormerlySerializedAs("expireAtEndOfEnemyTurn"),
     Tooltip("If on, these stacks are removed at the start of this enemy's next turn, before it acts.")]
    private bool expireAtStartOfEnemyTurn;

    public int Stacks => stacks;
    public bool ExpireAtStartOfEnemyTurn => expireAtStartOfEnemyTurn;

    protected override ActionVisualId VisualKey => ActionVisualId.GrantProtected;

    public override void Execute(GameActionContext context)
    {
        if (context?.Enemy == null)
        {
            Debug.LogError($"{nameof(GrantProtectedStacksAction)}: missing Enemy on context.");
            return;
        }

        if (stacks <= 0)
        {
            Debug.LogError($"{nameof(GrantProtectedStacksAction)}: stacks must be at least 1.");
            return;
        }

        context.Enemy.AddProtectedStacks(stacks, expireAtStartOfEnemyTurn);

        if (GameActionDebug.Enabled)
            Debug.Log(
                $"[GrantProtected] {context.Enemy.enemyData?.enemyName} gained {stacks} Protected stack(s)" +
                (expireAtStartOfEnemyTurn ? " (expire start of enemy turn)." : "."));
    }
}
