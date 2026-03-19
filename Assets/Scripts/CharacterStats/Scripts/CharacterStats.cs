using UnityEngine;

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
    
    
    [Header("Base Stats")]
    public float maxHP = 100;
    public float maxStamina = 80;
    public float Damage = 10;
    public float critMultiplier = 5;
    public float armor = 1;
    public float critRate = 1;
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
    
    
}
