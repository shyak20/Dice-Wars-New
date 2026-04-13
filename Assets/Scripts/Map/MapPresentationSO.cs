using UnityEngine;

[CreateAssetMenu(fileName = "MapPresentation", menuName = "DiceGame/Map/Map Presentation")]
public class MapPresentationSO : ScriptableObject
{
    [Header("Locations")]
    public Sprite startTileIcon;
    public Sprite bossTileIcon;

    [Header("Combat ranks")]
    public Sprite combatNormalIcon;
    public Sprite combatEliteIcon;
    public Sprite combatBossIcon;

    [Header("Other events")]
    public Sprite shopIcon;
    public Sprite unknownIcon;
    public Sprite shrineIcon;
    public Sprite treasureIcon;

    public Sprite GetEventIcon(MapEventType eventType, bool isStart, bool isBossEnd)
    {
        if (isStart && startTileIcon != null)
            return startTileIcon;
        if (isBossEnd && bossTileIcon != null)
            return bossTileIcon;

        return eventType switch
        {
            MapEventType.CombatNormal => combatNormalIcon,
            MapEventType.CombatElite => combatEliteIcon,
            MapEventType.CombatBoss => combatBossIcon,
            MapEventType.Shop => shopIcon,
            MapEventType.Unknown => unknownIcon,
            MapEventType.Shrine => shrineIcon,
            MapEventType.Treasure => treasureIcon,
            _ => null
        };
    }
}
