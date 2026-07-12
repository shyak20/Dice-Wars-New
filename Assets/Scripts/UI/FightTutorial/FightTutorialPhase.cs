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

    [Tooltip("What finishes this phase and moves on (or ends the tutorial).")]
    public FightTutorialTrigger completeWhen = FightTutorialTrigger.AdvanceButton;

    [Tooltip("Required when Complete When is Advance Button (or when Allow Button Skip is on).")]
    public Button advanceButton;

    [Tooltip("If Complete When is a combat trigger, also finish when the advance button is clicked.")]
    public bool allowButtonSkip;

    [Tooltip(
        "When true, enables the flow's interaction blocker so the player must use the tutorial control. " +
        "Turn off for phases that wait on Select Die / Roll so the player can interact with the fight UI.")]
    public bool blockPlayerInput = true;

    [Tooltip("Enabled when this phase starts; disabled when the phase ends.")]
    public List<GameObject> objectsToEnable = new List<GameObject>();

    [Tooltip(
        "UI objects temporarily reparented under the tutorial Screen Space Overlay so they draw above all Camera canvases. " +
        "Restored when the phase ends. Prefer the visual root of the control (e.g. whole HP cluster).")]
    public List<FightTutorialSortingTarget> sortingTargets = new List<FightTutorialSortingTarget>();

    public void Validate(string ownerName, int index)
    {
        var label = string.IsNullOrWhiteSpace(phaseName) ? $"phases[{index}]" : $"'{phaseName}'";
        if (phaseRoot == null)
            Debug.LogError($"{ownerName}: {label} — assign phaseRoot.");

        if (activateWhen == FightTutorialTrigger.None || activateWhen == FightTutorialTrigger.AdvanceButton)
            Debug.LogError($"{ownerName}: {label} — activateWhen cannot be {activateWhen}.");

        if (completeWhen == FightTutorialTrigger.None || completeWhen == FightTutorialTrigger.Immediate)
            Debug.LogError($"{ownerName}: {label} — completeWhen cannot be {completeWhen}.");

        if ((completeWhen == FightTutorialTrigger.AdvanceButton || allowButtonSkip) && advanceButton == null)
            Debug.LogError($"{ownerName}: {label} — assign advanceButton for button completion.");

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
