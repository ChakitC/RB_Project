using System;
using UnityEngine;

/// <summary>
/// A change to who stands on the stage, applied just before its line is shown.
///
/// The stage has exactly three slots, so bringing someone on is always also taking someone off:
/// occupying a slot that is already taken replaces its occupant. That is the whole cap — there is no
/// separate "max 3" rule to enforce, because there is nowhere for a fourth actor to stand.
/// </summary>
[Serializable]
public sealed class DialogueStageChange
{
    [Tooltip("Who to place in the slot. Any id the conversation can resolve — a party member's " +
             "CharacterStats.characterId, or a scene actor supplied by the trigger. Leave empty to " +
             "clear the slot and leave it empty.")]
    public string characterId;

    [Tooltip("Slot to place them in. Whoever is standing there is taken off.")]
    public DialogueSlot slot = DialogueSlot.Center;

    [Tooltip("Pose held while not speaking. Empty uses the character's profile idle.")]
    public string idlePoseId;

    /// <summary>True when this change clears its slot instead of filling it.</summary>
    public bool IsClear => string.IsNullOrWhiteSpace(characterId);
}
