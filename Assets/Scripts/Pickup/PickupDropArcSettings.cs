using System;
using UnityEngine;
using Random = UnityEngine.Random;

[Serializable]
public class PickupDropArcSettings
{
    const int DefaultTerrainLayer = 17;
    const int DefaultTerrainClearanceMask = 1 << DefaultTerrainLayer;

    [SerializeField] private bool enabled = true;
    [SerializeField, Min(0f)] private float startHeight = 0.45f;
    [SerializeField, Min(0f)] private float minRadius = 1.1f;
    [SerializeField, Min(0f)] private float maxRadius = 2.4f;
    [SerializeField, Min(0.01f)] private float duration = 0.55f;
    [SerializeField, Min(0f)] private float arcHeight = 1.5f;
    [SerializeField, Min(0f)] private float staggerDelay = 0.035f;
    [SerializeField, Range(0f, 180f)] private float angleJitterDegrees = 18f;
    [SerializeField, Min(1)] private int landingAttempts = 8;
    [SerializeField, Min(0f)] private float landingHeightOffset = 0.06f;
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField, Min(0f)] private float groundProbeHeight = 3f;
    [SerializeField, Min(0f)] private float groundProbeDistance = 8f;
    [SerializeField] private LayerMask terrainClearanceMask = DefaultTerrainClearanceMask;
    [SerializeField, Min(0f)] private float terrainClearanceRadius = 0.28f;

    public bool Enabled => enabled;
    public float Duration => Mathf.Max(0.01f, duration);
    public float ArcHeight => Mathf.Max(0f, arcHeight);

    public float CreateBurstAngleOffset()
    {
        return Random.Range(0f, Mathf.PI * 2f);
    }

    public Vector3 ResolveStartPosition(Vector3 origin)
    {
        return origin + Vector3.up * Mathf.Max(0f, startHeight);
    }

    public Vector3 ResolveLandingPosition(Vector3 origin, int index, int count, float burstAngleOffset)
    {
        int safeCount = Mathf.Max(1, count);
        int safeIndex = Mathf.Max(0, index);
        int attempts = Mathf.Max(1, landingAttempts);
        Vector3 fallbackPosition = origin;

        for (int attempt = 0; attempt < attempts; attempt++)
        {
            Vector3 landingPosition = CreateLandingCandidate(origin, safeIndex, safeCount, burstAngleOffset, attempt);
            Vector3 groundedPosition = ResolveGroundedPosition(landingPosition, out bool hitTerrainSurface);

            if (hitTerrainSurface || !HasTerrainClearance(groundedPosition))
                continue;

            if (attempt == 0)
                fallbackPosition = groundedPosition;

            return groundedPosition;
        }

        Vector3 originFallback = ResolveGroundedPosition(origin, out bool originHitTerrainSurface);
        if (!originHitTerrainSurface && HasTerrainClearance(originFallback))
            return originFallback;

        return fallbackPosition;
    }

    public float ResolveDelay(int index)
    {
        return Mathf.Max(0, index) * Mathf.Max(0f, staggerDelay);
    }

    Vector3 CreateLandingCandidate(Vector3 origin, int index, int count, float burstAngleOffset, int attempt)
    {
        float min = Mathf.Min(minRadius, maxRadius);
        float max = Mathf.Max(minRadius, maxRadius);
        float radius = Random.Range(min, max);
        float angleStep = count > 1 ? Mathf.PI * 2f / count : 0f;
        float jitter = Mathf.Min(angleJitterDegrees * Mathf.Deg2Rad, count > 1 ? angleStep * 0.45f : Mathf.PI);
        float retryOffset = attempt <= 0 ? 0f : attempt * 2.399963f;
        float angle = burstAngleOffset + angleStep * index + retryOffset + Random.Range(-jitter, jitter);

        Vector3 horizontalOffset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
        Vector3 landingPosition = origin + horizontalOffset;
        landingPosition.y = origin.y + Mathf.Max(0f, landingHeightOffset);

        return landingPosition;
    }

    Vector3 ResolveGroundedPosition(Vector3 landingPosition, out bool hitTerrainSurface)
    {
        hitTerrainSurface = false;

        int groundProbeMask = groundMask.value & ~terrainClearanceMask.value;
        if (groundProbeMask == 0 || groundProbeHeight <= 0f || groundProbeDistance <= 0f)
            return landingPosition;

        Vector3 rayOrigin = landingPosition + Vector3.up * groundProbeHeight;
        float rayDistance = groundProbeHeight + groundProbeDistance;
        if (!Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, rayDistance, groundProbeMask, QueryTriggerInteraction.Ignore))
            return landingPosition;

        hitTerrainSurface = IsInMask(hit.collider.gameObject.layer, terrainClearanceMask);
        return hit.point + Vector3.up * Mathf.Max(0f, landingHeightOffset);
    }

    bool HasTerrainClearance(Vector3 position)
    {
        if (terrainClearanceMask.value == 0 || terrainClearanceRadius <= 0f)
            return true;

        return !Physics.CheckSphere(position, Mathf.Max(0f, terrainClearanceRadius), terrainClearanceMask, QueryTriggerInteraction.Ignore);
    }

    static bool IsInMask(int layer, LayerMask mask)
    {
        return (mask.value & (1 << layer)) != 0;
    }
}
