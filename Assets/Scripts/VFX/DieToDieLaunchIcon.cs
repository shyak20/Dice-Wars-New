using UnityEngine;

/// <summary>Optional action icon payload spawned on each die-to-die projectile (one per reroll target).</summary>
public readonly struct DieToDieLaunchIcon
{
    public readonly Sprite Icon;
    public readonly Sprite Background;

    public DieToDieLaunchIcon(Sprite icon, Sprite background)
    {
        Icon = icon;
        Background = background;
    }

    public bool HasAny => Icon != null || Background != null;

    public static DieToDieLaunchIcon FromActionVisualId(ActionVisualId id) =>
        new(GameIconCatalog.GetActionIcon(id), GameIconCatalog.GetActionBackground(id));
}
