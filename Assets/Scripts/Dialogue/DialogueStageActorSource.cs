using UnityEngine;

/// <summary>
/// Lets a scene object put itself on the dialogue stage as a cast member.
///
/// Party members are found automatically through <see cref="PartyRuntime"/>, but an NPC is usually
/// just a model prefab with no <see cref="CharacteContext"/> behind it, so it has to declare itself.
/// Put this on the NPC, give it the id the sequence casts, and any
/// <see cref="DialogueTrigger"/> that lists it will hand it to the director.
/// </summary>
[DisallowMultipleComponent]
public sealed class DialogueStageActorSource : MonoBehaviour
{
    [SerializeField, Tooltip("Id the dialogue sequence casts this actor under. Keep NPC ids distinct " +
                             "from CharacterStats.characterId values, e.g. 'npc.abbygail'.")]
    private string characterId;

    [SerializeField, Tooltip("Name shown on the dialogue box. Empty falls back to the characterId.")]
    private string displayName;

    [SerializeField, Tooltip("Subtree cloned onto the stage. Empty uses this GameObject, which is " +
                             "what a plain model prefab wants.")]
    private Transform modelRoot;

    public string CharacterId => characterId;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? characterId : displayName;
    public Transform ModelRoot => modelRoot != null ? modelRoot : transform;

    public bool IsValid => !string.IsNullOrWhiteSpace(characterId) && ModelRoot != null;

    public DialogueCastSource BuildSource()
    {
        return DialogueCastSource.FromTransform(ModelRoot, DisplayName, characterId);
    }
}
