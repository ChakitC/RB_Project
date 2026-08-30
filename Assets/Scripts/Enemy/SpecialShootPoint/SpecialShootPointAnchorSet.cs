using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The authored anchor list for one enemy <em>model</em>.
///
/// Anchors are bone Transforms, and enemy models are rebuilt from their visual prefab at runtime —
/// the same reason <see cref="CharacterColliderRefs"/> lives on the model and is rebound by
/// <c>CharacterVisualController</c> rather than serialized on the context root. A bone reference
/// serialized on the context root would point at the destroyed model instance after the first
/// rebuild, so the anchor list has to live where the bones do.
///
/// Place this next to <see cref="CharacterColliderRefs"/> on the visual prefab.
/// <see cref="SpecialShootPointController"/> resolves it live and re-resolves it whenever the model
/// is swapped.
/// </summary>
[DisallowMultipleComponent]
public sealed class SpecialShootPointAnchorSet : MonoBehaviour
{
    [Tooltip("Candidate bone anchors on this model. The profile owns balancing; this owns geometry.")]
    [SerializeField] private List<SpecialShootPointAnchor> anchors = new();

    public IReadOnlyList<SpecialShootPointAnchor> Anchors => anchors;

    public int Count => anchors != null ? anchors.Count : 0;

    public SpecialShootPointAnchor GetAnchor(int index)
    {
        if (anchors == null || index < 0 || index >= anchors.Count)
            return null;

        return anchors[index];
    }

    public bool IsUsable(int index)
    {
        SpecialShootPointAnchor anchor = GetAnchor(index);
        return anchor != null && anchor.IsUsable;
    }
}
