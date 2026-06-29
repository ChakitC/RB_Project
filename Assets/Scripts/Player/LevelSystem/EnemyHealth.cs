using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class EnemyHealth : HealthSystem
{
    [Min(0)] public int xpReward = 25;

    [SerializeField] NavMeshAgent agent;
    [SerializeField] private EnemyContext EnemyContextCTX;
    [SerializeField] private StaggerMeter staggerMeter;

    GameObject _lastAttacker;
    bool _xpGranted;

    public override DamageResult TakeDamage(in DamageContext damageContext)
    {
        if (!IsAlive)
            return new DamageResult(this, damageContext.Attacker, damageContext.Damage, 0f, false, false, IsAlive);

        if (damageContext.Attacker != null)
            _lastAttacker = damageContext.Attacker;

        StaggerMeter meter = ResolveStaggerMeter();
        DamageContext resolvedContext = damageContext;

        if (meter != null && meter.IsStaggered && damageContext.Damage > 0f)
            resolvedContext = damageContext.WithDamage(damageContext.Damage * meter.DamageTakenMultiplier);

        if (meter != null && (meter.IsChainReady || meter.IsChainExecutionActive) && resolvedContext.Damage > 0f)
        {
            float maxLethalSafe = Mathf.Max(0f, currentHealth - 1f);
            if (resolvedContext.Damage > maxLethalSafe)
                resolvedContext = resolvedContext.WithDamage(maxLethalSafe);
        }

        DamageResult result = ApplyDamage(in resolvedContext);

        if (meter != null && result.Applied && result.IsAliveAfter && damageContext.HasStagger)
            meter.ApplyStagger(damageContext.Stagger, damageContext.Attacker);

        return result;
    }

    public override void Die()
    {
        if (_xpGranted)
            return;

        _xpGranted = true;
        GiveXpTo(_lastAttacker);

        base.Die();

        if (agent)
        {
            if (agent.isActiveAndEnabled && agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.ResetPath();
            }

            agent.updateRotation = false;
            agent.updatePosition = false;
        }

        if (EnemyContextCTX != null)
        {
            if (EnemyContextCTX.Collider != null)
                EnemyContextCTX.Collider.enabled = false;

            if (EnemyContextCTX.dropper != null)
                EnemyContextCTX.dropper.DropItem();
        }
    }

    protected override GameObject GetDestroyTarget()
    {
        if (EnemyContextCTX != null)
            return EnemyContextCTX.gameObject;

        if (CTX != null)
            return CTX.gameObject;

        return base.GetDestroyTarget();
    }

    void GiveXpTo(GameObject attacker)
    {
        if (XpManager.Instance == null || attacker == null || xpReward <= 0)
            return;

        var level = attacker.GetComponentInParent<LevelSystem>();
        if (level != null)
            XpManager.Instance.GrantXp(level, xpReward);
    }

    StaggerMeter ResolveStaggerMeter()
    {
        if (staggerMeter == null)
            staggerMeter = GetComponent<StaggerMeter>();
        if (staggerMeter == null)
            staggerMeter = gameObject.AddComponent<StaggerMeter>();

        return staggerMeter;
    }
}
