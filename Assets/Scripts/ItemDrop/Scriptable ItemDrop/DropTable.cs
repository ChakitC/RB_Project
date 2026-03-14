using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

[Serializable]
public class DropEntry
{
    public GameObject itemPrefab;
    public float weight = 1f;
}

[CreateAssetMenu(menuName = "Game/Drop/Drop Table")]
public class DropTable : ScriptableObject
{
    public List<DropEntry> entries = new List<DropEntry>();

    public GameObject GetRandomItem()
    {
        if (entries == null || entries.Count == 0) return null;

        float total = 0f;
        foreach (var e in entries)
            total += e.weight;

        float roll = Random.value * total;
        float cumulative = 0f;

        foreach (var e in entries)
        {
            cumulative += e.weight;
            if (roll <= cumulative)
                return e.itemPrefab;
        }

        return entries[^1].itemPrefab;
    }
}