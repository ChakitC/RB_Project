using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class EnemyHealth : HealthSystem, IDamageableWithSource
{
    [Min(0)] public int xpReward = 25;

    [SerializeField] NavMeshAgent agent;
    [SerializeField] private EnemyContext EnemyContextCTX;

    GameObject _lastAttacker;
    bool _xpGranted;

    public void TakeDamage(float finalDamage, GameObject attacker)
    {
        if (!IsAlive)
            return;

        if (attacker != null)
            _lastAttacker = attacker;

        ApplyDamage(finalDamage, attacker);
    }

    public override void TakeDamage(in DamageContext damageContext)
    {
        if (!IsAlive)
            return;

        if (damageContext.Attacker != null)
            _lastAttacker = damageContext.Attacker;

        ApplyDamage(in damageContext);
    }

    public override void Die()
    {
        if (_xpGranted)
            return;

        _xpGranted = true;
        GiveXpTo(_lastAttacker);

        if (agent)
        {
            agent.isStopped = true;
            agent.ResetPath();
            agent.updateRotation = false;
            agent.updatePosition = false;
        }

        if (EnemyContextCTX != null)
            EnemyContextCTX.Collider.enabled = false;

        base.Die();
    }

    void GiveXpTo(GameObject attacker)
    {
        if (XpManager.Instance == null || attacker == null || xpReward <= 0)
            return;

        var level = attacker.GetComponentInParent<LevelSystem>();
        if (level != null)
            XpManager.Instance.GrantXp(level, xpReward);
    }
}
