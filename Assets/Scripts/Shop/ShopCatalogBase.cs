using UnityEngine;

public abstract class ShopCatalogBase : ScriptableObject
{
    public abstract int EntryCount { get; }

    public virtual void PrepareForOpen()
    {
    }

    public abstract ShopCatalogEntry GetEntry(int index);

    public virtual string ResolveEntryRuntimeId(int index, ShopCatalogEntry entry)
    {
        if (entry == null)
            return $"missing_{Mathf.Max(0, index):000}";

        return entry.ResolveRuntimeId(index);
    }
}
