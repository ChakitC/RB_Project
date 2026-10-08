using UnityEngine;

public abstract class WeaponAimSource : MonoBehaviour
{
    [SerializeField] protected LayerMask aimCollisionMask = ~0;
    [SerializeField, Min(0f)] protected float muzzleProbeRadius = 0.045f;
    readonly WeaponAimQuery aimQuery = new();

    public abstract CharacteContext OwnerContext { get; }
    public abstract bool TryGetAimSample(out WeaponAimSample sample);
    public abstract float MovementSpreadFactor { get; }
    public bool IsMuzzleBlocked { get; private set; }
    public Vector3 MuzzleHitPoint { get; private set; }

    public void ProbeMuzzle(Vector3 origin, in WeaponAimSample sample)
    {
        MuzzleHitPoint = WeaponAimResolver.ResolveAimPoint(origin, sample);
        Vector3 delta = MuzzleHitPoint - origin;
        RaycastHit hit = default;
        IsMuzzleBlocked = delta.sqrMagnitude > 0.000001f &&
            QueryHit(new Ray(origin, delta.normalized), delta.magnitude,
                muzzleProbeRadius, false, out hit);
        if (IsMuzzleBlocked)
            MuzzleHitPoint = hit.point;
    }

    protected bool QueryHit(Ray ray, float distance, float radius,
        bool includeHurtboxTriggers, out RaycastHit hit) =>
        aimQuery.TryGetFirstValidHit(OwnerContext, aimCollisionMask, ray, distance,
            radius, includeHurtboxTriggers, out hit);
}
