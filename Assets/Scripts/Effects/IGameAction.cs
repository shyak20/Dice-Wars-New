public interface IGameAction
{
    void Execute(GameActionContext context);

    /// <summary>
    /// Die-face <see cref="GameActionWithIcon"/>: when false, <see cref="Execute"/> runs at turn submit.
    /// <see cref="FaceResolveModifierBase"/>: when false, <see cref="FaceResolveModifierBase.Modify"/> runs late in gather (after roll-watcher armor) instead of before relics.
    /// </summary>
    bool ActivateImmediately => true;
}

public static class GameActionDebug
{
    public const bool Enabled = true;
}
