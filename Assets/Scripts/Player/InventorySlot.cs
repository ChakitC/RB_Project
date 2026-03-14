using UnityEngine;

public class InventorySlot
{
    public ItemDefinition item; // reference ไปหา ScriptableObject
    public int amount;          // จำนวนในช่องนี้

    public bool IsEmpty => item == null || amount <= 0;

    public void Clear()
    {
        item = null;
        amount = 0;
    }
}
