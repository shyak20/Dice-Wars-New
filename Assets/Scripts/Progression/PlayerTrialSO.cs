using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewPlayerTrial", menuName = "DiceGame/Progression/Player Trial")]
public class PlayerTrialSO : ScriptableObject
{
    [SerializeField, HideInInspector] private string trialID;

    [Header("UI")]
    [Tooltip("Shown on trial slots, tooltips, and completion popups. Leave empty to use the asset file name.")]
    public string trialName;

    [TextArea(2, 4)]
    public string description;

    [Tooltip("Icon shown on trial slots (complete and locked states).")]
    public Sprite trialIcon;

    public TrialType type;
    [Min(1)] public int targetValue = 1;

    [Tooltip("Exact Roll only: face value that must be rolled. Target Value = how many times.")]
    [Min(1)] public int exactRollValue = 1;

    [Header("Completion rewards")]
    [Tooltip("Granted when this trial is completed. Use + to add typed rewards (stat bonus, unlock faces, etc.).")]
    [SerializeReference] public List<ProgressionRewardBase> completionRewards = new List<ProgressionRewardBase>();

    [Tooltip("Optional row format for all completion reward rows in the hover tooltip ({0} = amount or name).")]
    public string completionRewardRowFormat;

    /// <summary>Stable save key — always the asset file name (see <see cref="SyncTrialIdFromAssetName"/>).</summary>
    public string TrialId => string.IsNullOrWhiteSpace(trialID) ? name : trialID.Trim();

    /// <summary>Player-facing label for slots and tooltips.</summary>
    public string DisplayName
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(trialName))
                return trialName.Trim();

            return string.IsNullOrWhiteSpace(name) ? TrialId : name.Trim();
        }
    }

    public void SyncTrialIdFromAssetName()
    {
        if (string.IsNullOrWhiteSpace(name))
            return;

        trialID = name.Trim();
    }

    void OnValidate()
    {
        MigrateLegacyDisplayNameFromTrialId();
        SyncTrialIdFromAssetName();

        if (targetValue < 1)
            targetValue = 1;

        if (type == TrialType.ExactRoll && exactRollValue < 1)
            exactRollValue = 1;
    }

    void MigrateLegacyDisplayNameFromTrialId()
    {
        if (!string.IsNullOrWhiteSpace(trialName) || string.IsNullOrWhiteSpace(trialID) || string.IsNullOrWhiteSpace(name))
            return;

        if (string.Equals(trialID.Trim(), name.Trim(), System.StringComparison.Ordinal))
            return;

        trialName = trialID.Trim();
    }
}
