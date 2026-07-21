using System;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Grants Ghostwalk stacks to the acting enemy (blocks the next physical-damage arrivals).
/// Used by enemy intents and by <see cref="EnemyTypeSO.startingBuffActions"/> on appear.
/// </summary>
[Serializable]
public class GrantGhostwalkStacksAction : GameActionWithIcon
{
    [SerializeField, Min(1)] private int stacks = 1;
    [SerializeField, FormerlySerializedAs("expireAtEndOfEnemyTurn"),
     Tooltip("If on, these stacks are removed at the start of this enemy's next turn, before it acts.")]
    private bool expireAtStartOfEnemyTurn;

    public int Stacks => stacks;
    public bool ExpireAtStartOfEnemyTurn => expireAtStartOfEnemyTurn;

    protected override ActionVisualId VisualKey => ActionVisualId.GrantGhostwalk;

    public override void Execute(GameActionContext context)
    {
        if (context?.Enemy == null)
        {
            Debug.LogError($"{nameof(GrantGhostwalkStacksAction)}: missing Enemy on context.");
            return;
        }

        if (stacks <= 0)
        {
            Debug.LogError($"{nameof(GrantGhostwalkStacksAction)}: stacks must be at least 1.");
            return;
        }

        context.Enemy.AddGhostwalkStacks(stacks, expireAtStartOfEnemyTurn);

        if (GameActionDebug.Enabled)
            Debug.Log(
                $"[GrantGhostwalk] {context.Enemy.enemyData?.enemyName} gained {stacks} Ghostwalk stack(s)" +
                (expireAtStartOfEnemyTurn ? " (expire start of enemy turn)." : "."));
    }
}
