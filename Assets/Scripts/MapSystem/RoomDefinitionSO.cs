using UnityEngine;

[CreateAssetMenu(menuName = "Game/Map/Room Definition")]
public class RoomDefinitionSO : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("ชื่อห้องที่ใช้แสดงใน prompt ประตูหรือ map UI")]
    [SerializeField] private string displayName;

    [Tooltip("ชนิด node ที่ห้องนี้รองรับ เช่น Combat, Reward, Boss")]
    [SerializeField] private MapNodeType nodeType = MapNodeType.Combat;

    [Tooltip("น้ำหนักสุ่มห้องนี้เมื่อมีหลายห้องชนิดเดียวกัน")]
    [SerializeField, Min(0f)] private float weight = 1f;

    [Header("Prefab")]
    [Tooltip("prefab ห้อง 3D ที่จะ spawn เมื่อผู้เล่นเข้า node นี้")]
    [SerializeField] private GameObject roomPrefab;

    [Tooltip("จำนวนประตูออกสูงสุดที่ prefab ห้องนี้รองรับ")]
    [SerializeField, Min(1)] private int maxExitCount = 3;
    [SerializeField] private RoomExitMask exitMask = RoomExitMask.Up | RoomExitMask.Right | RoomExitMask.Down | RoomExitMask.Left;
    [SerializeField] private bool allowSupersetExitMask = true;

    [Header("Flow")]
    [Tooltip("ล็อกประตูออกจนกว่าห้องจะถูกเคลียร์")]
    [SerializeField] private bool lockExitsUntilClear = true;

    [Tooltip("เริ่ม encounter ทันทีเมื่อผู้เล่นเข้าห้องนี้")]
    [SerializeField] private bool startEncounterOnEnter = true;

    [Tooltip("ต้องเคลียร์ห้องก่อนจึงจะออกได้ ใช้ร่วมกับการล็อกประตู")]
    [SerializeField] private bool requiresClearBeforeExit = true;

    [Header("Reward")]
    [Tooltip("ตารางรางวัลที่จะสุ่มหลังเคลียร์ห้อง")]
    [SerializeField] private DropTable clearRewardTable;

    [Tooltip("จำนวนครั้งที่สุ่มรางวัลหลังเคลียร์ห้อง")]
    [SerializeField, Min(0)] private int clearRewardRolls;

    [Tooltip("rarity ที่ใช้กับ item drop จากรางวัลเคลียร์ห้อง")]
    [SerializeField] private WeaponRarity clearRewardRarity = WeaponRarity.Common;
    [SerializeField, Range(0f, 1f)] private float riskLevel;

    [Header("Map UI")]
    [Tooltip("ข้อความ hint สั้น ๆ สำหรับแสดงใน map UI")]
    [SerializeField] private string mapHint;

    [Tooltip("ไอคอนสำหรับแสดงชนิดห้องบน map UI")]
    [SerializeField] private Sprite mapIcon;

    public string DisplayName => displayName;
    public MapNodeType NodeType => nodeType;
    public float Weight => Mathf.Max(0f, weight);
    public GameObject RoomPrefab => roomPrefab;
    public int MaxExitCount => Mathf.Max(1, maxExitCount);
    public RoomExitMask ExitMask => exitMask;
    public bool AllowSupersetExitMask => allowSupersetExitMask;
    public bool LockExitsUntilClear => lockExitsUntilClear;
    public bool StartEncounterOnEnter => startEncounterOnEnter;
    public bool RequiresClearBeforeExit => requiresClearBeforeExit;
    public DropTable ClearRewardTable => clearRewardTable;
    public int ClearRewardRolls => Mathf.Max(0, clearRewardRolls);
    public WeaponRarity ClearRewardRarity => clearRewardRarity;
    public float RiskLevel => Mathf.Clamp01(riskLevel);
    public string MapHint => mapHint;
    public Sprite MapIcon => mapIcon;
    public bool HasClearReward => clearRewardTable != null && ClearRewardRolls > 0;

    public bool SupportsExitMask(RoomExitMask requiredMask)
    {
        return SupportsExitMask(requiredMask, 0);
    }

    public bool SupportsExitMask(RoomExitMask requiredMask, int rotationSteps)
    {
        if (allowSupersetExitMask)
            return (GetRotatedExitMask(rotationSteps) & requiredMask) == requiredMask;

        return GetRotatedExitMask(rotationSteps) == requiredMask;
    }

    public bool TryGetRotationForExitMask(RoomExitMask requiredMask, bool exactMaskOnly, out int rotationSteps)
    {
        rotationSteps = 0;
        for (int i = 0; i < 4; i++)
        {
            RoomExitMask rotatedMask = GetRotatedExitMask(i);
            bool supported = exactMaskOnly
                ? rotatedMask == requiredMask
                : SupportsExitMask(requiredMask, i);

            if (!supported)
                continue;

            rotationSteps = i;
            return true;
        }

        return false;
    }

    public RoomExitMask GetRotatedExitMask(int rotationSteps)
    {
        return RoomExitDirectionUtility.RotateMask(exitMask, rotationSteps);
    }
}
