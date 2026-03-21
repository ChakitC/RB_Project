public enum WeaponRarity
{
    Common,
    Rare,
    Epic
}

public static class WeaponRarityUtility
{
    public static WeaponRarity FromItemRarity(ItemRarity rarity)
    {
        return rarity switch
        {
            ItemRarity.Rare => WeaponRarity.Rare,
            ItemRarity.Epic => WeaponRarity.Epic,
            _ => WeaponRarity.Common
        };
    }
}
