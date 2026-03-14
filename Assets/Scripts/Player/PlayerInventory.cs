using System;
using UnityEngine;
using System.Collections.Generic;
using System.Text;
public class PlayerInventory : MonoBehaviour ,IGameSaveAble
{
    [Header("Settings")]
    public int slotCount = 0;   

    [Header("Runtime")]
    public List<InventorySlot> slots = new List<InventorySlot>();
    
    [Header("Refs")]
    public ItemDatabase itemDatabase;
    
    [Header("Currency")]
    [SerializeField] private int gold = 0;
    public int Gold => gold;
    
    public event Action<int> OnGoldChanged;

    public void AddGold(int amount)
    {
        if (amount <= 0) return;
        gold += amount;
        OnGoldChanged?.Invoke(gold);
    }

    public bool SpendGold(int amount)
    {
        if (amount <= 0) return false;
        if (gold < amount) return false;

        gold -= amount;
        OnGoldChanged?.Invoke(gold);
        return true;
    }
    
    public void ForceRefreshGoldUI()
    {
        OnGoldChanged?.Invoke(gold);
    }
    
    void Start()
    {
        if(SaveManager.Instance == null){ Debug.Log("Save Manager Missing");return;}
        SaveManager.Instance.Load();
        ForceRefreshGoldUI();
    }
    
      private void Awake()
    {
       
        // เตรียมช่องให้ครบ
        if (slots.Count == 0)
        {
            for (int i = 0; i < slotCount; i++)
            {
                slots.Add(new InventorySlot());
            }
        }
    }

    // -------------------------------------------------
    // 1) เพิ่มของเข้ากระเป๋า
    // -------------------------------------------------
    public bool AddItem(ItemDefinition item, int amount = 1)
    {
        if (item == null || amount <= 0)
            return false;
        
        if (itemDatabase != null && itemDatabase.goldItem != null && item == itemDatabase.goldItem)
        {
            AddGold(amount);
            return true;
        }

        // 1. ถ้า stackable → หา slot ที่เป็น item เดิมก่อน
        if (item.stackable)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];

                if (slot.item == item && slot.amount < item.maxStack)
                {
                    int canAdd = item.maxStack - slot.amount;
                    int toAdd = Mathf.Min(canAdd, amount);

                    slot.amount += toAdd;
                    amount -= toAdd;

                    // ถ้าใส่หมดแล้ว
                    if (amount <= 0)
                        return true;
                }
            }
        }

        // 2. ยังเหลือของอยู่ → หาช่องว่าง
        while (amount > 0)
        {
            int emptyIndex = FindEmptySlotIndex();
            if (emptyIndex == -1)
            {
                Debug.Log("Inventory full!");
                return false; // ของยังเหลือแต่ช่องเต็มแล้ว
            }

            var emptySlot = slots[emptyIndex];

            emptySlot.item = item;

            if (item.stackable)
            {
                int toAdd = Mathf.Min(item.maxStack, amount);
                emptySlot.amount = toAdd;
                amount -= toAdd;
            }
            else
            {
                emptySlot.amount = 1;
                amount -= 1;
            }
        }

        return true;
    }

    private int FindEmptySlotIndex()
    {
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i].IsEmpty)
                return i;
        }
        return -1;
    }

    // -------------------------------------------------
    // 2) เช็คว่ามีของพอไหม
    // -------------------------------------------------
    public bool HasItem(ItemDefinition item, int amount = 1)
    {
        if (item == null || amount <= 0)
            return false;

        int total = 0;
        foreach (var slot in slots)
        {
            if (slot.item == item)
            {
                total += slot.amount;
                if (total >= amount)
                    return true;
            }
        }
        return false;
    }

    // -------------------------------------------------
    // 3) ลบของออกจากกระเป๋า (ใช้/ทิ้ง)
    // -------------------------------------------------
    public bool RemoveItem(ItemDefinition item, int amount = 1)
    {
        if (!HasItem(item, amount))
            return false;

        for (int i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];
            if (slot.item != item)
                continue;

            int toRemove = Mathf.Min(slot.amount, amount);
            slot.amount -= toRemove;
            amount -= toRemove;

            if (slot.amount <= 0)
                slot.Clear();

            if (amount <= 0)
                return true;
        }

        return true;
    }
    
    
    
    
    public PlayerInventoryData ToData()
    {
        //แปลง Item ใน RunTime ที่มี เป็น Data
        var data = new PlayerInventoryData();
        data.gold = gold;
        
        for (int i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];
            if (slot == null || slot.IsEmpty)
            {
                // จะไม่เซฟช่องว่างก็ได้ หรือจะใส่ช่องว่างก็ได้แล้วแต่ดีไซน์
                data.slots.Add(new InventorySlotData
                {
                    itemId = null,
                    amount = 0
                });
                continue;
            }

            data.slots.Add(new InventorySlotData
            {
                itemId = slot.item != null ? slot.item.itemId : null,
                amount = slot.amount
            });
        }

        return data;
    }
    public void FromData(PlayerInventoryData data)
    {
        //แปลง Dataitem ที่เก็บไว้เป็น Item ใน Runtime
        if (data == null)
        {
            Debug.LogWarning("PlayerInventory.FromData: data is null");
            return;
        }
        gold = Mathf.Max(0, data.gold);
        OnGoldChanged?.Invoke(gold);
        
        // ให้แน่ใจว่ามีจำนวนช่องเท่ากับ slotCount
        slots.Clear();
        for (int i = 0; i < slotCount; i++)
        {
            slots.Add(new InventorySlot());
        }

        int count = Mathf.Min(slots.Count, data.slots.Count);

        for (int i = 0; i < count; i++)
        {
            var slotData = data.slots[i];
            var slot = slots[i];

            if (slotData == null || string.IsNullOrEmpty(slotData.itemId) || slotData.amount <= 0)
            {
                slot.Clear();
                continue;
            }

            var itemDef = itemDatabase.GetItemById(slotData.itemId);
            if (itemDef == null)
            {
                Debug.LogWarning($"Item with id {slotData.itemId} not found in database");
                slot.Clear();
                continue;
            }

            slot.item = itemDef;
            slot.amount = slotData.amount;
        }
    }

    public void OnSave(GameSaveData data)
    {
        if (data == null) return;
        data.inventory = ToData();
    }

    public void OnLoad(GameSaveData data)
    {
        if (data == null || data.inventory == null)
        {
            Debug.Log("[PlayerInventory] No inventory data in save");
            return;
        }

        FromData(data.inventory);
        ForceRefreshGoldUI();
    }
}
