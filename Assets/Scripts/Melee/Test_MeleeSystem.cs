using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class MeleeHitboxTrigger : MonoBehaviour
{
    [Header("Hitboxes")]
    [SerializeField] private Collider hitboxR;
    [SerializeField] private Collider hitboxL;

    [Header("Filter")]
    [SerializeField] private LayerMask targetMask = ~0;

    public event Action<Collider> ContactDetected;

    readonly Collider[] _overlapBuffer = new Collider[32];
    readonly HashSet<int> _sweepColliderIds = new();

    private void Awake()
    {
        if (!hitboxR) Debug.LogError("hitboxR not found", this);
        if (!hitboxL) Debug.LogWarning("hitboxL not assigned/found (will only use hitboxR)", this);

        if (hitboxR) hitboxR.isTrigger = true;
        if (hitboxL) hitboxL.isTrigger = true;

        SetHitboxes(false);
    }

    private void OnDisable()
    {
        SetHitboxes(false);
    }

    public void Activate()
    {
        SetHitboxes(true);
        NotifyExistingContacts();
    }

    public void Deactivate()
    {
        SetHitboxes(false);
    }

    public bool IsTargetAllowed(Collider other)
    {
        if (!other)
            return false;

        return ((1 << other.gameObject.layer) & targetMask.value) != 0;
    }

    private void OnTriggerEnter(Collider other) => NotifyContact(other);
    private void OnTriggerStay(Collider other)  => NotifyContact(other);

    private void NotifyContact(Collider other)
    {
        if (!other || !isActiveAndEnabled || !AreHitboxesEnabled())
            return;
        if (!IsTargetAllowed(other))
            return;

        ContactDetected?.Invoke(other);
    }

    private void SetHitboxes(bool on)
    {
        if (hitboxR) hitboxR.enabled = on;
        if (hitboxL) hitboxL.enabled = on;
    }

    private void NotifyExistingContacts()
    {
        _sweepColliderIds.Clear();

        SampleExistingContacts(hitboxR);

        if (hitboxL && hitboxL != hitboxR)
            SampleExistingContacts(hitboxL);
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
        return (hitboxR && hitboxR.enabled) || (hitboxL && hitboxL.enabled);
    }
}
