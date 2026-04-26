using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-108)]
public sealed class AIAimTargetDriver : MonoBehaviour
{
    const string DefaultAimTargetName = "Aim Target";

    [Header("Refs")]
    [SerializeField] private AITargetSensor sensor;
    [SerializeField] private SkillUserSystem skillUserSystem;
    [SerializeField] private Transform aimTarget;
    [SerializeField] private Transform forwardReference;

    [Header("Runtime Binding")]
    [SerializeField] private bool bindSkillUserAimTransform = true;
    [SerializeField] private string aimTargetName = DefaultAimTargetName;

    [Header("Fallback")]
    [SerializeField] private bool followLastSeenPosition = true;
    [SerializeField, Min(0.5f)] private float fallbackForwardDistance = 6f;

    Transform _overrideTarget;
    Vector3 _overridePoint;
    bool _hasOverridePoint;
    bool _preferChainAttackPoint;

    public Transform AimTarget => aimTarget;
    public bool HasOverride => _overrideTarget != null || _hasOverridePoint;

    void Awake()
    {
        CacheReferences();
        RefreshNow();
    }

    void OnEnable()
    {
        CacheReferences();
        RefreshNow();
    }

    void LateUpdate()
    {
        RefreshNow();
    }

    void OnDisable()
    {
        if (bindSkillUserAimTransform && skillUserSystem != null)
            skillUserSystem.ClearRuntimeAimTransformOverride(aimTarget);
    }

    public void SetOverrideTarget(Transform target, bool preferChainAttackPoint = false)
    {
        _overrideTarget = target;
        _overridePoint = Vector3.zero;
        _hasOverridePoint = false;
        _preferChainAttackPoint = preferChainAttackPoint;
        RefreshNow();
    }

    public void SetOverridePoint(Vector3 worldPoint)
    {
        _overrideTarget = null;
        _overridePoint = worldPoint;
        _hasOverridePoint = true;
        _preferChainAttackPoint = false;
        RefreshNow();
    }

    public void ClearOverride()
    {
        _overrideTarget = null;
        _overridePoint = Vector3.zero;
        _hasOverridePoint = false;
        _preferChainAttackPoint = false;
        RefreshNow();
    }

    public void RefreshNow()
    {
        CacheReferences();
        if (aimTarget == null)
            return;

        if (TryResolveDesiredAimPosition(out Vector3 position))
            aimTarget.position = position;
    }

    void CacheReferences()
    {
        if (sensor == null)
            sensor = GetComponent<AITargetSensor>();

        if (skillUserSystem == null)
            skillUserSystem = GetComponent<SkillUserSystem>();

        if (skillUserSystem == null)
            skillUserSystem = GetComponentInChildren<SkillUserSystem>(true);

        if (forwardReference == null)
            forwardReference = transform;

        if (!IsModuleCompatibleAimTarget(aimTarget))
            aimTarget = ResolveOrCreateAimTarget();

        if (bindSkillUserAimTransform && skillUserSystem != null && aimTarget != null)
            skillUserSystem.SetRuntimeAimTransformOverride(aimTarget);
    }

    Transform ResolveOrCreateAimTarget()
    {
        if (IsModuleCompatibleAimTarget(aimTarget))
            return aimTarget;

        if (skillUserSystem != null &&
            skillUserSystem.AimTransform != null &&
            skillUserSystem.AimTransform != skillUserSystem.transform &&
            skillUserSystem.AimTransform.name == GetAimTargetName())
        {
            return skillUserSystem.AimTransform;
        }

        string desiredName = GetAimTargetName();
        Transform directChild = transform.Find(desiredName);
        if (directChild != null)
            return directChild;

        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            if (IsModuleCompatibleAimTarget(child) && child != transform && child.name == desiredName)
                return child;
        }

        if (!Application.isPlaying)
            return null;

        GameObject aimTargetObject = new GameObject(desiredName);
        Transform created = aimTargetObject.transform;
        created.SetParent(transform, false);
        created.localPosition = Vector3.forward * Mathf.Max(1f, fallbackForwardDistance);
        created.localRotation = Quaternion.identity;
        created.localScale = Vector3.one;
        return created;
    }

    bool IsModuleCompatibleAimTarget(Transform candidate)
    {
        return candidate != null && candidate.name == GetAimTargetName();
    }

    bool TryResolveDesiredAimPosition(out Vector3 position)
    {
        if (_overrideTarget != null &&
            TryResolveTargetPoint(_overrideTarget, _preferChainAttackPoint, out position))
        {
            return true;
        }

        if (_hasOverridePoint)
        {
            position = _overridePoint;
            return true;
        }

        if (sensor != null)
        {
            Transform currentTarget = sensor.CurrentTarget;
            if (currentTarget != null &&
                sensor.HasLineOfSight &&
                TryResolveTargetPoint(currentTarget, preferChainAttackPoint: false, out position))
            {
                return true;
            }

            if (followLastSeenPosition && sensor.HasAnyTarget)
            {
                position = sensor.LastSeenPosition;
                return true;
            }

            if (currentTarget != null &&
                TryResolveTargetPoint(currentTarget, preferChainAttackPoint: false, out position))
            {
                return true;
            }
        }

        Transform reference = forwardReference != null ? forwardReference : transform;
        Vector3 forward = reference.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude <= 0.0001f)
            forward = transform.forward;

        forward.y = 0f;
        if (forward.sqrMagnitude <= 0.0001f)
            forward = Vector3.forward;

        position = reference.position + forward.normalized * Mathf.Max(0.5f, fallbackForwardDistance);
        return true;
    }

    bool TryResolveTargetPoint(Transform target, bool preferChainAttackPoint, out Vector3 point)
    {
        point = Vector3.zero;
        if (target == null)
            return false;

        AITargetInfo targetInfo = target.GetComponentInParent<AITargetInfo>();
        if (targetInfo != null)
        {
            Transform targetPoint = preferChainAttackPoint
                ? targetInfo.ChainAttackPoint
                : targetInfo.AimPoint;
            if (targetPoint != null)
            {
                point = targetPoint.position;
                return true;
            }
        }

        IAITargetable targetable = FindTargetable(target);
        if (targetable != null && targetable.AimPoint != null)
        {
            point = targetable.AimPoint.position;
            return true;
        }

        Transform rootTransform = ResolveTargetRoot(target);
        AITargetInfo childTargetInfo = rootTransform != null
            ? rootTransform.GetComponentInChildren<AITargetInfo>(true)
            : null;
        if (childTargetInfo != null)
        {
            Transform targetPoint = preferChainAttackPoint
                ? childTargetInfo.ChainAttackPoint
                : childTargetInfo.AimPoint;
            if (targetPoint != null)
            {
                point = targetPoint.position;
                return true;
            }
        }

        IAITargetable childTargetable = FindTargetableInChildren(rootTransform);
        if (childTargetable != null && childTargetable.AimPoint != null)
        {
            point = childTargetable.AimPoint.position;
            return true;
        }

        point = target.position;
        return true;
    }

    IAITargetable FindTargetable(Transform start)
    {
        if (start == null)
            return null;

        MonoBehaviour[] all = start.GetComponentsInParent<MonoBehaviour>(true);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] is IAITargetable targetable)
                return targetable;
        }

        return null;
    }

    IAITargetable FindTargetableInChildren(Transform start)
    {
        if (start == null)
            return null;

        MonoBehaviour[] all = start.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] is IAITargetable targetable)
                return targetable;
        }

        return null;
    }

    static Transform ResolveTargetRoot(Transform start)
    {
        if (start == null)
            return null;

        CharacteContext targetContext = start.GetComponentInParent<CharacteContext>();
        if (targetContext != null)
            return targetContext.transform;

        Rigidbody rigidbody = start.GetComponentInParent<Rigidbody>();
        if (rigidbody != null)
            return rigidbody.transform;

        return start.root != null ? start.root : start;
    }

    string GetAimTargetName()
    {
        return string.IsNullOrWhiteSpace(aimTargetName) ? DefaultAimTargetName : aimTargetName;
    }
}
