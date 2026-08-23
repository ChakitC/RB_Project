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
    public int scrap;
    public List<InventorySlotSaveData> slots = new();
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
    public List<CharacterSkillSelectionSaveData> selectedSkillOptions = new();
    public int skillPoints = 0;
    public bool skillProgressInitialized = false;
    public List<CharacterSkillTreeProgressSaveData> activeSkillTrees = new();

    public CharacterProgressData DeepClone()
    {
        return new CharacterProgressData
        {
            level = level,
            xp = xp,
            unlocked = unlocked,
            selectedSkillOptions = CloneSkillSelections(selectedSkillOptions),
            skillPoints = skillPoints,
            skillProgressInitialized = skillProgressInitialized,
            activeSkillTrees = CloneActiveSkillTrees(activeSkillTrees),
        };
    }

    static List<CharacterSkillSelectionSaveData> CloneSkillSelections(List<CharacterSkillSelectionSaveData> source)
    {
        var clone = new List<CharacterSkillSelectionSaveData>();
        if (source == null)
            return clone;

        for (int i = 0; i < source.Count; i++)
        {
            CharacterSkillSelectionSaveData entry = source[i];
            if (entry == null)
                continue;

            clone.Add(new CharacterSkillSelectionSaveData
            {
                slotId = entry.slotId,
                optionId = entry.optionId
            });
        }

        return clone;
    }

    static List<CharacterSkillTreeProgressSaveData> CloneActiveSkillTrees(
        List<CharacterSkillTreeProgressSaveData> source)
    {
        var clone = new List<CharacterSkillTreeProgressSaveData>();
        if (source == null)
            return clone;

        for (int i = 0; i < source.Count; i++)
        {
            CharacterSkillTreeProgressSaveData entry = source[i];
            if (entry != null)
                clone.Add(entry.DeepClone());
        }

        return clone;
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
