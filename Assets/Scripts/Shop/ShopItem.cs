/// <summary>One offer in the shop: a face or a full die, with price and sold state.</summary>
public class ShopItem
{
    public enum Kind
    {
        Face,
        FullDie,
        Relic
    }

    public Kind ItemKind { get; }
    public DieFaceSO Face { get; }
    public DieAssetSO Die { get; }
    public RelicSO Relic { get; }
    public int CalculatedPrice { get; }
    public bool IsSoldOut { get; set; }

    private ShopItem(Kind kind, DieFaceSO face, DieAssetSO die, RelicSO relic, int price)
    {
        ItemKind = kind;
        Face = face;
        Die = die;
        Relic = relic;
        CalculatedPrice = price;
    }

    public static ShopItem CreateFace(DieFaceSO face, int price)
    {
        return new ShopItem(Kind.Face, face, null, null, price);
    }

    public static ShopItem CreateDie(DieAssetSO die, int price)
    {
        return new ShopItem(Kind.FullDie, null, die, null, price);
    }

    public static ShopItem CreateRelic(RelicSO relic, int price)
    {
        return new ShopItem(Kind.Relic, null, null, relic, price);
    }
}
