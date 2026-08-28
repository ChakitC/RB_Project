using System;
using System.Collections.Generic;
using Animancer;
using UnityEngine;

/// <summary>
/// Per-character dialogue poses. One asset per character, keyed by
/// <see cref="CharacterStats.characterId"/>, so dialogue authoring never touches the gameplay
/// <see cref="CharacterAnimProfileSO"/>. A pose the sequence asks for but this profile does not
/// define falls back to <see cref="idlePose"/>.
/// </summary>
[CreateAssetMenu(
    menuName = "Game/Dialogue/Character Dialogue Animation Profile",
    fileName = "CharacterDialogueAnimationProfile")]
public sealed class CharacterDialogueAnimationProfileSO : ScriptableObject
{
    [Serializable]
    public sealed class PoseEntry
    {
        [Tooltip("Id referenced by DialogueLine.poseId. Case-insensitive.")]
        public string poseId;

        public ClipTransition clip;

        public bool IsValid => !string.IsNullOrWhiteSpace(poseId) && clip != null && clip.IsValid;
    }

    [Header("Identity")]
    [SerializeField, Tooltip("Must match CharacterStats.characterId of the character this profile poses.")]
    private string characterId;

    [Header("Poses")]
    [SerializeField, Tooltip("Played when a line asks for a pose this profile does not define, and " +
                             "for any actor that is on stage but not speaking.")]
    private ClipTransition idlePose;

    [SerializeField] private List<PoseEntry> poses = new();

    [Header("Portrait framing (0 = use the stage default)")]
    [SerializeField, Min(0f), Tooltip("How tall a slice of the world this character's portrait shows, " +
                                      "in metres. Larger pulls the camera back.")]
    private float framingViewHeight;

    public string CharacterId => characterId;

    /// <summary>Per-character framing override, or 0 when the stage default should be used.</summary>
    public float FramingViewHeight => framingViewHeight;
    public ClipTransition IdlePose => idlePose;
    public IReadOnlyList<PoseEntry> Poses => poses;

    public bool HasIdlePose => idlePose != null && idlePose.IsValid;

    /// <summary>
    /// Resolves a pose id to a clip. Returns false only when neither the pose nor the idle fallback
    /// is usable, which is the one case the caller has to leave the actor un-posed.
    /// </summary>
    public bool TryGetPose(string poseId, out ClipTransition clip)
    {
        if (!string.IsNullOrWhiteSpace(poseId) && poses != null)
        {
            for (int i = 0; i < poses.Count; i++)
            {
                PoseEntry entry = poses[i];
                if (entry == null || !entry.IsValid)
                    continue;

                if (string.Equals(entry.poseId, poseId, StringComparison.OrdinalIgnoreCase))
                {
                    clip = entry.clip;
                    return true;
                }
            }
        }

        clip = idlePose;
        return HasIdlePose;
    }

    /// <summary>Every authoring problem that would make this profile unusable at runtime.</summary>
    public void CollectValidationIssues(List<string> issues)
    {
        if (issues == null)
            throw new ArgumentNullException(nameof(issues));

        if (string.IsNullOrWhiteSpace(characterId))
            issues.Add($"'{name}': characterId is empty; no actor can resolve this profile.");

        if (!HasIdlePose)
            issues.Add($"'{name}': idlePose is missing. Any unmapped pose leaves the actor un-posed.");

        if (poses == null)
            return;

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < poses.Count; i++)
        {
            PoseEntry entry = poses[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.poseId))
            {
                issues.Add($"'{name}': pose entry {i} has no poseId.");
                continue;
            }

            if (entry.clip == null || !entry.clip.IsValid)
                issues.Add($"'{name}': pose '{entry.poseId}' has no clip.");

            if (!seen.Add(entry.poseId))
                issues.Add($"'{name}': pose id '{entry.poseId}' is declared more than once.");
        }
    }
}
