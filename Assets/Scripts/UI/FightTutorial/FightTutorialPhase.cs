using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>One step in the fight tutorial: when it appears, how it ends, and what to highlight.</summary>
[Serializable]
public sealed class FightTutorialPhase
{
    [Tooltip("Optional label for the Inspector only. Also used as the once-ever seen id unless Persistence Id is set.")]
    public string phaseName;

    [Tooltip(
        "Stable PlayerPrefs id for once-ever phase progress. Leave empty to use Phase Name. " +
        "Cleared when progression is reset.")]
    public string persistenceId;

    [Tooltip("Root panel for this phase. Enabled while active, disabled when the phase ends.")]
    public GameObject phaseRoot;

    [Tooltip("When this phase becomes next in the list, wait for this before showing it.")]
    public FightTutorialTrigger activateWhen = FightTutorialTrigger.Immediate;

    [Tooltip("Seconds to wait after Activate When fires before enabling Phase Root (and the rest of the phase UI). 0 = immediate.")]
    [Min(0f)]
    public float phaseRootEnableDelaySeconds;

    [Tooltip("What finishes this phase and moves on (or ends the tutorial).")]
    public FightTutorialTrigger completeWhen = FightTutorialTrigger.AdvanceButton;

    [Tooltip("Any of these buttons finish the phase when Complete When is Advance Button (or Allow Button Skip is on).")]
    public List<Button> advanceButtons = new List<Button>();

    [Tooltip(
        "When true, Face Action Option buttons from the face picker also act as advance buttons " +
        "(resolved when the phase presents — use with Activate When = Face Select Appear).")]
    public bool useFaceSelectOptionsAsAdvanceButtons;

    [Tooltip(
        "When true, RunRewardOfferRow action buttons on the win stage also act as advance buttons " +
        "(resolved when the phase presents — use with Activate When = Victory Screen Appear).")]
    public bool useVictoryRewardRowsAsAdvanceButtons;

    [Tooltip("If Complete When is a combat trigger, also finish when any advance button is clicked.")]
    public bool allowButtonSkip;

    [Tooltip(
        "When true, turns on the Tutorial Controller Interaction Blocker for this phase (blocks fight UI). " +
        "Turn off for phases that need fight interaction (Select Die / Roll / Element Value drag). " +
        "When off and there are no advance buttons, the phase root also passes raycasts through so tip UI cannot block drops.")]
    [UnityEngine.Serialization.FormerlySerializedAs("blockPlayerInput")]
    public bool enableInteractionBlocker = true;

    [Tooltip("Enabled when this phase starts; disabled when the phase ends.")]
    public List<GameObject> objectsToEnable = new List<GameObject>();

    [Tooltip(
        "UI objects temporarily reparented under the tutorial Screen Space Overlay so they draw above all Camera canvases. " +
        "Restored when the phase ends. Prefer the visual root of the control (e.g. whole HP cluster).")]
    public List<FightTutorialSortingTarget> sortingTargets = new List<FightTutorialSortingTarget>();

    public bool HasAdvanceButtons()
    {
        if (useFaceSelectOptionsAsAdvanceButtons || activateWhen == FightTutorialTrigger.FaceSelectAppear)
            return true;

        if (useVictoryRewardRowsAsAdvanceButtons || activateWhen == FightTutorialTrigger.VictoryScreenAppear)
            return true;

        if (advanceButtons == null || advanceButtons.Count == 0)
            return false;
        for (var i = 0; i < advanceButtons.Count; i++)
        {
            if (advanceButtons[i] != null)
                return true;
        }

        return false;
    }

    /// <summary>Stable id used for once-ever seen prefs.</summary>
    public string ResolvePersistenceId(int phaseIndex)
    {
        if (!string.IsNullOrWhiteSpace(persistenceId))
            return persistenceId.Trim();
        if (!string.IsNullOrWhiteSpace(phaseName))
            return phaseName.Trim();
        return $"PhaseIndex_{phaseIndex}";
    }

    public void Validate(string ownerName, int index)
    {
        var label = string.IsNullOrWhiteSpace(phaseName) ? $"phases[{index}]" : $"'{phaseName}'";
        if (phaseRoot == null)
            Debug.LogError($"{ownerName}: {label} — assign phaseRoot.");

        if (string.IsNullOrWhiteSpace(persistenceId) && string.IsNullOrWhiteSpace(phaseName))
            Debug.LogError(
                $"{ownerName}: {label} — assign phaseName or persistenceId so once-ever progress can be stored.");

        if (activateWhen == FightTutorialTrigger.None || activateWhen == FightTutorialTrigger.AdvanceButton)
            Debug.LogError($"{ownerName}: {label} — activateWhen cannot be {activateWhen}.");

        if (completeWhen == FightTutorialTrigger.None || completeWhen == FightTutorialTrigger.Immediate)
            Debug.LogError($"{ownerName}: {label} — completeWhen cannot be {completeWhen}.");

        if ((completeWhen == FightTutorialTrigger.AdvanceButton || allowButtonSkip) && !HasAdvanceButtons())
            Debug.LogError(
                $"{ownerName}: {label} — assign advanceButtons, or use Face Select / Victory Screen dynamic advance options.");

        if (advanceButtons != null)
        {
            for (var i = 0; i < advanceButtons.Count; i++)
            {
                if (advanceButtons[i] == null)
                    Debug.LogError($"{ownerName}: {label} advanceButtons[{i}] — assign Button.");
            }
        }

        if (objectsToEnable != null)
        {
            for (var i = 0; i < objectsToEnable.Count; i++)
            {
                if (objectsToEnable[i] == null)
                    Debug.LogError($"{ownerName}: {label} objectsToEnable[{i}] — assign GameObject.");
            }
        }

        if (sortingTargets == null)
            return;

        for (var i = 0; i < sortingTargets.Count; i++)
        {
            if (sortingTargets[i] == null)
            {
                Debug.LogError($"{ownerName}: {label} sortingTargets[{i}] is null.");
                continue;
            }

            sortingTargets[i].Validate($"{ownerName}: {label}", i);
        }
    }
}
