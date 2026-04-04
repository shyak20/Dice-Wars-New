public class FaceResult
{
    public DieFaceSO Face { get; set; }
    public int Value { get; set; }
    public DieType Type { get; set; }

    // New fields to track independent values and timing
    public int Damage { get; set; }
    public int Armor { get; set; }
    public bool ActivateImmediately { get; set; }
    public IGameAction Action { get; set; }
}