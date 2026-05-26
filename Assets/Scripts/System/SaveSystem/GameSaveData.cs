using System;
using System.Collections.Generic;
using UnityEngine.Serialization;

[Serializable]
public class GameSaveData
{
    public PlayerInventoryData inventory;
    public PlayerWeaponData weapon;
    public EquipmentSaveData equipment;
    public AccessoryLoadoutSaveData accessories;
    public PartyData party = new PartyData();
}


   

