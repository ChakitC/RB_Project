using System;
using UnityEngine;

[Serializable]
public sealed class EncounterWave
{
    [Tooltip("enemy prefab ที่ wave นี้สามารถสุ่ม spawn ได้")]
    [SerializeField] private GameObject[] enemyPrefabs;

    [Tooltip("จำนวนศัตรูที่จะ spawn ใน wave นี้")]
    [SerializeField, Min(1)] private int spawnCount = 1;

    [Tooltip("เวลาหน่วงก่อนเริ่ม spawn wave นี้")]
    [SerializeField, Min(0f)] private float initialDelay;

    [Tooltip("เวลาหน่วงระหว่างการ spawn ศัตรูแต่ละตัว")]
    [SerializeField, Min(0f)] private float spawnInterval = 0.25f;

    [Tooltip("ถ้าเปิด จะรอให้ศัตรู wave นี้ตายหมดก่อนเริ่ม wave ถัดไป")]
    [SerializeField] private bool waitForWaveClear = true;

    public GameObject[] EnemyPrefabs => enemyPrefabs;
    public int SpawnCount => Mathf.Max(1, spawnCount);
    public float InitialDelay => Mathf.Max(0f, initialDelay);
    public float SpawnInterval => Mathf.Max(0f, spawnInterval);
    public bool WaitForWaveClear => waitForWaveClear;

    public GameObject GetRandomEnemyPrefab()
    {
        if (enemyPrefabs == null || enemyPrefabs.Length == 0)
            return null;

        for (int guard = 0; guard < enemyPrefabs.Length; guard++)
        {
            GameObject candidate = enemyPrefabs[UnityEngine.Random.Range(0, enemyPrefabs.Length)];
            if (candidate != null)
                return candidate;
        }

        return null;
    }
}

[CreateAssetMenu(menuName = "Game/Map/Encounter Definition")]
public class EncounterDefinitionSO : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("ชื่อ encounter สำหรับแยกอ่านใน Inspector")]
    [SerializeField] private string displayName;

    [Tooltip("ชนิด node ที่ encounter นี้รองรับ")]
    [SerializeField] private MapNodeType nodeType = MapNodeType.Combat;

    [Tooltip("น้ำหนักสุ่ม encounter นี้เมื่อมีหลายตัวชนิดเดียวกัน")]
    [SerializeField, Min(0f)] private float weight = 1f;

    [Tooltip("ระบุว่า encounter นี้เป็น boss encounter")]
    [SerializeField] private bool bossEncounter;

    [Header("Waves")]
    [Tooltip("ลำดับ wave ศัตรูที่จะ spawn ในห้อง")]
    [SerializeField] private EncounterWave[] waves;

    [Tooltip("ถ้าไม่มี wave หรือ spawn ไม่ได้ ให้ถือว่า encounter เคลียร์เมื่อไม่มีศัตรูเหลือ")]
    [SerializeField] private bool completeWhenNoEnemies = true;

    public string DisplayName => displayName;
    public MapNodeType NodeType => nodeType;
    public float Weight => Mathf.Max(0f, weight);
    public bool BossEncounter => bossEncounter;
    public EncounterWave[] Waves => waves;
    public bool CompleteWhenNoEnemies => completeWhenNoEnemies;
}
