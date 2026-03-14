using UnityEngine;

[CreateAssetMenu(menuName = "Game/Drop/Enemy Drop Profile")]
public class EnemyDropProfile : ScriptableObject
{
    [Header("โอกาสดรอปของ (0–1)")]
    [Range(0f, 1f)]
    public float dropChance = 0.6f;

    [Header("ตารางสุ่ม Rarity")]
    public RarityTable rarityTable;

    [Header("Pool ตาม Rarity")]
    public DropTable commonPool;
    public DropTable rarePool;
    public DropTable epicPool;
}