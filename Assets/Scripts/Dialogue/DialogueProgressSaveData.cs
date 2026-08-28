using System;
using System.Collections.Generic;

/// <summary>
/// Persisted completion of play-once dialogues, keyed by <see cref="DialogueSequenceSO.DialogueId"/>.
/// Shaped like <see cref="StageProgressSaveFile"/> so it reads back the same way: an absent
/// schemaVersion identifies a file written before versioning existed.
/// </summary>
[Serializable]
public sealed class DialogueProgressEntry
{
    public string dialogueId;
    public int completedCount;
}

[Serializable]
public sealed class DialogueProgressSaveFile
{
    public const int CurrentVersion = 1;

    public int schemaVersion;
    public List<DialogueProgressEntry> entries = new();

    public bool IsCompleted(string dialogueId)
    {
        return GetCompletedCount(dialogueId) > 0;
    }

    public int GetCompletedCount(string dialogueId)
    {
        DialogueProgressEntry entry = FindEntry(dialogueId);
        return entry != null ? Math.Max(0, entry.completedCount) : 0;
    }

    public void MarkCompleted(string dialogueId)
    {
        if (string.IsNullOrWhiteSpace(dialogueId))
            return;

        entries ??= new List<DialogueProgressEntry>();
        DialogueProgressEntry entry = FindEntry(dialogueId);
        if (entry == null)
        {
            entry = new DialogueProgressEntry { dialogueId = dialogueId };
            entries.Add(entry);
        }

        entry.completedCount = Math.Max(0, entry.completedCount) + 1;
        schemaVersion = CurrentVersion;
    }

    /// <summary>Marks a file read from disk as being at the current schema. Returns true when it was not.</summary>
    public bool NormalizeSchemaVersion()
    {
        if (schemaVersion == CurrentVersion)
            return false;

        schemaVersion = CurrentVersion;
        return true;
    }

    DialogueProgressEntry FindEntry(string dialogueId)
    {
        if (string.IsNullOrWhiteSpace(dialogueId) || entries == null)
            return null;

        return entries.Find(candidate =>
            candidate != null &&
            string.Equals(candidate.dialogueId, dialogueId, StringComparison.Ordinal));
    }
}
