using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class EnemyDropRarityLevelBand
{
    [Min(1)] public int minimumLevel = 1;
    public RarityTable rarityTable;
}

[CreateAssetMenu(menuName = "Game/Drop/Enemy Drop Profile")]
public class EnemyDropProfile : ScriptableObject
{
    [Header("Guaranteed Currency")]
    public ItemDefinition goldItem;
    [Min(0)] public int minimumGold;
    [Min(0)] public int maximumGold;
    public ItemDefinition scrapItem;
    [Min(0)] public int minimumScrap;
    [Min(0)] public int maximumScrap;

    [Header("Drop Chance (0-1)")]
    [Range(0f, 1f)]
    public float dropChance = 0.6f;

    [Header("Rarity Roll")]
    public RarityTable rarityTable;
    public List<EnemyDropRarityLevelBand> rarityByLevel = new();

    [Header("Pool By Rarity")]
    public DropTable commonPool;
    public DropTable rarePool;
    public DropTable epicPool;

    public int RollGoldAmount() => RollInclusive(minimumGold, maximumGold);
    public int RollScrapAmount() => RollInclusive(minimumScrap, maximumScrap);

    public RarityTable ResolveRarityTable(int enemyLevel)
    {
        RarityTable resolved = rarityTable;
        int highestMinimum = int.MinValue;

        if (rarityByLevel == null)
            return resolved;

        for (int i = 0; i < rarityByLevel.Count; i++)
        {
            EnemyDropRarityLevelBand band = rarityByLevel[i];
            if (band == null || band.rarityTable == null)
                continue;

            int minimum = Mathf.Max(1, band.minimumLevel);
            if (enemyLevel >= minimum && minimum >= highestMinimum)
            {
                highestMinimum = minimum;
                resolved = band.rarityTable;
            }
        }

        return resolved;
    }

    static int RollInclusive(int minimum, int maximum)
    {
        minimum = Mathf.Max(0, minimum);
        maximum = Mathf.Max(minimum, maximum);
        return UnityEngine.Random.Range(minimum, maximum + 1);
    }
}
