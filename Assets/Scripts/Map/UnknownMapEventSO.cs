using UnityEngine;

/// <summary>
/// One possible outcome for an <see cref="MapEventType.Unknown"/> tile on a given act.
/// Subclass or extend with gameplay hooks when you implement unknown events.
/// </summary>
[CreateAssetMenu(fileName = "UnknownMapEvent", menuName = "DiceGame/Map/Unknown Map Event")]
public class UnknownMapEventSO : ScriptableObject
{
    [Tooltip("Shown in logs/UI; defaults to asset name if empty.")]
    public string displayName;

    [TextArea(2, 6)]
    public string description;

    public Sprite icon;

    public string DisplayLabel => string.IsNullOrEmpty(displayName) ? name : displayName;
}
