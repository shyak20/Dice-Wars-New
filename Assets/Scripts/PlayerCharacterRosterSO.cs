using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Selectable player characters for the dice-select screen. Each entry is a full <see cref="PlayerDataSO"/> profile (deck, HP, combat settings).
/// </summary>
[CreateAssetMenu(fileName = "PlayerCharacterRoster", menuName = "DiceGame/Player Character Roster")]
public class PlayerCharacterRosterSO : ScriptableObject
{
    public List<PlayerDataSO> characters = new List<PlayerDataSO>();

    public int ValidCharacterCount
    {
        get
        {
            if (characters == null)
                return 0;
            var n = 0;
            for (var i = 0; i < characters.Count; i++)
            {
                if (characters[i] != null)
                    n++;
            }

            return n;
        }
    }

    public bool TryGetCharacterAtIndex(int index, out PlayerDataSO character)
    {
        character = null;
        if (characters == null || characters.Count == 0)
            return false;

        var validIndex = 0;
        for (var i = 0; i < characters.Count; i++)
        {
            var c = characters[i];
            if (c == null)
                continue;
            if (validIndex == index)
            {
                character = c;
                return true;
            }

            validIndex++;
        }

        return false;
    }
}
