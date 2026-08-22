using System;
using Sirenix.OdinInspector;
using UnityEngine;

[Serializable]
public sealed class EncounterWave
{
    [Tooltip("enemy prefab ที่ wave นี้สามารถสุ่ม spawn ได้")]
    [SerializeField] private GameObject[] enemyPrefabs;

#if UNITY_EDITOR
    [ShowInInspector]
    [FoldoutGroup("Enemy Base Stats", Expanded = true)]
    [ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true, DraggableItems = false, ShowPaging = false, NumberOfItemsPerPage = 0)]
    [LabelText("Prefab Base Stats")]
    private EncounterEnemyBaseStatsPreview[] EnemyBaseStatsPreview
    {
        get
        {
            if (enemyPrefabs == null || enemyPrefabs.Length == 0)
                return Array.Empty<EncounterEnemyBaseStatsPreview>();

            EncounterEnemyBaseStatsPreview[] previews = new EncounterEnemyBaseStatsPreview[enemyPrefabs.Length];
            for (int i = 0; i < enemyPrefabs.Length; i++)
                previews[i] = new EncounterEnemyBaseStatsPreview(i, enemyPrefabs[i]);

            return previews;
        }
    }
#endif

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

    /// <summary>
    /// Picks uniformly among the assigned prefabs, skipping empty slots. Random sampling over the
    /// raw array could miss the single valid prefab in a mostly empty pool, so the non-null
    /// candidates are counted first and then indexed. Neither pass allocates.
    /// </summary>
    public GameObject GetRandomEnemyPrefab()
    {
        if (enemyPrefabs == null || enemyPrefabs.Length == 0)
            return null;

        int candidateCount = 0;
        for (int i = 0; i < enemyPrefabs.Length; i++)
        {
            if (enemyPrefabs[i] != null)
                candidateCount++;
        }

        if (candidateCount == 0)
            return null;

        int pick = UnityEngine.Random.Range(0, candidateCount);
        for (int i = 0; i < enemyPrefabs.Length; i++)
        {
            if (enemyPrefabs[i] == null)
                continue;

            if (pick == 0)
                return enemyPrefabs[i];

            pick--;
        }

        return null;
    }

#if UNITY_EDITOR
    static CharacterStats ResolveEnemyBaseStats(GameObject prefab, out string status)
    {
        if (prefab == null)
        {
            status = "Prefab slot is empty.";
            return null;
        }

        EnemyContext enemyContext = prefab.GetComponentInChildren<EnemyContext>(true);
        if (enemyContext != null)
        {
            if (enemyContext.baseStats != null)
            {
                status = "Resolved from EnemyContext.baseStats.";
                return enemyContext.baseStats;
            }

            status = "EnemyContext found, but baseStats is not assigned.";
            return null;
        }

        CharacteContext characterContext = prefab.GetComponentInChildren<CharacteContext>(true);
        if (characterContext != null)
        {
            if (characterContext.baseStats != null)
            {
                status = "Resolved from CharacteContext.baseStats.";
                return characterContext.baseStats;
            }

            status = "CharacteContext found, but baseStats is not assigned.";
            return null;
        }

        status = "No EnemyContext or CharacteContext found on prefab.";
        return null;
    }

    [Serializable]
    sealed class EncounterEnemyBaseStatsPreview
    {
        readonly int index;
        readonly GameObject prefab;
        readonly CharacterStats baseStats;
        readonly string status;

        public EncounterEnemyBaseStatsPreview(int index, GameObject prefab)
        {
            this.index = index;
            this.prefab = prefab;
            baseStats = ResolveEnemyBaseStats(prefab, out status);
        }

        [ShowInInspector, ReadOnly]
        [HorizontalGroup("Source", Width = 70)]
        [LabelText("Slot")]
        string Slot => $"#{index}";

        [ShowInInspector, ReadOnly]
        [HorizontalGroup("Source")]
        [LabelText("Prefab")]
        GameObject Prefab => prefab;

        [ShowInInspector, ReadOnly]
        [LabelText("Status")]
        string Status => status;

        [ShowInInspector, ReadOnly]
        [LabelText("Base Summary")]
        string BaseSummary => baseStats == null
            ? "No CharacterStats resolved."
            : $"HP {baseStats.maxHP:0.##} | DMG {baseStats.Damage:0.##} | ARM {baseStats.armor:0.##} | SPD {baseStats.speed:0.##} | CR {baseStats.critRate:0.##} | CD {baseStats.critMultiplier:0.##}";

        [ShowInInspector, ReadOnly]
        [LabelText("Growth / Lv")]
        string GrowthSummary => baseStats == null
            ? "No CharacterStats resolved."
            : $"HP +{baseStats.MAXHPScaling:0.##} | DMG +{baseStats.DamageScaling:0.##} | ARM +{baseStats.ArmorScaling:0.##} | SPD +{baseStats.SpeedScaling:0.##}";

        [ShowInInspector, InlineEditor]
        [LabelText("Editable Base Stats")]
        CharacterStats EditableBaseStats => baseStats;
    }
#endif
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

    public int TotalSpawnCount
    {
        get
        {
            if (waves == null)
                return 0;

            int total = 0;
            for (int i = 0; i < waves.Length; i++)
            {
                if (waves[i] != null)
                    total += waves[i].SpawnCount;
            }

            return total;
        }
    }
}
