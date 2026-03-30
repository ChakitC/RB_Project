using System.Collections.Generic;
using UnityEngine;

public enum CharacterWeaponHandMode
{
    RightHand = 0,
    LeftHand = 1,
    BothHands = 2,
    None = 3
}

[CreateAssetMenu(menuName = "Character Stats")]
public class CharacterStats : ScriptableObject
{
    [Header("Character Detail")]
    public string characterId;
    public string characterName;
    public Sprite icon;
    public GameObject CharacterPrefab;
    public GameObject CharacterPrefabBasement;
    public Avatar characterAvatar;
    public RuntimeAnimatorController controller;
    public CharacterAnimProfileSO animProfile;

    [Header("Weapon Visual")]
    public CharacterWeaponHandMode weaponHandMode = CharacterWeaponHandMode.RightHand;
    
    
    [Header("Base Stats")]
    public float maxHP = 100;
    public float maxStamina = 80;
    public float Damage = 10;
    public float critMultiplier = 1;
    public float armor = 1;
    public float critRate = 0;
    public float Enagy = 100;
    public float speed = 4.5f;
    
    [Header("Base Stats Down")]
    public float speedDown = 1;
    
    [Header("Level Scaling")] 
    public float DamageScaling;
    public float ArmorScaling;
    public float MAXHPScaling;
    public float StaminaScaling;
    public float CritrateScaling;
    public float CritDamageScaling;
    public float EnagyScaling;
    public float SpeedScaling;

    [Header("Passives")]
    public List<PassiveDefinition> passives = new();

    [Header("Audio")]
    public AudioCue dashCue;
    public AudioCue meleeLightCue;
    public AudioCue meleeHeavyCue;
    public AudioCue damagedCue;
    public AudioCue downCue;
    public AudioCue deathCue;
    public AudioCue reviveCue;
    
    
}
