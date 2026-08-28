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

    [Tooltip("Not yet wired: every cast entry currently behaves as optional. When the live party " +
             "has no match the slot is simply left empty and the remaining actors re-centre.")]
    public bool optional = true;

    public bool IsValid => !string.IsNullOrWhiteSpace(characterId);
}
