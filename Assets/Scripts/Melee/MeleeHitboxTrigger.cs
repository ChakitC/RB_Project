using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

// Import-only schema for the one-time editor migration. No contact or damage runtime remains.
[Obsolete("Migrate this component with Tools/RB/Melee/Migrate Character Hitboxes To Skill Layouts.")]
[AddComponentMenu("")]
public sealed class MeleeHitboxTrigger : MonoBehaviour
{
    [Serializable]
    private sealed class HitboxGroup
    {
        [SerializeField] private List<Collider> colliders = new();
    }
    [SerializeField] private HitboxGroup lightHitboxes = new();
    [SerializeField] private HitboxGroup heavyHitboxes = new();
    [FormerlySerializedAs("hitboxR"), SerializeField, HideInInspector] private Collider legacyHitboxR;
    [FormerlySerializedAs("hitboxL"), SerializeField, HideInInspector] private Collider legacyHitboxL;
    [SerializeField] private LayerMask targetMask = ~0;
}
