using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Maps <see cref="CharacterStats.characterId"/> to the character's dialogue pose profile. One asset
/// for the project, referenced by the dialogue stage, so sequences can name characters by id without
/// holding direct asset references to every profile.
/// </summary>
[CreateAssetMenu(menuName = "Game/Dialogue/Dialogue Profile Database", fileName = "DialogueProfileDatabase")]
public sealed class DialogueProfileDatabaseSO : ScriptableObject
{
    [SerializeField] private List<CharacterDialogueAnimationProfileSO> profiles = new();

    Dictionary<string, CharacterDialogueAnimationProfileSO> lookup;

    public IReadOnlyList<CharacterDialogueAnimationProfileSO> Profiles => profiles;

    public CharacterDialogueAnimationProfileSO Find(string characterId)
    {
        if (string.IsNullOrWhiteSpace(characterId))
            return null;

        EnsureLookup();
        return lookup.TryGetValue(characterId, out CharacterDialogueAnimationProfileSO profile)
            ? profile
            : null;
    }

    /// <summary>Drops the cached lookup so an asset edited in the editor is picked up on the next find.</summary>
    public void InvalidateLookup()
    {
        lookup = null;
    }

    void EnsureLookup()
    {
        if (lookup != null)
            return;

        lookup = new Dictionary<string, CharacterDialogueAnimationProfileSO>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < profiles.Count; i++)
        {
            CharacterDialogueAnimationProfileSO profile = profiles[i];
            if (profile == null || string.IsNullOrWhiteSpace(profile.CharacterId))
                continue;

            lookup[profile.CharacterId] = profile;
        }
    }

    void OnValidate()
    {
        InvalidateLookup();
    }

    /// <summary>Every authoring problem in the database itself and in the profiles it lists.</summary>
    public void CollectValidationIssues(List<string> issues)
    {
        if (issues == null)
            throw new ArgumentNullException(nameof(issues));

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < profiles.Count; i++)
        {
            CharacterDialogueAnimationProfileSO profile = profiles[i];
            if (profile == null)
            {
                issues.Add($"'{name}': profile entry {i} is missing.");
                continue;
            }

            if (!string.IsNullOrWhiteSpace(profile.CharacterId) && !seen.Add(profile.CharacterId))
                issues.Add($"'{name}': two profiles claim characterId '{profile.CharacterId}'.");

            profile.CollectValidationIssues(issues);
        }
    }
}
