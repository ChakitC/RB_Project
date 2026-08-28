using System;
using UnityEngine;

/// <summary>
/// One actor placed on the dialogue stage for the whole sequence. Slots are fixed: an actor never
/// moves when the speaker changes, it is only emphasized in place.
/// </summary>
[Serializable]
public sealed class DialogueCastEntry
{
    [Tooltip("Who stands here. Either a CharacterStats.characterId for a fixed actor (NPCs), or a " +
             "party role key — 'role.Player', 'role.PartySlot1', 'role.PartySlot2', 'role.Helper' — " +
             "which resolves against whatever party the player actually deployed.")]
    public string characterId;

    public DialogueSlot slot = DialogueSlot.Center;

    [Tooltip("Pose held whenever this actor is not the speaker. Empty uses the profile's idle pose.")]
    public string idlePoseId;

    [Tooltip("On (default): if nothing resolves this key the slot is left empty and the remaining " +
             "portraits re-centre — what a conversation written for 'whoever is deployed' wants. " +
             "Off: the conversation refuses to start at all when this key cannot be resolved, before " +
             "the world is paused or the HUD hidden. Use Off only when the line makes no sense " +
             "without that character. Applies to the opening cast only; a mid-conversation stage " +
             "change that cannot resolve keeps the current occupant and warns.")]
    public bool optional = true;

    public bool IsValid => !string.IsNullOrWhiteSpace(characterId);
}
