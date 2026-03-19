using System;
using UnityEngine;

public class StatusEffectPickUp : MonoBehaviour
{
    
    [SerializeField] private StatusEffectDef effect;
    [SerializeField, Min(1)] private int stacks = 1;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;
        
        var controller = other.GetComponentInParent<StatusEffectController>();
        if (controller == null || effect == null)
            return;

        controller.ApplyEffect(effect, gameObject, stacks);
        Destroy(gameObject);
    }
}
