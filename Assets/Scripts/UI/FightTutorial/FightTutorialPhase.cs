using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>One step in the fight tutorial: when it appears, how it ends, and what to highlight.</summary>
[Serializable]
public sealed class FightTutorialPhase
{
    [Tooltip("Optional label for the Inspector only.")]
    public string phaseName;

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

    [Tooltip("If Complete When is a combat trigger, also finish when any advance button is clicked.")]
    public bool allowButtonSkip;

    [Tooltip(
        "When true, turns on the Tutorial Controller Interaction Blocker for this phase (blocks fight UI). " +
        "Turn off for phases that wait on Select Die / Roll so the player can interact.")]
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
        if (advanceButtons == null || advanceButtons.Count == 0)
            return false;
        for (var i = 0; i < advanceButtons.Count; i++)
        {
            if (advanceButtons[i] != null)
                return true;
        }

        return false;
    }

    public void Validate(string ownerName, int index)
    {
        var label = string.IsNullOrWhiteSpace(phaseName) ? $"phases[{index}]" : $"'{phaseName}'";
        if (phaseRoot == null)
            Debug.LogError($"{ownerName}: {label} — assign phaseRoot.");

        if (activateWhen == FightTutorialTrigger.None || activateWhen == FightTutorialTrigger.AdvanceButton)
            Debug.LogError($"{ownerName}: {label} — activateWhen cannot be {activateWhen}.");

        if (completeWhen == FightTutorialTrigger.None || completeWhen == FightTutorialTrigger.Immediate)
            Debug.LogError($"{ownerName}: {label} — completeWhen cannot be {completeWhen}.");

        if ((completeWhen == FightTutorialTrigger.AdvanceButton || allowButtonSkip) && !HasAdvanceButtons())
            Debug.LogError($"{ownerName}: {label} — assign at least one advanceButtons entry for button completion.");

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
