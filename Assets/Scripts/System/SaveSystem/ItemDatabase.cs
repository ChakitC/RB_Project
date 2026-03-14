using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ItemDatabase", menuName = "Game/Item Database")]
public class ItemDatabase : ScriptableObject

{
    public List<ItemDefinition> items = new List<ItemDefinition>();
    
    [Header("Special Items")]
    public ItemDefinition goldItem;

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
            if (item == null || string.IsNullOrWhiteSpace(item.itemId))
                continue;

            if (_lookup.ContainsKey(item.itemId))
            {
                Debug.LogWarning($"Duplicate itemId detected: {item.itemId}");
                continue;
            }

            _lookup.Add(item.itemId, item);
        }
    }

    public ItemDefinition GetItemById(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        if (_lookup == null) BuildLookup();

        _lookup.TryGetValue(id, out var item);
        return item;
    }
}