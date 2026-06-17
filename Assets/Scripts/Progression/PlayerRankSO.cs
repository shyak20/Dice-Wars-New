using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewPlayerRank", menuName = "DiceGame/Progression/Player Rank")]
public class PlayerRankSO : ScriptableObject
{
    [Min(0)] public int rankIndex;
    public string rankName;
    [TextArea(2, 4)] public string rankFlavorText;

    [Header("Portrait")]
    [Tooltip("Large portrait (dice-select preview, rank-up popup).")]
    [SerializeField] private Sprite portrait;
    [Tooltip("Compact portrait (character buttons, fight HUD). Falls back to Portrait when unset.")]
    [SerializeField] private Sprite smallPortrait;

    public Sprite Portrait => portrait;
    public Sprite SmallPortrait => smallPortrait != null ? smallPortrait : portrait;

    public List<PlayerTrialSO> associatedTrials = new List<PlayerTrialSO>();

    [Header("Rank-up rewards")]
    [Tooltip("Granted when all trials on this rank are complete. Use + to add typed rewards (stat bonus, unlock faces, etc.).")]
    [SerializeReference] public List<ProgressionRewardBase> rankUpRewards = new List<ProgressionRewardBase>();

    void OnValidate()
    {
        if (rankIndex < 0)
            rankIndex = 0;

        if (string.IsNullOrWhiteSpace(rankName))
            rankName = name;
    }
}
