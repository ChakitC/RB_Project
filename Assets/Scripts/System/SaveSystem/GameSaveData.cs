using System;
using System.Collections.Generic;
using UnityEngine.Serialization;

[Serializable]
public class GameSaveData
{
    public PlayerInventoryData inventory;
    public PlayerWeaponData weapon;
    public PartyData party = new PartyData();
}


   

