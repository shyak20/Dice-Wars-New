using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Curses Loot Table", menuName = "DiceGame/Curses/Curse Loot Table")]
public class CurseLootSO : ScriptableObject
{
    public List<DieFaceSO> allPossibleCurses = new List<DieFaceSO>();

    public DieFaceSO PickRandomCurse()
    {
        if (allPossibleCurses == null || allPossibleCurses.Count == 0)
        {
            Debug.LogError("CurseLootSO: allPossibleCurses is empty.", this);
            return null;
        }

        var pool = new List<DieFaceSO>();
        for (var i = 0; i < allPossibleCurses.Count; i++)
        {
            var face = allPossibleCurses[i];
            if (face != null && face.type == DieType.Curse)
                pool.Add(face);
        }

        if (pool.Count == 0)
        {
            Debug.LogError("CurseLootSO: no valid curse faces in allPossibleCurses.", this);
            return null;
        }

        return pool[Random.Range(0, pool.Count)];
    }
}
