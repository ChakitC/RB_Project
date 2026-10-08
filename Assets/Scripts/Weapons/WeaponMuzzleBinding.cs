using UnityEngine;

[DisallowMultipleComponent]
public sealed class WeaponMuzzleBinding : MonoBehaviour
{
    [SerializeField, Tooltip("Barrel tip on this model. Local +Z points out of the barrel.")]
    private Transform firePoint;
    public Transform FirePoint => firePoint;
    public bool IsValid => firePoint != null &&
                           (firePoint == transform || firePoint.IsChildOf(transform));

    public void Configure(Transform value)
    {
        firePoint = value;
    }

    public bool TryBind(WeaponSystem weapon)
    {
        if (weapon == null || !IsValid)
            return false;
        return weapon.BindFirePoint(firePoint);
    }

    void OnDrawGizmosSelected()
    {
        if (!IsValid) return;
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(firePoint.position, 0.015f);
        Gizmos.DrawRay(firePoint.position, firePoint.forward * 0.2f);
    }
}
