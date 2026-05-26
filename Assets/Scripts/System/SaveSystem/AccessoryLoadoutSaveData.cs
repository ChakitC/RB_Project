using System;
using System.Collections.Generic;

[Serializable]
public class CharacterAccessoryLoadoutSaveData
{
    public string ownerId;
    public int slotCount;
    public List<AccessoryInstanceData> equippedAccessories = new();
}

[Serializable]
public class AccessoryLoadoutSaveData
{
    public List<CharacterAccessoryLoadoutSaveData> entries = new();
}
