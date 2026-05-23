using System;
using UnityEngine;

[Serializable]
public sealed class MapPitySystem
{
    [Tooltip("จำนวนห้องแดงติดกันสูงสุดก่อนระบบบังคับให้ห้องถัดไปเป็นห้องน้ำเงิน")]
    [SerializeField, Min(1)] private int maxRedStreakBeforeBlue = 3;

    [Tooltip("ชนิดห้องน้ำเงินที่จะใช้เมื่อระบบ pity ทำงาน")]
    [SerializeField] private MapNodeType forcedBlueType = MapNodeType.Reward;

    public int MaxRedStreakBeforeBlue => Mathf.Max(1, maxRedStreakBeforeBlue);
    public MapNodeType ForcedBlueType => IsBlueNodeType(forcedBlueType) ? forcedBlueType : MapNodeType.Reward;

    public bool ShouldForceBlue(int redStreak)
    {
        return redStreak >= MaxRedStreakBeforeBlue;
    }

    public static bool IsRedNodeType(MapNodeType type)
    {
        return type == MapNodeType.Combat ||
               type == MapNodeType.Elite ||
               type == MapNodeType.Ambush ||
               type == MapNodeType.Trap;
    }

    public static bool IsBlueNodeType(MapNodeType type)
    {
        return type == MapNodeType.Reward ||
               type == MapNodeType.Shop ||
               type == MapNodeType.Heal ||
               type == MapNodeType.Upgrade;
    }

    public static bool IsDeadEndRewardType(MapNodeType type)
    {
        return IsBlueNodeType(type) ||
               type == MapNodeType.Elite ||
               type == MapNodeType.Event;
    }
}
