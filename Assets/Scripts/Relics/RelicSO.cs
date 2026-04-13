using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Relic", menuName = "DiceGame/Relics/Relic")]
public class RelicSO : ScriptableObject
{
    public string title;
    [TextArea] public string description;
    public FaceRarity rarity = FaceRarity.Common;
    public Sprite icon;
    [Tooltip("Shop price; if 0, uses rarity-based default from RelicPriceUtility.")]
    [Min(0)] public int shopGoldPrice;

    [Tooltip("When non-zero, the relic bar shows Text BG and this number (e.g. stack counter).")]
    public int barBenefitDisplayValue;

    [Header("Passive actions")]
    [Tooltip("Run in relic phases (see RelicPhases). Use + to add types under RelicGameActionBase only.")]
    [SerializeReference] public List<RelicGameActionBase> actions = new List<RelicGameActionBase>();
}
