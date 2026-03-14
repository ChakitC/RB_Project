using System;
using System.Collections.Generic;

[Serializable]
public class InventorySlotData
{
    public string itemId;
    public int amount;
    
}

[Serializable]
public class PlayerInventoryData
{
    public int gold;
    public List<InventorySlotData> slots = new List<InventorySlotData>();
    
}

[Serializable]
public class PlayerWeaponData
{
    public float damage;
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
    public int skillPoints = 0;
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

