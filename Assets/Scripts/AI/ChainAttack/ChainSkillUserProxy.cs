using UnityEngine;

[DisallowMultipleComponent]
public sealed class ChainSkillUserProxy : MonoBehaviour, ISkillUser
{
    [SerializeField] private MonoBehaviour baseSkillUserSource;
    [SerializeField] private CharacteContext characteContext;

    ISkillUser _baseSkillUser;
    Transform _aimTargetOverride;
    Vector3 _aimPointOverride;
    bool _hasAimPointOverride;

    public Transform CastOrigin => ResolveBaseSkillUser()?.CastOrigin ?? transform;
    public Transform AimTransform => _aimTargetOverride != null
        ? _aimTargetOverride
        : ResolveBaseSkillUser()?.AimTransform ?? transform;
    public float currentEnagy => ResolveBaseSkillUser()?.currentEnagy ?? 0f;
    public StatsHub StatsHub => ResolveBaseSkillUser()?.StatsHub ?? (characteContext != null ? characteContext.StatsHub : null);

    void Awake()
    {
        if (characteContext == null)
            characteContext = GetComponent<CharacteContext>();

        ResolveBaseSkillUser();
    }

    public void SetAimTargetOverride(Transform target)
    {
        _aimTargetOverride = target;
        _hasAimPointOverride = false;
        _aimPointOverride = Vector3.zero;
    }

    public void SetAimPointOverride(Vector3 worldPoint)
    {
        _aimTargetOverride = null;
        _hasAimPointOverride = true;
        _aimPointOverride = worldPoint;
    }

    public void ClearAimOverrides()
    {
        _aimTargetOverride = null;
        _hasAimPointOverride = false;
        _aimPointOverride = Vector3.zero;
    }

    public void SpendEnagy(float amount)
    {
        ResolveBaseSkillUser()?.SpendEnagy(amount);
    }

    public Vector3 AimDirection
    {
        get
        {
            Transform castOrigin = CastOrigin;
            if (castOrigin != null)
            {
                if (_aimTargetOverride != null)
                {
                    Vector3 directionToTarget = _aimTargetOverride.position - castOrigin.position;
                    directionToTarget.y = 0f;
                    if (directionToTarget.sqrMagnitude > 0.0001f)
                        return directionToTarget.normalized;
                }

                if (_hasAimPointOverride)
                {
                    Vector3 directionToPoint = _aimPointOverride - castOrigin.position;
                    directionToPoint.y = 0f;
                    if (directionToPoint.sqrMagnitude > 0.0001f)
                        return directionToPoint.normalized;
                }
            }

            Vector3 fallback = ResolveBaseSkillUser()?.AimDirection ?? transform.forward;
            if (fallback.sqrMagnitude <= 0.0001f)
                fallback = transform.forward;

            fallback.y = 0f;
            return fallback.sqrMagnitude > 0.0001f ? fallback.normalized : Vector3.forward;
        }
    }

    ISkillUser ResolveBaseSkillUser()
    {
        if (_baseSkillUser != null)
            return _baseSkillUser;

        if (baseSkillUserSource is ISkillUser typedSource && baseSkillUserSource != this)
        {
            _baseSkillUser = typedSource;
            return _baseSkillUser;
        }

        if (characteContext == null)
            characteContext = GetComponent<CharacteContext>();

        if (characteContext != null && characteContext.EnegySystem != null)
        {
            _baseSkillUser = characteContext.EnegySystem;
            return _baseSkillUser;
        }

        MonoBehaviour[] behaviours = GetComponents<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour == null || behaviour == this)
                continue;

            if (behaviour is ISkillUser skillUser)
            {
                _baseSkillUser = skillUser;
                return _baseSkillUser;
            }
        }

        return null;
    }
}
