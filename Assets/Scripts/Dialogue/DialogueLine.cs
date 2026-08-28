using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One advanceable beat of a dialogue sequence: who speaks, how they are posed while speaking, and
/// what is typed out. Voice is data only — it never advances the line.
/// </summary>
[Serializable]
public sealed class DialogueLine
{
    [Tooltip("CharacterStats.characterId of the speaker. Must be one of the sequence's cast entries. " +
             "Leave empty for a narration line with no emphasized actor.")]
    public string speakerCharacterId;

    [Tooltip("Optional override for the name plate. Empty uses the speaker's CharacterStats name.")]
    public string speakerNameOverride;

    [Tooltip("Pose played on the speaker for this line. Empty, or an id the character's dialogue " +
             "profile does not define, falls back to that profile's idle pose.")]
    public string poseId;

    [TextArea(2, 6)]
    public string text;

    [Tooltip("Stage changes applied just before this line is shown — bring a character on, or clear " +
             "a slot. Occupying a taken slot replaces whoever was standing there, which is how the " +
             "three-slot cap enforces itself.")]
    public List<DialogueStageChange> stageChanges = new();

    [Tooltip("Played unscaled when the line starts. Does not auto-advance the line.")]
    public AudioClip voice;

    [Tooltip("Characters revealed per unscaled second. 0 uses the sequence default.")]
    [Min(0f)] public float typewriterCharactersPerSecond;

    public bool HasSpeaker => !string.IsNullOrWhiteSpace(speakerCharacterId);

    public bool HasStageChanges => stageChanges != null && stageChanges.Count > 0;
}
