using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class SkillHitboxGroup : MonoBehaviour
{
    [SerializeField] private string groupKey = "Group01";
    [SerializeField] private Collider[] colliders = Array.Empty<Collider>();

    public string GroupKey
    {
        get
        {
            if (string.IsNullOrWhiteSpace(groupKey))
                return name;

            return groupKey.Trim();
        }
    }

    public IReadOnlyList<Collider> Colliders => colliders ?? Array.Empty<Collider>();

    void Reset()
    {
        CacheColliders();
    }

    void OnValidate()
    {
        if (colliders == null || colliders.Length == 0)
            CacheColliders();
    }

    public void Initialize()
    {
        if (colliders == null || colliders.Length == 0)
            CacheColliders();

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider hitbox = colliders[i];
            if (hitbox == null)
                continue;

            hitbox.isTrigger = true;
            hitbox.enabled = false;
        }
    }

    public void SetActive(bool active)
    {
        if (colliders == null)
            return;

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider hitbox = colliders[i];
            if (hitbox == null)
                continue;

            hitbox.enabled = active;
        }
    }

    public void CollectOwnedColliderIds(HashSet<int> target)
    {
        if (target == null || colliders == null)
            return;

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider hitbox = colliders[i];
            if (hitbox == null)
                continue;

            target.Add(hitbox.GetInstanceID());
        }
    }

    public void SampleContacts(
        Collider[] overlapBuffer,
        HashSet<int> seenColliderIds,
        LayerMask targetMask,
        QueryTriggerInteraction queryTriggers,
        Action<Collider> onContact)
    {
        if (colliders == null || overlapBuffer == null || overlapBuffer.Length == 0 || onContact == null)
            return;

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider hitbox = colliders[i];
            if (hitbox == null || !hitbox.enabled)
                continue;

            int count = GetOverlapCount(hitbox, overlapBuffer, targetMask, queryTriggers);
            for (int j = 0; j < count; j++)
            {
                Collider other = overlapBuffer[j];
                if (other == null)
                    continue;

                if (seenColliderIds != null && !seenColliderIds.Add(other.GetInstanceID()))
                    continue;

                onContact(other);
            }
        }
    }

    int GetOverlapCount(
        Collider hitbox,
        Collider[] overlapBuffer,
        LayerMask targetMask,
        QueryTriggerInteraction queryTriggers)
    {
        if (hitbox is BoxCollider box)
            return OverlapBox(box, overlapBuffer, targetMask, queryTriggers);

        if (hitbox is SphereCollider sphere)
            return OverlapSphere(sphere, overlapBuffer, targetMask, queryTriggers);

        if (hitbox is CapsuleCollider capsule)
            return OverlapCapsule(capsule, overlapBuffer, targetMask, queryTriggers);

        return Physics.OverlapBoxNonAlloc(
            hitbox.bounds.center,
            hitbox.bounds.extents,
            overlapBuffer,
            hitbox.transform.rotation,
            targetMask,
            queryTriggers);
    }

    int OverlapBox(
        BoxCollider box,
        Collider[] overlapBuffer,
        LayerMask targetMask,
        QueryTriggerInteraction queryTriggers)
    {
        Vector3 scale = Abs(box.transform.lossyScale);
        Vector3 halfExtents = Vector3.Scale(box.size * 0.5f, scale);

        return Physics.OverlapBoxNonAlloc(
            box.transform.TransformPoint(box.center),
            halfExtents,
            overlapBuffer,
            box.transform.rotation,
            targetMask,
            queryTriggers);
    }

    int OverlapSphere(
        SphereCollider sphere,
        Collider[] overlapBuffer,
        LayerMask targetMask,
        QueryTriggerInteraction queryTriggers)
    {
        Vector3 scale = Abs(sphere.transform.lossyScale);
        float radius = sphere.radius * Mathf.Max(scale.x, Mathf.Max(scale.y, scale.z));

        return Physics.OverlapSphereNonAlloc(
            sphere.transform.TransformPoint(sphere.center),
            radius,
            overlapBuffer,
            targetMask,
            queryTriggers);
    }

    int OverlapCapsule(
        CapsuleCollider capsule,
        Collider[] overlapBuffer,
        LayerMask targetMask,
        QueryTriggerInteraction queryTriggers)
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
            overlapBuffer,
            targetMask,
            queryTriggers);
    }

    void CacheColliders()
    {
        colliders = GetComponentsInChildren<Collider>(true);
    }

    static Vector3 Abs(Vector3 value)
    {
        return new Vector3(
            Mathf.Abs(value.x),
            Mathf.Abs(value.y),
            Mathf.Abs(value.z));
    }
}
