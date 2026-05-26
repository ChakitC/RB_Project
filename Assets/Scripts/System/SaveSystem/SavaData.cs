using System;
using System.Collections.Generic;

[Serializable]
public class InventorySlotSaveData
{
    public string itemId;
    public int amount;
    public WeaponInstanceData weaponInstance;
    public AccessoryInstanceData accessoryInstance;
}

[Serializable]
public class PlayerInventoryData
{
    public int maxSlotCount;
    public int gold;
    public List<InventorySlotSaveData> slots = new();
}

[Serializable]
public class PlayerWeaponData
{
    public string equippedWeaponInstanceId;
}

[Serializable]
public class CharacterEquipmentSaveData
{
    public string ownerId;
    public string equippedWeaponInstanceId;
}

[Serializable]
public class EquipmentSaveData
{
    public List<CharacterEquipmentSaveData> entries = new();
}

[Serializable]
public class PartyData
{
    public List<string> partyIds = new();
}

#region CharacterProgressData

[Serializable]
public class CharacterProgressData
{
    public int level = 1;
    public int xp = 0;
    public bool unlocked = false;
    public int skillPoints = 0;
    public bool passiveProgressInitialized = false;
    public List<string> unlockedPassiveNodeIds = new();

    public CharacterProgressData DeepClone()
    {
        return new CharacterProgressData
        {
            level = level,
            xp = xp,
            unlocked = unlocked,
            skillPoints = skillPoints,
            passiveProgressInitialized = passiveProgressInitialized,
            unlockedPassiveNodeIds = unlockedPassiveNodeIds != null ? new List<string>(unlockedPassiveNodeIds) : new List<string>()
        };
    }
}

[Serializable]
public class CharacterProgressEntry
{
    public string characterId;
    public CharacterProgressData progress = new CharacterProgressData();
}

[Serializable]
public class CharacterProgressSaveFile
{
    public List<CharacterProgressEntry> entries = new();
}

#endregion
