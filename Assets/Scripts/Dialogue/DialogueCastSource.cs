using UnityEngine;

/// <summary>
/// Where the stage gets the visual for one cast member.
///
/// Party members come from a live <see cref="CharacteContext"/> so the clone picks up their real
/// equipment. Scene-authored NPCs have no context at all — they are plain model prefabs — so they
/// supply their model transform directly. Both end up as the same thing to the stage: a transform to
/// clone and a name to show.
/// </summary>
public readonly struct DialogueCastSource
{
    /// <summary>The subtree that gets cloned onto the stage.</summary>
    public Transform ModelRoot { get; }

    /// <summary>Name shown on the plate when this cast member speaks.</summary>
    public string DisplayName { get; }

    /// <summary>
    /// The actual character behind this source. A sequence may reach it through a role key
    /// (`role.Player`), but a pose profile is per character, so lookups use this rather than the key.
    /// </summary>
    public string CharacterId { get; }

    /// <summary>The live character this came from, or null for a scene-authored NPC.</summary>
    public CharacteContext Context { get; }

    public bool IsValid => ModelRoot != null;

    DialogueCastSource(Transform modelRoot, string displayName, string characterId, CharacteContext context)
    {
        ModelRoot = modelRoot;
        DisplayName = displayName;
        CharacterId = characterId;
        Context = context;
    }

    /// <summary>
    /// A live party member. The clone is taken from <see cref="CharacterVisualController.ModelRoot"/>,
    /// which is where the built model and its mounted weapon already live.
    /// </summary>
    public static DialogueCastSource FromCharacter(CharacteContext context)
    {
        if (context == null)
            return default;

        context.ResolveReferences();

        CharacterVisualController visual = context.Visual;
        Transform modelRoot = visual != null ? visual.ModelRoot : null;
        if (modelRoot == null)
        {
            Debug.LogWarning(
                $"[Dialogue] '{context.name}' has no CharacterVisualController.ModelRoot; " +
                "its dialogue slot stays empty.", context);
            return default;
        }

        string name = context.baseStats != null && !string.IsNullOrWhiteSpace(context.baseStats.characterName)
            ? context.baseStats.characterName
            : context.name;

        string characterId = context.baseStats != null ? context.baseStats.characterId : null;
        return new DialogueCastSource(modelRoot, name, characterId, context);
    }

    /// <summary>A scene-authored actor — an NPC model with no character context behind it.</summary>
    public static DialogueCastSource FromTransform(Transform modelRoot, string displayName, string characterId)
    {
        if (modelRoot == null)
            return default;

        return new DialogueCastSource(
            modelRoot,
            string.IsNullOrWhiteSpace(displayName) ? modelRoot.name : displayName,
            characterId,
            null);
    }
}
