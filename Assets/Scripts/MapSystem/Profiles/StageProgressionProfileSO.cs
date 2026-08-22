using UnityEngine;

/// <summary>
/// How a Test Stage advances: the level band it covers, how many runs clear it, what level its
/// enemies spawn at per run, and how the run's XP budget is split between regular kills, the boss,
/// and finishing.
///
/// A <see cref="MapRunConfigSO"/> with no profile assigned keeps reading its own legacy fields, so
/// existing assets are unaffected.
/// </summary>
[CreateAssetMenu(menuName = "Game/Map/Profiles/Stage Progression Profile")]
public class StageProgressionProfileSO : ScriptableObject
{
    [SerializeField] private LevelTableSO levelTable;
    [SerializeField, Min(1)] private int startLevel = 1;
    [SerializeField, Min(2)] private int targetLevel = 11;
    [SerializeField, Min(1)] private int targetRunCount = 2;

    [Tooltip("ระดับศัตรูของแต่ละรอบ ต้องมีจำนวนเท่ากับ Target Run Count")]
    [SerializeField] private int[] enemyLevelTiers;

    [SerializeField, Range(0f, 1f)] private float regularEnemyXpShare = 0.6f;
    [SerializeField, Range(0f, 1f)] private float bossXpShare = 0.2f;
    [SerializeField] private GameObject stageExitPrefab;

    public LevelTableSO LevelTable => levelTable;
    public int StartLevel => Mathf.Max(1, startLevel);
    public int TargetLevel => Mathf.Max(StartLevel + 1, targetLevel);
    public int TargetRunCount => Mathf.Max(1, targetRunCount);
    public int[] EnemyLevelTiers => enemyLevelTiers;
    public float RegularEnemyXpShare => Mathf.Clamp01(regularEnemyXpShare);
    public float BossXpShare => Mathf.Clamp(bossXpShare, 0f, 1f - RegularEnemyXpShare);
    public GameObject StageExitPrefab => stageExitPrefab;
}
