using System;
using UnityEngine;

public class StatusEffectPickUp : MonoBehaviour
{
    
    [SerializeField] private StatusEffectDef effect;
    [SerializeField, Min(1)] private int stacks = 1;
    [SerializeField] private PickupVisualMotion pickupVisualMotion;

    bool _collecting;

    void Reset()
    {
        TryGetComponent(out pickupVisualMotion);
    }

    void Awake()
    {
        if (pickupVisualMotion == null)
            TryGetComponent(out pickupVisualMotion);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_collecting)
            return;

        if (!other.CompareTag("Player"))
            return;
        
        var controller = other.GetComponentInParent<StatusEffectController>();
        if (controller == null || effect == null)
            return;

        controller.ApplyEffect(effect, gameObject, stacks);
        CompletePickup(controller.transform);
    }

    void CompletePickup(Transform collector)
    {
        _collecting = true;
        DisablePickupColliders();

        if (pickupVisualMotion == null)
            TryGetComponent(out pickupVisualMotion);

        if (pickupVisualMotion == null)
        {
            Destroy(gameObject);
            return;
        }

        pickupVisualMotion.enabled = true;
        pickupVisualMotion.PlayCollectTo(collector, DestroySelf);
    }

    void DisablePickupColliders()
    {
        foreach (var pickupCollider in GetComponentsInChildren<Collider>())
            pickupCollider.enabled = false;
    }

    void DestroySelf()
    {
        Destroy(gameObject);
    }
}
