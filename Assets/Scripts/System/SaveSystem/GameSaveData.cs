using System;

[Serializable]
public class GameSaveData
{
    public const int CurrentVersion = 3;

    public int saveVersion = CurrentVersion;
    public PlayerInventoryData inventory;
    public EquipmentSaveData equipment;
    public AccessoryLoadoutSaveData accessories;
    public PartyData party = new PartyData();
}
