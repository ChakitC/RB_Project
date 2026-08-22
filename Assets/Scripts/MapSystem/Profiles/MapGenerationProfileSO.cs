using UnityEngine;

/// <summary>
/// The shape of a generated map: how long the critical path is, how many branches it grows, and how
/// node types are rolled. Sharing one profile across stages is what stops every new stage from
/// copy-pasting the same tuning.
///
/// A <see cref="MapRunConfigSO"/> that references a profile reads its shape from here; a config with
/// no profile keeps reading its own legacy fields, so existing assets are unaffected.
/// </summary>
[CreateAssetMenu(menuName = "Game/Map/Profiles/Map Generation Profile")]
public class MapGenerationProfileSO : ScriptableObject
{
    [Header("Seed")]
    [Tooltip("ถ้าเปิด จะสุ่ม seed ใหม่ทุกครั้งที่เริ่ม run")]
    [SerializeField] private bool randomizeSeed = true;

    [Tooltip("seed คงที่สำหรับทดสอบ map เดิมซ้ำ ใช้เมื่อปิด Randomize Seed")]
    [SerializeField] private int seed;

    [Header("Shape")]
    [Tooltip("จำนวน node บนเส้นหลักตั้งแต่ Start ถึง Boss")]
    [SerializeField, Min(2)] private int criticalPathNodeCount = 6;

    [Tooltip("จำนวนทางแยกเสริมขั้นต่ำต่อ run")]
    [SerializeField, Min(0)] private int minBranchCount = 1;

    [Tooltip("จำนวนทางแยกเสริมสูงสุดต่อ run")]
    [SerializeField, Min(0)] private int maxBranchCount = 3;

    [Tooltip("จำนวนประตูออกสูงสุดที่ node หนึ่ง node มีได้")]
    [SerializeField, Range(1, 4)] private int maxOutgoingPerNode = 3;

    [Tooltip("บังคับให้มีห้องน้ำเงินก่อนถึง Boss บนเส้นหลัก")]
    [SerializeField] private bool forceBlueBeforeBoss = true;

    [Tooltip("กฎช่วยลดการเจอห้องแดงติดกันนานเกินไป")]
    [SerializeField] private MapPitySystem pitySystem = new();

    [Header("Node Weights")]
    [Tooltip("น้ำหนักสุ่มชนิดห้องบนเส้นหลักช่วงกลาง run")]
    [SerializeField] private WeightedMapNodeType[] mainPathWeights =
    {
        new() { type = MapNodeType.Combat, weight = 6f },
        new() { type = MapNodeType.Elite, weight = 1f },
        new() { type = MapNodeType.Ambush, weight = 1f }
    };

    [Tooltip("น้ำหนักสุ่มชนิดห้องน้ำเงิน เช่น Reward, Shop, Heal, Upgrade")]
    [SerializeField] private WeightedMapNodeType[] blueWeights =
    {
        new() { type = MapNodeType.Reward, weight = 4f },
        new() { type = MapNodeType.Shop, weight = 1f },
        new() { type = MapNodeType.Heal, weight = 1f },
        new() { type = MapNodeType.Upgrade, weight = 1f }
    };

    [Tooltip("น้ำหนักสุ่มชนิดห้องปลายทางตัน ทางตันควรมีรางวัลหรือความคุ้มค่าเสมอ")]
    [SerializeField] private WeightedMapNodeType[] branchDeadEndWeights =
    {
        new() { type = MapNodeType.Reward, weight = 5f },
        new() { type = MapNodeType.Elite, weight = 2f },
        new() { type = MapNodeType.Event, weight = 1f }
    };

    public bool RandomizeSeed => randomizeSeed;
    public int Seed => seed;
    public int CriticalPathNodeCount => Mathf.Max(2, criticalPathNodeCount);
    public int MinBranchCount => Mathf.Max(0, minBranchCount);
    public int MaxBranchCount => Mathf.Max(MinBranchCount, maxBranchCount);
    public int MaxOutgoingPerNode => Mathf.Clamp(maxOutgoingPerNode, 1, 4);
    public bool ForceBlueBeforeBoss => forceBlueBeforeBoss;
    public MapPitySystem PitySystem => pitySystem ?? new MapPitySystem();
    public WeightedMapNodeType[] MainPathWeights => mainPathWeights;
    public WeightedMapNodeType[] BlueWeights => blueWeights;
    public WeightedMapNodeType[] BranchDeadEndWeights => branchDeadEndWeights;
}
