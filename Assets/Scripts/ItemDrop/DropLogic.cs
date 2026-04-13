using UnityEngine;

public static class DropLogic
{
    public static bool RollDrop(float chance)
    {
        chance = Mathf.Clamp01(chance);
        return Random.value <= chance;
    }

    public static ItemRarity RollRarity(RarityTable table)
    {
        if (table == null)
            return ItemRarity.Common;

        return table.RollRarity();
    }

    public static DropTable GetPoolFromProfile(EnemyDropProfile profile, ItemRarity rarity)
    {
        if (profile == null)
            return null;

        return rarity switch
        {
            ItemRarity.Common => profile.commonPool,
            ItemRarity.Rare => profile.rarePool,
            ItemRarity.Epic => profile.epicPool,
            _ => profile.commonPool
        };
    }

    public static DropEntry RollEntryFromPool(DropTable pool)
    {
        if (pool == null)
            return null;

        return pool.GetRandomEntry();
    }
}
