using System;
using UnityEngine;

[DisallowMultipleComponent]
public class AITargetSensor : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Transform sensorOrigin;
    [SerializeField] private Transform forwardReference;

    [Header("Scan")]
    [SerializeField] private float radius = 15f;
    [SerializeField] private float scanInterval = 0.1f;
    [SerializeField] private LayerMask targetLayers = ~0;
    [SerializeField] private LayerMask obstacleLayers = 0;
    [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;
    [SerializeField] private int maxHits = 64;

    [Header("Filter")]
    [SerializeField] private string requiredTag = "";
    [SerializeField] private bool useFieldOfView = false;
    [SerializeField, Range(0f, 360f)] private float fieldOfView = 180f;
    [SerializeField] private bool requireLineOfSight = true;

    [Header("Team / Alive")]
    [SerializeField] private bool useTeamFilter = false;
    [SerializeField] private int ownerTeamId = 0;
    [SerializeField] private bool requireAliveIfAvailable = true;

    [Header("Memory")]
    [SerializeField] private float gracePeriod = 1.25f;
    [SerializeField] private bool keepLastSeenTarget = true;

    [Header("Offsets")]
    [SerializeField] private float originHeightOffset = 0.5f;
    [SerializeField] private float fallbackTargetHeightOffset = 0.6f;

    [Header("Debug Runtime")]
    [SerializeField] private Transform currentTarget;
    [SerializeField] private Transform lastSeenTarget;
    [SerializeField] private Vector3 lastSeenPosition;
    [SerializeField] private bool hasLineOfSight;
    [SerializeField] private float targetDistance;
    [SerializeField] private float lastSeenTime = float.NegativeInfinity;

    private Collider[] overlapBuffer;
    private float nextScanTime;

    public Transform CurrentTarget => IsTrackedTargetStillValid(currentTarget) ? currentTarget : null;
    public Transform LastSeenTarget => IsTrackedTargetStillValid(lastSeenTarget) ? lastSeenTarget : null;
    public Vector3 LastSeenPosition => lastSeenPosition;
    public bool HasLineOfSight => hasLineOfSight;
    public float GracePeriod => gracePeriod;
    public float TargetDistance => targetDistance;
    public float LastSeenTime => lastSeenTime;
    public float TimeSinceLastSeen => Time.time - lastSeenTime;

    public bool HasLiveTarget => CurrentTarget != null;
    public bool HasAnyTarget => HasLiveTarget || IsWithinGracePeriod();

    public event Action<Transform, Transform> OnTargetChanged;

    private void Awake()
    {
        if (sensorOrigin == null) sensorOrigin = transform;
        if (forwardReference == null) forwardReference = transform;

        maxHits = Mathf.Max(1, maxHits);
        overlapBuffer = new Collider[maxHits];
    }

    private void Update()
    {
        if (RefreshTrackedTargetValidity())
            nextScanTime = Time.time;

        if (Time.time < nextScanTime) return;
        nextScanTime = Time.time + Mathf.Max(0.01f, scanInterval);

        Scan();
    }

    public void ForceScan()
    {
        RefreshTrackedTargetValidity();
        Scan();
        nextScanTime = Time.time + Mathf.Max(0.01f, scanInterval);
    }

    public void ClearTargetMemory()
    {
        SetCurrentTarget(null);
        if (!keepLastSeenTarget)
            lastSeenTarget = null;

        lastSeenPosition = Vector3.zero;
        hasLineOfSight = false;
        targetDistance = 0f;
        lastSeenTime = float.NegativeInfinity;
    }

    private void Scan()
    {
        Vector3 origin = GetOriginPosition();

        int count = Physics.OverlapSphereNonAlloc(
            origin,
            radius,
            overlapBuffer,
            targetLayers,
            triggerInteraction
        );

        Transform bestTarget = null;
        Vector3 bestPoint = Vector3.zero;
        float bestDistSqr = float.PositiveInfinity;
        bool bestLOS = false;

        for (int i = 0; i < count; i++)
        {
            Collider hit = overlapBuffer[i];
            if (hit == null) continue;

            if (!TryResolveTarget(hit, out Transform targetRoot, out IAITargetable targetable))
                continue;

            if (!IsValidTarget(hit, targetRoot, targetable))
                continue;

            Vector3 targetPoint = GetTargetPoint(hit, targetRoot, targetable);
            Vector3 toTarget = targetPoint - origin;
            float distSqr = toTarget.sqrMagnitude;

            if (useFieldOfView && fieldOfView < 360f && !IsInsideFOVXZ(toTarget))
                continue;

            bool los = !requireLineOfSight || CheckLineOfSight(origin, targetPoint);
            if (requireLineOfSight && !los)
                continue;

            if (distSqr < bestDistSqr)
            {
                bestDistSqr = distSqr;
                bestTarget = targetRoot;
                bestPoint = targetPoint;
                bestLOS = los;
            }
        }

        if (bestTarget != null)
        {
            SetCurrentTarget(bestTarget);

            lastSeenTarget = bestTarget;
            lastSeenPosition = bestPoint;
            hasLineOfSight = bestLOS;
            targetDistance = Mathf.Sqrt(bestDistSqr);
            lastSeenTime = Time.time;
            return;
        }

        // หาไม่เจอเป้าสด
        SetCurrentTarget(null);
        hasLineOfSight = false;

        if (IsWithinGracePeriod())
        {
            targetDistance = Vector3.Distance(origin, lastSeenPosition);
        }
        else
        {
            if (!keepLastSeenTarget)
                lastSeenTarget = null;

            targetDistance = 0f;
        }
    }

    private void SetCurrentTarget(Transform newTarget)
    {
        if (currentTarget == newTarget) return;

        Transform old = currentTarget;
        currentTarget = newTarget;
        OnTargetChanged?.Invoke(old, newTarget);
    }

    private bool TryResolveTarget(Collider hit, out Transform targetRoot, out IAITargetable targetable)
    {
        targetRoot = null;
        targetable = null;

        if (hit == null) return false;

        targetable = FindTargetable(hit.transform);
        if (targetable != null)
        {
            if (targetable is Component c)
            {
                targetRoot = c.transform;
                return true;
            }
        }

        if (hit.attachedRigidbody != null)
        {
            targetRoot = hit.attachedRigidbody.transform;
            return true;
        }

        targetRoot = hit.transform.root != null ? hit.transform.root : hit.transform;
        return targetRoot != null;
    }

    private bool IsValidTarget(Collider hit, Transform targetRoot, IAITargetable targetable)
    {
        if (targetRoot == null) return false;

        // กัน self
        if (targetRoot == transform || targetRoot.root == transform.root)
            return false;

        if (!string.IsNullOrEmpty(requiredTag))
        {
            bool rootMatch = targetRoot.CompareTag(requiredTag);
            bool hitMatch = hit.CompareTag(requiredTag);
            if (!rootMatch && !hitMatch)
                return false;
        }

        if (targetable != null)
        {
            if (!IsTargetAllowedByTargetable(targetable))
                return false;
        }

        if (!IsTargetAllowedByLifeState(targetRoot))
            return false;

        return true;
    }

    private bool RefreshTrackedTargetValidity()
    {
        bool invalidated = false;

        if (currentTarget != null && !IsTrackedTargetStillValid(currentTarget))
        {
            SetCurrentTarget(null);
            hasLineOfSight = false;
            targetDistance = 0f;
            invalidated = true;
        }

        if (lastSeenTarget != null && !IsTrackedTargetStillValid(lastSeenTarget))
        {
            lastSeenTarget = null;
            lastSeenPosition = Vector3.zero;
            lastSeenTime = float.NegativeInfinity;
            hasLineOfSight = false;
            targetDistance = 0f;
            invalidated = true;
        }

        return invalidated;
    }

    private bool IsTrackedTargetStillValid(Transform target)
    {
        if (!TryResolveTrackedTarget(target, out Transform targetRoot, out IAITargetable targetable))
            return false;

        if (targetRoot == transform || targetRoot.root == transform.root)
            return false;

        if (targetable != null && !IsTargetAllowedByTargetable(targetable))
            return false;

        return IsTargetAllowedByLifeState(targetRoot);
    }

    private bool TryResolveTrackedTarget(Transform target, out Transform targetRoot, out IAITargetable targetable)
    {
        targetRoot = null;
        targetable = null;

        if (target == null)
            return false;

        targetable = FindTargetable(target);
        if (targetable is Component component)
        {
            targetRoot = component.transform;
            return true;
        }

        Rigidbody targetRigidbody = target.GetComponentInParent<Rigidbody>();
        if (targetRigidbody != null)
        {
            targetRoot = targetRigidbody.transform;
            return true;
        }

        targetRoot = target.root != null ? target.root : target;
        return targetRoot != null;
    }

    private bool IsTargetAllowedByTargetable(IAITargetable targetable)
    {
        if (targetable == null)
            return true;

        if (requireAliveIfAvailable && !targetable.IsAlive)
            return false;

        if (!targetable.IsTargetable)
            return false;

        if (useTeamFilter && targetable.TeamId == ownerTeamId)
            return false;

        return true;
    }

    private bool IsTargetAllowedByLifeState(Transform targetRoot)
    {
        if (targetRoot == null)
            return false;

        CharacteContext targetContext = targetRoot.GetComponentInParent<CharacteContext>();
        if (targetContext == null || targetContext.stateHub == null)
            return true;

        return targetContext.stateHub.IsAlive && !targetContext.stateHub.Isdown;
    }

    private IAITargetable FindTargetable(Transform start)
    {
        if (start == null) return null;

        MonoBehaviour[] all = start.GetComponentsInParent<MonoBehaviour>(true);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] is IAITargetable t)
                return t;
        }

        return null;
    }

    private Vector3 GetTargetPoint(Collider hit, Transform targetRoot, IAITargetable targetable)
    {
        if (targetable != null && targetable.AimPoint != null)
            return targetable.AimPoint.position;

        if (hit != null)
            return hit.bounds.center;

        return targetRoot.position + Vector3.up * fallbackTargetHeightOffset;
    }

    private Vector3 GetOriginPosition()
    {
        Transform origin = sensorOrigin != null ? sensorOrigin : transform;
        return origin.position + Vector3.up * originHeightOffset;
    }

    private bool IsInsideFOVXZ(Vector3 toTarget)
    {
        Vector3 forward = forwardReference != null ? forwardReference.forward : transform.forward;

        Vector3 flatForward = Vector3.ProjectOnPlane(forward, Vector3.up);
        Vector3 flatToTarget = Vector3.ProjectOnPlane(toTarget, Vector3.up);

        if (flatForward.sqrMagnitude < 0.0001f || flatToTarget.sqrMagnitude < 0.0001f)
            return true;

        return Vector3.Angle(flatForward, flatToTarget) <= fieldOfView * 0.5f;
    }

    private bool CheckLineOfSight(Vector3 origin, Vector3 targetPoint)
    {
        Vector3 dir = targetPoint - origin;
        float dist = dir.magnitude;
        if (dist <= 0.001f) return true;

        return !Physics.Raycast(
            origin,
            dir / dist,
            dist,
            obstacleLayers,
            triggerInteraction
        );
    }

    private bool IsWithinGracePeriod()
    {
        if (lastSeenTarget != null && !IsTrackedTargetStillValid(lastSeenTarget))
            return false;

        return Time.time - lastSeenTime <= gracePeriod;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Vector3 origin = (sensorOrigin != null ? sensorOrigin.position : transform.position) + Vector3.up * originHeightOffset;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(origin, radius);

        Transform trackedTarget = CurrentTarget;
        if (trackedTarget != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(origin, trackedTarget.position);
        }
        else if (IsWithinGracePeriod())
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(lastSeenPosition, 0.15f);
            Gizmos.DrawLine(origin, lastSeenPosition);
        }
    }
#endif
}
