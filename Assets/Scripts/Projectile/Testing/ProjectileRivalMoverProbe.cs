#if UNITY_EDITOR
using UnityEngine;

/// <summary>
/// Test-only stand-in for a vendor mover such as <c>HS_ProjectileMover</c>: it holds the root's
/// Rigidbody and drives its own physics step, which is exactly the shape
/// <c>ProjectileRootOwnerRule</c> exists to catch.
///
/// Runtime assembly, Editor-only compilation - see ProjectileSpawnProbe for why.
/// </summary>
public sealed class ProjectileRivalMoverProbe : MonoBehaviour
{
    [SerializeField] Rigidbody body;
    [SerializeField] float speed = 15f;

    public void Bind(Rigidbody rootBody) => body = rootBody;

    void FixedUpdate()
    {
        if (body == null)
            return;

#if UNITY_6000_0_OR_NEWER
        body.linearVelocity = transform.forward * speed;
#else
        body.velocity = transform.forward * speed;
#endif
    }
}
#endif
