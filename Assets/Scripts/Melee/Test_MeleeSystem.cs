using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class MeleeHitboxTrigger : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private CharacteContext CTX;
    [SerializeField] private CharacterAnimBrain brain;
    [SerializeField] private Collider hitboxR;
    [SerializeField] private Collider hitboxL;

    [Header("Filter")]
    [SerializeField] private LayerMask targetMask = ~0;
    
    private float damage = 100f;
    private WeaponType gunType = WeaponType.Melee;
    private float baseDamage, critRate, critMult, Distance = 0;
    
    
    
    private readonly HashSet<int> _hitIds = new();
    private bool _active;

    private void Awake()
    {
        if (!brain) brain = GetComponentInParent<CharacterAnimBrain>();
     
        if (!brain) Debug.LogError("CharacterAnimBrain not found", this);
        if (!hitboxR) Debug.LogError("hitboxR not found", this);
        if (!hitboxL) Debug.LogWarning("hitboxL not assigned/found (will only use hitboxR)", this);

        // make sure trigger
        if (hitboxR) hitboxR.isTrigger = true;
        if (hitboxL) hitboxL.isTrigger = true;

        SetHitboxes(false);

        if (brain)
        {
            brain.MeleeHitStart += OnHitStart;
            brain.MeleeHitEnd   += OnHitEnd;
        }
    }

    private void Start()
    {
        if(!CTX) CTX = GetComponentInChildren<CharacteContext>();
        baseDamage = CTX.StatsHub.GetSkillBaseDamage();
        critRate = CTX.StatsHub.GetCritRatePercent(CTX.currentWeapon);
        critMult = CTX.StatsHub.GetCritMultiplier(CTX.currentWeapon);
    }

    private void OnDestroy()
    {
        if (!brain) return;
        brain.MeleeHitStart -= OnHitStart;
        brain.MeleeHitEnd   -= OnHitEnd;
    }

    private void OnHitStart()
    {
        Debug.Log("OnHitStart fired!", this);
        
        _hitIds.Clear();
        _active = true;
        
        SetHitboxes(true);
        
        
    }

    private void OnHitEnd()
    {
        Debug.Log("OnHitEnd fired!", this);
        _hitIds.Clear();
        _active = false;
        SetHitboxes(false);
        
    }

    private void SetHitboxes(bool on)
    {
        
        if (hitboxR) hitboxR.enabled = on;
        if (hitboxL) hitboxL.enabled = on;
    }

    private void OnTriggerEnter(Collider other) => TryHit(other);
    private void OnTriggerStay(Collider other)  => TryHit(other);

    private void TryHit(Collider other)
    {
        if (!_active || !other) return;

        if (((1 << other.gameObject.layer) & targetMask.value) == 0) return;

        if (brain && other.transform.root == brain.transform.root) return;

        baseDamage = CTX.StatsHub.GetSkillBaseDamage();
        critRate = CTX.StatsHub.GetCritRatePercent(CTX.currentWeapon);
        critMult = CTX.StatsHub.GetCritMultiplier(CTX.currentWeapon);
        
        var dmg = other.GetComponentInParent<IDamageable>();
        if (dmg == null) return;

        int id = (dmg as Component) ? (dmg as Component).GetInstanceID() : dmg.GetHashCode();
        if (!_hitIds.Add(id)) return;
        
        float armor = 0f;
        var armorComp = other.GetComponentInParent<IHasArmor>();
        if (armorComp != null) armor = armorComp.Armor;

        damage = DamageCalculator.CalculateFinalDamage(
            gunType,
            Distance,
            baseDamage,
            critRate,
            critMult,
            armor
        );
        
        dmg.TakeDamage(damage);
        
        Vector3 hitPoint = other.ClosestPoint(transform.position);
        if (hitPoint == Vector3.zero) hitPoint = other.bounds.center;
        if (VfxSpawner.Instance != null) { VfxSpawner.Instance.SpawnDamageNumber(hitPoint ,damage); }
    }
}
