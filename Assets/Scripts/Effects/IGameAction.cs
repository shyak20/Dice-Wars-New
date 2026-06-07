public interface IGameAction
{
    void Execute(GameActionContext context);

    /// <summary>
    /// Die-face <see cref="GameActionWithIcon"/>: when false, <see cref="Execute"/> runs at turn submit.
    /// <see cref="FaceResolveModifierBase"/>: when false, <see cref="FaceResolveModifierBase.Modify"/> runs late in gather (after roll-watcher armor) instead of before relics.
    /// </summary>
    bool ActivateImmediately => true;

    /// <summary>
    /// Multi-enemy targeting: when true, this enemy-targeted action resolves the instant the player drops the rolled
    /// outcome onto an enemy (instead of accumulating under that enemy until turn end). No effect for player-targeted actions.
    /// </summary>
    bool TriggerImmediatelyOnDrop => false;
}

public static class GameActionDebug
{
    public const bool Enabled = true;
}
