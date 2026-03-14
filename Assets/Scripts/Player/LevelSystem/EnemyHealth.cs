using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class EnemyHealth : HealthSystem, IDamageableWithSource 
{
    [Min(0)] public int xpReward = 25;

    [SerializeField] NavMeshAgent  agent;
    private GameObject _lastAttacker;
    private bool _xpGranted;
    
    [SerializeField] private EnemyContext EnemyContextCTX;
   
    public void TakeDamage(float finalDamage, GameObject attacker)
    {
        if (!IsAlive) return;                 
        if (attacker != null) _lastAttacker = attacker;
        
        
        base.TakeDamage(finalDamage);        
       
    }

    public override void Die()
    {
      
        if (_xpGranted) return;              
        _xpGranted = true;

        
        GiveXpTo(_lastAttacker);
        
        if (agent)
        {
            agent.isStopped = true;
            agent.ResetPath();
            agent.updateRotation = false;
            agent.updatePosition = false; 
        }
        
        if(EnemyContextCTX != null) EnemyContextCTX.Collider.enabled = false;
        
        base.Die();
       
    }

   

    private void GiveXpTo(GameObject attacker)
    {
        Debug.Log("GiveXP");

        if (XpManager.Instance == null)
        {
            Debug.Log("XpManager is null");
            return;
        }
        
        if (attacker == null || xpReward <= 0) return;

        var level = attacker.GetComponentInParent<LevelSystem>();
        if (level != null)
        {
            Debug.Log($"{attacker} Get Exp");
            XpManager.Instance.GrantXp(level, xpReward);
        }
    }
}