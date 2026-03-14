using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;


[Serializable]
public class RarityWeight
{
    public ItemRarity rarity;
    public float weight = 1f;
}

[CreateAssetMenu(menuName = "Game/Drop/Rarity Table")]
public class RarityTable : ScriptableObject
{
    public List<RarityWeight> entries = new List<RarityWeight>();

    public ItemRarity RollRarity()
    {
        float total = 0f;
        foreach (var e in entries)
            total += e.weight;

        float roll = Random.value * total;
        float cumulative = 0;

        foreach (var e in entries)
        {
            cumulative += e.weight;
            if (roll <= cumulative)
                return e.rarity;
        }

        return entries[^1].rarity;
    }
}