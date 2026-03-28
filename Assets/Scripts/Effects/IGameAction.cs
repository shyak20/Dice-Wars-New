public interface IGameAction
{
    void Execute(GameActionContext context);
}

public static class GameActionDebug
{
    public const bool Enabled = true;
}
