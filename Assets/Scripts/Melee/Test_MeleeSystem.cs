using System;
using UnityEngine;

public sealed class MeleeHitboxTrigger : MonoBehaviour
{
    [Header("Hitboxes")]
    [SerializeField] private Collider hitboxR;
    [SerializeField] private Collider hitboxL;

    [Header("Filter")]
    [SerializeField] private LayerMask targetMask = ~0;

    public event Action<Collider> ContactDetected;

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

    private bool AreHitboxesEnabled()
    {
        return (hitboxR && hitboxR.enabled) || (hitboxL && hitboxL.enabled);
    }
}
