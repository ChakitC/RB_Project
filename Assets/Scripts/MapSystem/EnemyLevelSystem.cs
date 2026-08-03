using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemyLevelSystem : MonoBehaviour
{
    [SerializeField, Min(1)] private int level = 1;

    public int Level => Mathf.Max(1, level);
    public event Action<int> LevelChanged;

    public void SetLevel(int value)
    {
        int resolved = Mathf.Max(1, value);
        if (level == resolved)
            return;

        level = resolved;
        LevelChanged?.Invoke(level);

        StatsHub statsHub = GetComponentInChildren<StatsHub>(true);
        if (statsHub == null)
            statsHub = GetComponentInParent<StatsHub>();
        statsHub?.MarkDirty();
    }
}
