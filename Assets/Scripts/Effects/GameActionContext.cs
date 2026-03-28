using System.Collections.Generic;

public class GameActionContext
{
    public CombatManager CombatManager { get; set; }
    public PlayerStatus Player { get; set; }
    public EnemyController Enemy { get; set; }
    public List<FaceResult> ChanneledFaces { get; set; }
    public FaceResult TriggeringFace { get; set; }
}
