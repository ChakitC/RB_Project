using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public sealed class MeleeHitboxTrigger : MonoBehaviour
{
    [Serializable]
    private sealed class HitboxGroup
    {
        [SerializeField] private List<Collider> colliders = new();

        public IReadOnlyList<Collider> Colliders => colliders;

        public void AddIfMissing(Collider collider)
        {
            if (!collider)
                return;

            if (colliders == null)
                colliders = new List<Collider>();

            if (!colliders.Contains(collider))
                colliders.Add(collider);
        }

        public bool Contains(Collider collider)
        {
            if (!collider || colliders == null)
                return false;

            for (int i = 0; i < colliders.Count; i++)
            {
                if (colliders[i] == collider)
                    return true;
            }

            return false;
        }

        public void SetEnabled(bool enabled)
        {
            if (colliders == null)
                return;

            for (int i = 0; i < colliders.Count; i++)
            {
                Collider collider = colliders[i];
                if (!collider)
                    continue;

                collider.isTrigger = true;
                collider.enabled = enabled;
            }
        }

        public bool HasEnabledCollider()
        {
            if (colliders == null)
                return false;

            for (int i = 0; i < colliders.Count; i++)
            {
                Collider collider = colliders[i];
                if (collider && collider.enabled)
                    return true;
            }

            return false;
        }

        public bool IsEmpty()
        {
            if (colliders == null)
                return true;

            for (int i = 0; i < colliders.Count; i++)
            {
                if (colliders[i])
                    return false;
            }

            return true;
        }
    }

    [Header("Hitboxes")]
    [SerializeField] private HitboxGroup lightHitboxes = new();
    [SerializeField] private HitboxGroup heavyHitboxes = new();
    [FormerlySerializedAs("hitboxR")]
    [SerializeField, HideInInspector] private Collider legacyHitboxR;
    [FormerlySerializedAs("hitboxL")]
    [SerializeField, HideInInspector] private Collider legacyHitboxL;

    [Header("Filter")]
    [SerializeField] private LayerMask targetMask = ~0;

    public event Action<Collider> ContactDetected;

    readonly Collider[] _overlapBuffer = new Collider[32];
    readonly HashSet<int> _sweepColliderIds = new();
    private CharacterAnimBrain.MeleeType _activeMeleeType = CharacterAnimBrain.MeleeType.Light;
    private bool _samplingActive;

    private void Awake()
    {
        EnsureHitboxGroups();
        MigrateLegacyHitboxesIfNeeded();
        LogMissingAssignments();
        SetAllHitboxes(false);
    }

    private void OnValidate()
    {
        EnsureHitboxGroups();
        MigrateLegacyHitboxesIfNeeded();
    }

    private void OnDisable()
    {
        _samplingActive = false;
        SetAllHitboxes(false);
    }

    public void Activate(CharacterAnimBrain.MeleeType meleeType)
    {
        _activeMeleeType = meleeType;
        _sweepColliderIds.Clear();
        SetHitboxesForActiveType(true);
        _samplingActive = true;
        SampleActiveContacts();
    }

    public void Activate()
    {
        Activate(CharacterAnimBrain.MeleeType.Light);
    }

    public void Deactivate()
    {
        _samplingActive = false;
        SetAllHitboxes(false);
        _sweepColliderIds.Clear();
    }

    public bool IsTargetAllowed(Collider other)
    {
        if (!other)
            return false;

        return ((1 << other.gameObject.layer) & targetMask.value) != 0;
    }

    public bool IsHitboxCollider(Collider other)
    {
        EnsureHitboxGroups();
        return other && (lightHitboxes.Contains(other) || heavyHitboxes.Contains(other));
    }

    private void OnTriggerEnter(Collider other) => NotifyContact(other);
    private void OnTriggerStay(Collider other)  => NotifyContact(other);

    private void LateUpdate()
    {
        if (!_samplingActive)
            return;

        SampleActiveContacts();
    }

    private void NotifyContact(Collider other)
    {
        if (!other || !isActiveAndEnabled || !AreHitboxesEnabled())
            return;
        if (!IsTargetAllowed(other))
            return;

        ContactDetected?.Invoke(other);
    }

    private void SetAllHitboxes(bool on)
    {
        EnsureHitboxGroups();
        lightHitboxes.SetEnabled(on);
        heavyHitboxes.SetEnabled(on);
    }

    private void SetHitboxesForActiveType(bool on)
    {
        SetAllHitboxes(false);

        if (on)
            GetActiveHitboxGroup().SetEnabled(true);
    }

    private void SampleActiveContacts()
    {
        SampleExistingContacts(GetActiveHitboxGroup());
    }

    private void SampleExistingContacts(HitboxGroup hitboxGroup)
    {
        IReadOnlyList<Collider> hitboxes = hitboxGroup.Colliders;
        if (hitboxes == null)
            return;

        for (int i = 0; i < hitboxes.Count; i++)
        {
            Collider hitbox = hitboxes[i];
            if (!hitbox || !hitbox.enabled)
                continue;

            SampleExistingContacts(hitbox);
        }
    }

    private void SampleExistingContacts(Collider hitbox)
    {
        if (!hitbox || !hitbox.enabled)
            return;

        int hitCount = GetOverlapCount(hitbox);
        for (int i = 0; i < hitCount; i++)
        {
            Collider other = _overlapBuffer[i];
            if (!other)
                continue;

            int colliderId = other.GetInstanceID();
            if (!_sweepColliderIds.Add(colliderId))
                continue;

            NotifyContact(other);
        }
    }

    private int GetOverlapCount(Collider hitbox)
    {
        if (hitbox is BoxCollider box)
            return OverlapBox(box);

        if (hitbox is SphereCollider sphere)
            return OverlapSphere(sphere);

        if (hitbox is CapsuleCollider capsule)
            return OverlapCapsule(capsule);

        return Physics.OverlapBoxNonAlloc(
            hitbox.bounds.center,
            hitbox.bounds.extents,
            _overlapBuffer,
            hitbox.transform.rotation,
            targetMask,
            QueryTriggerInteraction.Collide);
    }

    private int OverlapBox(BoxCollider box)
    {
        Vector3 scale = Abs(box.transform.lossyScale);
        Vector3 halfExtents = Vector3.Scale(box.size * 0.5f, scale);

        return Physics.OverlapBoxNonAlloc(
            box.transform.TransformPoint(box.center),
            halfExtents,
            _overlapBuffer,
            box.transform.rotation,
            targetMask,
            QueryTriggerInteraction.Collide);
    }

    private int OverlapSphere(SphereCollider sphere)
    {
        Vector3 scale = Abs(sphere.transform.lossyScale);
        float radius = sphere.radius * Mathf.Max(scale.x, Mathf.Max(scale.y, scale.z));

        return Physics.OverlapSphereNonAlloc(
            sphere.transform.TransformPoint(sphere.center),
            radius,
            _overlapBuffer,
            targetMask,
            QueryTriggerInteraction.Collide);
    }

    private int OverlapCapsule(CapsuleCollider capsule)
    {
        Vector3 scale = Abs(capsule.transform.lossyScale);
        float axisScale;
        float radiusScale;
        Vector3 axis;

        switch (capsule.direction)
        {
            case 0:
                axisScale = scale.x;
                radiusScale = Mathf.Max(scale.y, scale.z);
                axis = capsule.transform.right;
                break;
            case 1:
                axisScale = scale.y;
                radiusScale = Mathf.Max(scale.x, scale.z);
                axis = capsule.transform.up;
                break;
            default:
                axisScale = scale.z;
                radiusScale = Mathf.Max(scale.x, scale.y);
                axis = capsule.transform.forward;
                break;
        }

        float radius = Mathf.Max(0.001f, capsule.radius * radiusScale);
        float height = Mathf.Max(capsule.height * axisScale, radius * 2f);
        float halfSegment = Mathf.Max(0f, height * 0.5f - radius);
        Vector3 center = capsule.transform.TransformPoint(capsule.center);
        Vector3 p0 = center + axis * halfSegment;
        Vector3 p1 = center - axis * halfSegment;

        return Physics.OverlapCapsuleNonAlloc(
            p0,
            p1,
            radius,
            _overlapBuffer,
            targetMask,
            QueryTriggerInteraction.Collide);
    }

    private static Vector3 Abs(Vector3 value)
    {
        return new Vector3(
            Mathf.Abs(value.x),
            Mathf.Abs(value.y),
            Mathf.Abs(value.z));
    }

    private bool AreHitboxesEnabled()
    {
        return GetActiveHitboxGroup().HasEnabledCollider();
    }

    private HitboxGroup GetActiveHitboxGroup()
    {
        EnsureHitboxGroups();
        return _activeMeleeType == CharacterAnimBrain.MeleeType.Heavy
            ? heavyHitboxes
            : lightHitboxes;
    }

    private void MigrateLegacyHitboxesIfNeeded()
    {
        EnsureHitboxGroups();

        if (!legacyHitboxR && !legacyHitboxL)
            return;

        if (!lightHitboxes.IsEmpty() || !heavyHitboxes.IsEmpty())
            return;

        lightHitboxes.AddIfMissing(legacyHitboxR);
        lightHitboxes.AddIfMissing(legacyHitboxL);
        heavyHitboxes.AddIfMissing(legacyHitboxR);
        heavyHitboxes.AddIfMissing(legacyHitboxL);

        legacyHitboxR = null;
        legacyHitboxL = null;
    }

    private void LogMissingAssignments()
    {
        EnsureHitboxGroups();

        bool lightMissing = lightHitboxes.IsEmpty();
        bool heavyMissing = heavyHitboxes.IsEmpty();

        if (lightMissing && heavyMissing)
        {
            Debug.LogError("No melee hitboxes assigned for Light or Heavy attacks.", this);
            return;
        }

        if (lightMissing)
            Debug.LogWarning("Light hitboxes are not assigned.", this);

        if (heavyMissing)
            Debug.LogWarning("Heavy hitboxes are not assigned.", this);
    }

    private void EnsureHitboxGroups()
    {
        if (lightHitboxes == null)
            lightHitboxes = new HitboxGroup();

        if (heavyHitboxes == null)
            heavyHitboxes = new HitboxGroup();
    }
}
