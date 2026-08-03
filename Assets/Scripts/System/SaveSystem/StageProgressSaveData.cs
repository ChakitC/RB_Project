using System;
using System.Collections.Generic;

[Serializable]
public sealed class StageProgressEntry
{
    public string stageId;
    public int progressCount;
}

[Serializable]
public sealed class StageProgressSaveFile
{
    public List<StageProgressEntry> entries = new();

    public int GetProgress(string stageId)
    {
        if (string.IsNullOrWhiteSpace(stageId) || entries == null)
            return 0;

        StageProgressEntry entry = entries.Find(candidate =>
            candidate != null && string.Equals(candidate.stageId, stageId, StringComparison.Ordinal));
        return entry != null ? Math.Max(0, entry.progressCount) : 0;
    }

    public void SetProgress(string stageId, int progressCount)
    {
        if (string.IsNullOrWhiteSpace(stageId))
            return;

        entries ??= new List<StageProgressEntry>();
        StageProgressEntry entry = entries.Find(candidate =>
            candidate != null && string.Equals(candidate.stageId, stageId, StringComparison.Ordinal));
        if (entry == null)
        {
            entry = new StageProgressEntry { stageId = stageId };
            entries.Add(entry);
        }

        entry.progressCount = Math.Max(0, progressCount);
    }
}
