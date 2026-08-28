using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A reusable, scene-free linear conversation: a cast of up to three slotted actors and an ordered
/// list of lines. Scene consequences (quests, doors, spawns) belong on the
/// <see cref="DialogueTrigger"/> that plays this asset, never here, so the same sequence can be
/// reused from several places.
/// </summary>
[CreateAssetMenu(menuName = "Game/Dialogue/Dialogue Sequence", fileName = "DialogueSequence")]
public sealed class DialogueSequenceSO : ScriptableObject
{
    public const int MaxCastSize = 3;

    [Header("Identity")]
    [SerializeField, Tooltip("Stable id used to persist completion. Renaming this asset is safe; " +
                             "changing this id makes every player see the dialogue again.")]
    private string dialogueId;

    [Header("Cast")]
    [SerializeField, Tooltip("Up to three actors, one per stage slot.")]
    private List<DialogueCastEntry> cast = new();

    [Header("Lines")]
    [SerializeField] private List<DialogueLine> lines = new();

    [Header("Presentation")]
    [SerializeField, Tooltip("Optional light rig override. Empty uses the stage's default rig.")]
    private DialogueLightRigSO lightRig;

    [SerializeField, Min(1f), Tooltip("Default typewriter speed for lines that do not override it.")]
    private float typewriterCharactersPerSecond = 40f;

    [SerializeField, Tooltip("Allow holding the skip button to end the whole sequence.")]
    private bool allowHoldToSkip = true;

    [SerializeField, Min(0.05f)] private float holdToSkipSeconds = 0.75f;

    public string DialogueId => dialogueId;
    public IReadOnlyList<DialogueCastEntry> Cast => cast;
    public IReadOnlyList<DialogueLine> Lines => lines;
    public DialogueLightRigSO LightRig => lightRig;
    public float TypewriterCharactersPerSecond => Mathf.Max(1f, typewriterCharactersPerSecond);
    public bool AllowHoldToSkip => allowHoldToSkip;
    public float HoldToSkipSeconds => Mathf.Max(0.05f, holdToSkipSeconds);

    public float ResolveTypewriterSpeed(DialogueLine line)
    {
        if (line != null && line.typewriterCharactersPerSecond > 0f)
            return line.typewriterCharactersPerSecond;

        return TypewriterCharactersPerSecond;
    }

    public DialogueCastEntry FindCastEntry(string characterId)
    {
        if (string.IsNullOrWhiteSpace(characterId) || cast == null)
            return null;

        for (int i = 0; i < cast.Count; i++)
        {
            DialogueCastEntry entry = cast[i];
            if (entry != null && string.Equals(entry.characterId, characterId, StringComparison.OrdinalIgnoreCase))
                return entry;
        }

        return null;
    }

    /// <summary>
    /// Every cast key that can end up on stage: the opening cast plus everyone brought on by a
    /// `stageChanges` entry.
    ///
    /// Validation has to use this rather than the opening cast alone. A character who only ever
    /// appears mid-conversation is still a character who needs a source and a pose profile, and
    /// checking `FindCastEntry` on its own reported those as unused while leaving the real gap
    /// unreported.
    /// </summary>
    public void CollectAppearingCastKeys(ICollection<string> keys)
    {
        if (keys == null)
            throw new ArgumentNullException(nameof(keys));

        for (int i = 0; cast != null && i < cast.Count; i++)
        {
            DialogueCastEntry entry = cast[i];
            if (entry != null && entry.IsValid)
                keys.Add(entry.characterId);
        }

        for (int l = 0; lines != null && l < lines.Count; l++)
        {
            DialogueLine line = lines[l];
            if (line == null || !line.HasStageChanges)
                continue;

            for (int c = 0; c < line.stageChanges.Count; c++)
            {
                DialogueStageChange change = line.stageChanges[c];

                // A clear has no characterId by definition; it empties a slot.
                if (change != null && !change.IsClear)
                    keys.Add(change.characterId);
            }
        }
    }

    /// <summary>Every authoring problem that would stop this sequence from playing correctly.</summary>
    public void CollectValidationIssues(List<string> issues)
    {
        if (issues == null)
            throw new ArgumentNullException(nameof(issues));

        if (string.IsNullOrWhiteSpace(dialogueId))
            issues.Add($"'{name}': dialogueId is empty; play-once completion cannot be persisted.");

        if (lines == null || lines.Count == 0)
            issues.Add($"'{name}': the sequence has no lines.");

        if (cast == null || cast.Count == 0)
        {
            issues.Add($"'{name}': the sequence has no cast; nobody will appear on stage.");
        }
        else
        {
            if (cast.Count > MaxCastSize)
                issues.Add($"'{name}': cast has {cast.Count} entries; the stage has {MaxCastSize} slots.");

            var slots = new HashSet<DialogueSlot>();
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < cast.Count; i++)
            {
                DialogueCastEntry entry = cast[i];
                if (entry == null || !entry.IsValid)
                {
                    issues.Add($"'{name}': cast entry {i} has no characterId.");
                    continue;
                }

                if (!slots.Add(entry.slot))
                    issues.Add($"'{name}': slot '{entry.slot}' is used by more than one cast entry.");

                if (!ids.Add(entry.characterId))
                    issues.Add($"'{name}': character '{entry.characterId}' appears in the cast twice.");
            }
        }

        if (lines == null)
            return;

        // Who stands where changes as the sequence runs, so validation has to walk the line-up
        // forward rather than judging every line against the opening cast.
        var onStage = new Dictionary<DialogueSlot, string>();
        for (int i = 0; cast != null && i < cast.Count; i++)
        {
            DialogueCastEntry entry = cast[i];
            if (entry != null && entry.IsValid)
                onStage[entry.slot] = entry.characterId;
        }

        for (int i = 0; i < lines.Count; i++)
        {
            DialogueLine line = lines[i];
            if (line == null)
            {
                issues.Add($"'{name}': line {i} is null.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(line.text))
                issues.Add($"'{name}': line {i} has no text.");

            for (int c = 0; line.HasStageChanges && c < line.stageChanges.Count; c++)
            {
                DialogueStageChange change = line.stageChanges[c];
                if (change == null)
                {
                    issues.Add($"'{name}': line {i} has an empty stage change.");
                    continue;
                }

                if (change.IsClear)
                    onStage.Remove(change.slot);
                else
                    onStage[change.slot] = change.characterId;
            }

            if (!line.HasSpeaker)
                continue;

            bool present = false;
            foreach (KeyValuePair<DialogueSlot, string> occupant in onStage)
            {
                if (string.Equals(occupant.Value, line.speakerCharacterId, StringComparison.OrdinalIgnoreCase))
                {
                    present = true;
                    break;
                }
            }

            if (!present)
            {
                issues.Add(
                    $"'{name}': line {i} speaks as '{line.speakerCharacterId}', who is not on stage at " +
                    "that point. Use speakerNameOverride for a deliberate off-stage voice, or bring " +
                    "them on with a stage change.");
            }
        }
    }

    public bool IsPlayable(out string error)
    {
        var issues = new List<string>();
        CollectValidationIssues(issues);

        // Cast/pose gaps degrade gracefully at runtime; only a missing id or an empty script make the
        // sequence genuinely unplayable.
        bool blocked = string.IsNullOrWhiteSpace(dialogueId) || lines == null || lines.Count == 0;
        error = blocked ? string.Join("\n", issues) : string.Empty;
        return !blocked;
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(dialogueId))
            dialogueId = name;
    }
#endif
}
