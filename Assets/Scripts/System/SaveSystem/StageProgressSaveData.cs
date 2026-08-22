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
    /// <summary>
    /// Bumped whenever the shape of this file changes. Version 0 is a file written before the field
    /// existed: JsonUtility leaves it at 0 when the key is absent, which is exactly what identifies
    /// a pre-versioning save.
    /// </summary>
    public const int CurrentVersion = 1;

    public int schemaVersion;
    public List<StageProgressEntry> entries = new();

    public int GetProgress(string stageId)
    {
        StageProgressEntry entry = FindEntry(stageId);
        return entry != null ? Math.Max(0, entry.progressCount) : 0;
    }

    /// <summary>
    /// Progress for a stage that may have been saved under an earlier id. The current id wins; a
    /// legacy id is only consulted when nothing has been written under the current one yet, so a
    /// rename never loses what the player already cleared.
    /// </summary>
    public int GetProgress(string stageId, IReadOnlyList<string> legacyStageIds)
    {
        if (FindEntry(stageId) != null)
            return GetProgress(stageId);

        if (legacyStageIds == null)
            return 0;

        for (int i = 0; i < legacyStageIds.Count; i++)
        {
            StageProgressEntry legacy = FindEntry(legacyStageIds[i]);
            if (legacy != null)
                return Math.Max(0, legacy.progressCount);
        }

        return 0;
    }

    public void SetProgress(string stageId, int progressCount)
    {
        if (string.IsNullOrWhiteSpace(stageId))
            return;

        entries ??= new List<StageProgressEntry>();
        StageProgressEntry entry = FindEntry(stageId);
        if (entry == null)
        {
            entry = new StageProgressEntry { stageId = stageId };
            entries.Add(entry);
        }

        entry.progressCount = Math.Max(0, progressCount);
        schemaVersion = CurrentVersion;
    }

    /// <summary>
    /// Rewrites entries saved under a legacy id to the current one. Returns true when the file
    /// changed and should be written back.
    /// </summary>
    public bool MigrateStageId(string stageId, IReadOnlyList<string> legacyStageIds)
    {
        if (string.IsNullOrWhiteSpace(stageId) || legacyStageIds == null || entries == null)
            return false;

        bool changed = false;
        StageProgressEntry current = FindEntry(stageId);

        for (int i = 0; i < legacyStageIds.Count; i++)
        {
            string legacyId = legacyStageIds[i];
            if (string.IsNullOrWhiteSpace(legacyId) ||
                string.Equals(legacyId, stageId, StringComparison.Ordinal))
            {
                continue;
            }

            StageProgressEntry legacy = FindEntry(legacyId);
            if (legacy == null)
                continue;

            if (current == null)
            {
                // Reuse the row so the highest legacy value is not lost to ordering.
                legacy.stageId = stageId;
                current = legacy;
            }
            else
            {
                current.progressCount = Math.Max(current.progressCount, Math.Max(0, legacy.progressCount));
                entries.Remove(legacy);
            }

            changed = true;
        }

        return changed;
    }

    /// <summary>Marks a file read from disk as being at the current schema. Returns true when it was not.</summary>
    public bool NormalizeSchemaVersion()
    {
        if (schemaVersion == CurrentVersion)
            return false;

        schemaVersion = CurrentVersion;
        return true;
    }

    StageProgressEntry FindEntry(string stageId)
    {
        if (string.IsNullOrWhiteSpace(stageId) || entries == null)
            return null;

        return entries.Find(candidate =>
            candidate != null && string.Equals(candidate.stageId, stageId, StringComparison.Ordinal));
    }
}
