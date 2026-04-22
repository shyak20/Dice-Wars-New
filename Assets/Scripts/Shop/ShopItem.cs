/// <summary>One offer in the shop: a face, full die, relic, or gem, with price and sold state.</summary>
public class ShopItem
{
    public enum Kind
    {
        Face,
        FullDie,
        Relic,
        Gem
    }

    public Kind ItemKind { get; }
    public DieFaceSO Face { get; }
    public DieAssetSO Die { get; }
    public RelicSO Relic { get; }
    public GemSO Gem { get; }
    public int CalculatedPrice { get; }
    public bool IsSoldOut { get; set; }

    private ShopItem(Kind kind, DieFaceSO face, DieAssetSO die, RelicSO relic, GemSO gem, int price)
    {
        ItemKind = kind;
        Face = face;
        Die = die;
        Relic = relic;
        Gem = gem;
        CalculatedPrice = price;
    }

    public static ShopItem CreateFace(DieFaceSO face, int price) =>
        new ShopItem(Kind.Face, face, null, null, null, price);

    public static ShopItem CreateDie(DieAssetSO die, int price) =>
        new ShopItem(Kind.FullDie, null, die, null, null, price);

    public static ShopItem CreateRelic(RelicSO relic, int price) =>
        new ShopItem(Kind.Relic, null, null, relic, null, price);

    public static ShopItem CreateGem(GemSO gem, int price) =>
        new ShopItem(Kind.Gem, null, null, null, gem, price);
}
