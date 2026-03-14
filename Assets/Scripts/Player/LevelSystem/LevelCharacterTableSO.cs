using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Progression/Level Table (Multi-Range Generated)", fileName = "LevelTable_MultiRange")]
public class LevelTableSO : ScriptableObject
{
    [Header("Max Level")]
    [Min(1)]
    [SerializeField] private int maxLevel = 100;

    [Header("Rows (Level 1..MaxLevel)")]
    [SerializeField] private List<LevelRow> levels = new();

    [Header("Ranges (Top = Higher Priority)")]
    [SerializeField] private List<RangeGenSettings> ranges = new()
    {
        new RangeGenSettings
        {
            name = "Early (1-20)",
            startLevel = 1,
            endLevel = 20,
            mode = GenMode.Curve,
            minXpToNext = 50,
            maxXpToNext = 600,
            curve01 = DefaultEarlyCurve()
        },
        new RangeGenSettings
        {
            name = "Mid (21-60)",
            startLevel = 21,
            endLevel = 60,
            mode = GenMode.Curve,
            minXpToNext = 300,
            maxXpToNext = 1800,
            curve01 = DefaultMidCurve()
        },
        new RangeGenSettings
        {
            name = "Late (61-100)",
            startLevel = 61,
            endLevel = 100,
            mode = GenMode.Curve,
            minXpToNext = 1200,
            maxXpToNext = 5000,
            curve01 = DefaultLateCurve()
        },
    };

    [Header("Fallback (if no range matches)")]
    [SerializeField] private RangeGenSettings fallback = new RangeGenSettings
    {
        name = "Fallback",
        startLevel = 1,
        endLevel = 9999,
        mode = GenMode.Quadratic,
        minXpToNext = 100,
        maxXpToNext = 5000,
        quadratic = new QuadraticParams { a = 80, b = 12, c = 0.9f }
    };

    public int MaxLevel => maxLevel;
    public IReadOnlyList<LevelRow> RawLevels => levels;

    // -------------------- Public API --------------------

    /// <summary>XP ที่ต้องใช้จากเลเวลนี้เพื่อไปเลเวลถัดไป (ถ้าเป็น MaxLevel จะคืน int.MaxValue)</summary>
    public int GetXpToNext(int level)
    {
        level = Mathf.Clamp(level, 1, MaxLevel);
        if (level >= MaxLevel) return int.MaxValue;

        EnsureSize();
        return Mathf.Max(0, levels[level - 1].xpToNext);
    }

    /// <summary>
    /// XP รวมที่ต้องใช้เพื่อ "ไปถึงเลเวล targetLevel"
    /// ตัวอย่าง: targetLevel=1 => 0, targetLevel=2 => xpToNext(1), targetLevel=10 => sum xpToNext(1..9)
    /// </summary>
    public long GetTotalXpToReach(int targetLevel)
    {
        EnsureSize();
        targetLevel = Mathf.Clamp(targetLevel, 1, MaxLevel);

        long total = 0;
        // รวมจนถึงเลเวลก่อนหน้า targetLevel
        for (int lvl = 1; lvl < targetLevel; lvl++)
        {
            total += (long)Mathf.Max(0, levels[lvl - 1].xpToNext);
        }
        return total;
    }

    /// <summary>XP รวมเพื่อถึง MaxLevel</summary>
    public long GetTotalXpForMax() => GetTotalXpToReach(MaxLevel);

    /// <summary>
    /// % ความคืบหน้าของเลเวลปัจจุบัน (0..1) จาก currentXp กับ xpToNext(level)
    /// </summary>
    public float GetProgress01(int level, int currentXp)
    {
        int need = GetXpToNext(level);
        if (need <= 0 || need == int.MaxValue) return 1f;
        return Mathf.Clamp01((float)currentXp / need);
    }

    /// <summary>
    /// แปลง totalXp (สะสมรวมตั้งแต่ Lv1) -> level + xp ที่เหลือในเลเวลนั้น
    /// totalXp=0 => Lv1, remainder=0
    /// </summary>
    public int GetLevelFromTotalXp(long totalXp, out int remainderXp)
    {
        EnsureSize();

        if (totalXp <= 0)
        {
            remainderXp = 0;
            return 1;
        }

        long xp = totalXp;
        int level = 1;

        while (level < MaxLevel)
        {
            int need = Mathf.Max(0, levels[level - 1].xpToNext);
            if (need <= 0) { level++; continue; } // กัน table แปลก ๆ
            if (xp < need) break;

            xp -= need;
            level++;
        }

        remainderXp = (int)Mathf.Clamp((long)0, xp, int.MaxValue);
        return level;
    }

    // -------------------- Generate --------------------

    [ContextMenu("Generate XP Table (Multi-Range, Respect lockXp)")]
    public void GenerateXpTable()
    {
        EnsureSize();
        ValidateRanges();

        for (int lvl = 1; lvl <= MaxLevel; lvl++)
        {
            int idx = lvl - 1;

            // MaxLevel ไม่มี xpToNext
            if (lvl == MaxLevel)
            {
                var last = levels[idx];
                last.xpToNext = 0;
                levels[idx] = last;
                continue;
            }

            // ล็อกไว้ไม่ให้ทับ
            if (levels[idx].lockXp) continue;

            var r = GetRangeForLevel(lvl);
            int xp = GenerateForLevelInRange(lvl, r);

            xp = Mathf.Clamp(xp, Mathf.Max(0, r.minXpToNext), Mathf.Max(0, r.maxXpToNext));

            var row = levels[idx];
            row.xpToNext = xp;
            levels[idx] = row;
        }

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    private int GenerateForLevelInRange(int lvl, RangeGenSettings r)
    {
        return r.mode switch
        {
            GenMode.Curve     => GenerateByCurve01(lvl, r),
            GenMode.Quadratic => GenerateByQuadratic(lvl, r),
            _ => 100
        };
    }

    private int GenerateByCurve01(int lvl, RangeGenSettings r)
    {
        int start = Mathf.Clamp(r.startLevel, 1, MaxLevel);
        int end   = Mathf.Clamp(r.endLevel,   1, MaxLevel);

        if (end < start) (start, end) = (end, start);

        int genEnd = Mathf.Min(end, MaxLevel - 1);

        float t = (genEnd <= start) ? 0f : (float)(lvl - start) / (genEnd - start);
        t = Mathf.Clamp01(t);

        float v01 = Mathf.Clamp01(r.curve01 != null ? r.curve01.Evaluate(t) : t);
        return Mathf.RoundToInt(Mathf.Lerp(r.minXpToNext, r.maxXpToNext, v01));
    }

    private int GenerateByQuadratic(int lvl, RangeGenSettings r)
    {
        float L = lvl;
        float xp = r.quadratic.a + r.quadratic.b * L + r.quadratic.c * L * L;
        return Mathf.RoundToInt(xp);
    }

    // -------------------- Range Logic --------------------

    private RangeGenSettings GetRangeForLevel(int level)
    {
        if (ranges != null)
        {
            for (int i = 0; i < ranges.Count; i++)
            {
                var r = ranges[i];
                if (IsLevelInRange(level, r)) return r;
            }
        }
        return fallback;
    }

    private bool IsLevelInRange(int level, RangeGenSettings r)
    {
        int start = Mathf.Min(r.startLevel, r.endLevel);
        int end   = Mathf.Max(r.startLevel, r.endLevel);

        start = Mathf.Clamp(start, 1, MaxLevel);
        end   = Mathf.Clamp(end,   1, MaxLevel);

        return level >= start && level <= end;
    }

    private void ValidateRanges()
    {
        if (ranges == null) ranges = new List<RangeGenSettings>();
        if (fallback.curve01 == null) fallback.curve01 = AnimationCurve.Linear(0, 0, 1, 1);

        for (int i = 0; i < ranges.Count; i++)
        {
            var r = ranges[i];
            r.startLevel = Mathf.Max(1, r.startLevel);
            r.endLevel   = Mathf.Max(1, r.endLevel);

            r.minXpToNext = Mathf.Max(0, r.minXpToNext);
            r.maxXpToNext = Mathf.Max(0, r.maxXpToNext);
            if (r.maxXpToNext < r.minXpToNext) (r.minXpToNext, r.maxXpToNext) = (r.maxXpToNext, r.minXpToNext);

            if (r.curve01 == null) r.curve01 = AnimationCurve.Linear(0, 0, 1, 1);
            ranges[i] = r;
        }

        fallback.startLevel = Mathf.Max(1, fallback.startLevel);
        fallback.endLevel   = Mathf.Max(1, fallback.endLevel);

        fallback.minXpToNext = Mathf.Max(0, fallback.minXpToNext);
        fallback.maxXpToNext = Mathf.Max(0, fallback.maxXpToNext);
        if (fallback.maxXpToNext < fallback.minXpToNext) (fallback.minXpToNext, fallback.maxXpToNext) = (fallback.maxXpToNext, fallback.minXpToNext);

        if (fallback.curve01 == null) fallback.curve01 = AnimationCurve.Linear(0, 0, 1, 1);
    }

    // -------------------- Size / Validate --------------------

    private void OnValidate()
    {
        maxLevel = Mathf.Max(1, maxLevel);
        EnsureSize();
        ValidateRanges();
    }

    private void EnsureSize()
    {
        if (levels == null) levels = new List<LevelRow>();

        while (levels.Count < maxLevel) levels.Add(LevelRow.Default());
        if (levels.Count > maxLevel) levels.RemoveRange(maxLevel, levels.Count - maxLevel);

        // บังคับ MaxLevel xpToNext = 0
        if (maxLevel >= 1)
        {
            var last = levels[maxLevel - 1];
            last.xpToNext = 0;
            levels[maxLevel - 1] = last;
        }
    }

    // -------------------- Default Curves --------------------

    private static AnimationCurve DefaultEarlyCurve()
    {
        return new AnimationCurve(
            new Keyframe(0f, 0.05f),
            new Keyframe(0.5f, 0.25f),
            new Keyframe(1f, 0.55f)
        );
    }

    private static AnimationCurve DefaultMidCurve()
    {
        return new AnimationCurve(
            new Keyframe(0f, 0.20f),
            new Keyframe(0.5f, 0.55f),
            new Keyframe(1f, 0.85f)
        );
    }

    private static AnimationCurve DefaultLateCurve()
    {
        return new AnimationCurve(
            new Keyframe(0f, 0.35f),
            new Keyframe(0.65f, 0.80f),
            new Keyframe(1f, 1.00f)
        );
    }
}

// -------------------- Data Structs --------------------

public enum GenMode
{
    Curve,
    Quadratic
}

[Serializable]
public struct QuadraticParams
{
    public float a, b, c;
}

[Serializable]
public struct RangeGenSettings
{
    public string name;

    [Min(1)] public int startLevel;
    [Min(1)] public int endLevel;

    public GenMode mode;

    [Min(0)] public int minXpToNext;
    [Min(0)] public int maxXpToNext;

    [Tooltip("Curve 0..1 แล้วระบบจะ Lerp(min,max)")]
    public AnimationCurve curve01;

    [Tooltip("xp = a + b*L + c*L^2 (mode=Quadratic)")]
    public QuadraticParams quadratic;
}

[Serializable]
public struct LevelRow
{
    [Min(0)] public int xpToNext;
    public bool lockXp;

    public static LevelRow Default() => new LevelRow
    {
        xpToNext = 100,
        lockXp = false
    };
}
