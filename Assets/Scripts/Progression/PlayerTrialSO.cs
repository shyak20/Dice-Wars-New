using UnityEngine;

[CreateAssetMenu(fileName = "NewPlayerTrial", menuName = "DiceGame/Progression/Player Trial")]
public class PlayerTrialSO : ScriptableObject
{
    [Tooltip("Unique id for save data and UI.")]
    public string trialID;

    [TextArea(2, 4)]
    public string description;

    [Header("UI")]
    [Tooltip("Icon shown on trial slots (complete and locked states).")]
    public Sprite trialIcon;

    public TrialType type;
    [Min(1)] public int targetValue = 1;

    [Tooltip("Exact Roll only: face value that must be rolled. Target Value = how many times.")]
    [Min(1)] public int exactRollValue = 1;

    [Header("Completion reward")]
    [Tooltip("Use + to pick a typed reward granted when this trial is completed.")]
    [SerializeReference] public ProgressionRewardBase completionReward;

    [Header("Reward tooltip")]
    [Tooltip("Row text for completion reward in trial hover tooltip. {0} = amount or relic name. Overrides reward.rowFormat when set.")]
    public string completionRewardRowFormat;

    void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(trialID))
            trialID = name;

        if (targetValue < 1)
            targetValue = 1;

        if (type == TrialType.ExactRoll && exactRollValue < 1)
            exactRollValue = 1;
    }
}
