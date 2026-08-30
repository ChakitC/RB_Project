using System;
using UnityEngine;

/// <summary>
/// One authored candidate location for a Special Shoot Point, declared on the enemy prefab.
///
/// The prefab owns anchors; <see cref="SpecialShootPointProfileSO"/> owns shared balancing and
/// presentation. An anchor is deliberately data only: no live collider, no VFX instance, and no
/// component is placed under the bone at author time. A pooled runtime point is reparented here
/// when the anchor is selected for a round.
/// </summary>
[Serializable]
public class SpecialShootPointAnchor
{
    [Tooltip("Bone or transform the point rides. Required.")]
    public Transform anchor;

    [Tooltip("Uncheck to keep the anchor authored but out of the shuffle bag.")]
    public bool enabled = true;

    [Tooltip("Local offset from the anchor, applied to the pooled point.")]
    public Vector3 localPosition = Vector3.zero;

    [Tooltip("Local rotation from the anchor, applied to the pooled point.")]
    public Vector3 localEulerAngles = Vector3.zero;

    [Tooltip("World radius of this point's hit collider.")]
    [Min(0.01f)] public float colliderRadius = 0.25f;

    [Tooltip("Uniform scale applied to the point's presentation root.")]
    [Min(0.01f)] public float vfxScale = 1f;

    [Tooltip("Hit zone credited for a shot that lands on this point. Head applies the normal Headshot multiplier.")]
    public CharacterHitZone hitZone = CharacterHitZone.Torso;

    /// <summary>An anchor is usable when it is enabled and still points at a live transform.</summary>
    public bool IsUsable => enabled && anchor != null;

    public Quaternion LocalRotation => Quaternion.Euler(localEulerAngles);
}
