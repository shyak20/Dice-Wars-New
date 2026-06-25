using System;
using UnityEngine;

/// <summary>
/// Marker: after every die in the roll batch has gathered and submitted its outcomes (flyouts / assignment),
/// all <b>other</b> dice in the batch are physics-rerolled and resolved again. Their first resolve still submits
/// actions to pools but does not add Cast Power; the rerolled resolve adds power and activates again normally.
/// </summary>
[Serializable]
public class RerollOtherDiceAfterAllSettledAction : GameActionWithIcon
{
    [Tooltip("Optional world projectile that flies from this die to each rerolled target before physics reroll.")]
    [SerializeField] private GameObject dieToDieProjectilePrefab;

    public GameObject DieToDieProjectilePrefab => dieToDieProjectilePrefab;

    protected override ActionVisualId VisualKey => ActionVisualId.RerollOtherDice;

    public override void Execute(GameActionContext context)
    {
    }
}
