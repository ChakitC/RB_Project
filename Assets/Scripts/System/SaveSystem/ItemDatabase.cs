using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ItemDatabase", menuName = "Game/Item Database")]
public class ItemDatabase : ScriptableObject

{
    public List<ItemDefinition> items = new List<ItemDefinition>();
    
    [Header("Special Items")]
    public ItemDefinition goldItem;
    public ItemDefinition scrapItem;

    private Dictionary<string, ItemDefinition> _lookup;

    void OnEnable()
    {
        BuildLookup();
    }

    void BuildLookup()
    {
        _lookup = new Dictionary<string, ItemDefinition>();
            
        foreach (var item in items)
        {
            AddToLookup(item);
        }

        AddToLookup(goldItem);
        AddToLookup(scrapItem);
    }

    public ItemDefinition GetItemById(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        if (_lookup == null) BuildLookup();

        _lookup.TryGetValue(id, out var item);
        return item;
    }

    void AddToLookup(ItemDefinition item)
    {
        if (item == null || string.IsNullOrWhiteSpace(item.itemId))
            return;

        if (_lookup.TryGetValue(item.itemId, out var existing))
        {
            if (existing == item)
                return;

            Debug.LogWarning($"Duplicate itemId detected: {item.itemId}");
            return;
        }

        _lookup.Add(item.itemId, item);
    }
}
