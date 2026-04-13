using UnityEngine;

[CreateAssetMenu(menuName = "Game/Drop/Enemy Drop Profile")]
public class EnemyDropProfile : ScriptableObject
{
    [Header("Drop Chance (0-1)")]
    [Range(0f, 1f)]
    public float dropChance = 0.6f;

    [Header("Rarity Roll")]
    public RarityTable rarityTable;

    [Header("Pool By Rarity")]
    public DropTable commonPool;
    public DropTable rarePool;
    public DropTable epicPool;
}
