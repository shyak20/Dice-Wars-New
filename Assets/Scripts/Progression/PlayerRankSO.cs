using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewPlayerRank", menuName = "DiceGame/Progression/Player Rank")]
public class PlayerRankSO : ScriptableObject
{
    [Min(0)] public int rankIndex;
    public string rankName;
    [TextArea(2, 4)] public string rankFlavorText;

    [Header("Portrait — large (dice-select preview)")]
    [Tooltip("PSB prefab spawned in dice-select. Must include an Animator with a clip recorded on this rig (not another character's clip).")]
    [SerializeField] private GameObject largePortraitPrefab;
    [Tooltip("Static sprite for compact UI stills (not rank-up popup — that uses Large Portrait Prefab).")]
    [SerializeField] private Sprite portrait;

    [Header("Portrait — small (UI Image)")]
    [Tooltip("Compact portrait for character buttons and fight HUD. Falls back to Portrait when unset.")]
    [SerializeField] private Sprite smallPortrait;

    public GameObject LargePortraitPrefab => largePortraitPrefab;
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
