using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Relic", menuName = "DiceGame/Relics/Relic")]
public class RelicSO : ScriptableObject
{
    public string title;
    [TextArea] public string description;
    public FaceRarity rarity = FaceRarity.Common;
    public Sprite icon;

    [Tooltip("When non-zero, the relic bar shows Text BG and this number (e.g. stack counter).")]
    public int barBenefitDisplayValue;

    [Header("Hero availability")]
    [Tooltip("When unset, any hero can draft this relic. When set, only this hero's runs can roll or receive it.")]
    [SerializeField] private PlayerDataSO exclusiveHero;

    public PlayerDataSO ExclusiveHero => exclusiveHero;
    public bool IsGenericRelic => exclusiveHero == null;

    [Header("Passive actions")]
    [Tooltip("Run in relic phases (see RelicPhases). Use + to add types under RelicGameActionBase only.")]
    [SerializeReference] public List<RelicGameActionBase> actions = new List<RelicGameActionBase>();
}
