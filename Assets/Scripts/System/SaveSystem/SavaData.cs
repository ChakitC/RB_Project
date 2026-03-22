using System;
using System.Collections.Generic;

[Serializable]
public class InventorySlotSaveData
{
    public string itemId;
    public int amount;
    public WeaponInstanceData weaponInstance;
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
