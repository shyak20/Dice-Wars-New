/// <summary>One offer in the shop: a face or a full die, with price and sold state.</summary>
public class ShopItem
{
    public enum Kind
    {
        Face,
        FullDie
    }

    public Kind ItemKind { get; }
    public DieFaceSO Face { get; }
    public DieAssetSO Die { get; }
    public int CalculatedPrice { get; }
    public bool IsSoldOut { get; set; }

    private ShopItem(Kind kind, DieFaceSO face, DieAssetSO die, int price)
    {
        ItemKind = kind;
        Face = face;
        Die = die;
        CalculatedPrice = price;
    }

    public static ShopItem CreateFace(DieFaceSO face, int price)
    {
        return new ShopItem(Kind.Face, face, null, price);
    }

    public static ShopItem CreateDie(DieAssetSO die, int price)
    {
        return new ShopItem(Kind.FullDie, null, die, price);
    }
}
