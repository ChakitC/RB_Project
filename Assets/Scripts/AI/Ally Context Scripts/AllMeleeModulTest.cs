using UnityEngine;

public class AllMeleeModulTest : MonoBehaviour
{
   [Header("Settings")]
   public float Damage = 10;
   public float detectionRadius = 5f;
   public LayerMask enemyLayer;         
   public LayerMask obstacleLayer;
   public GameObject vfx;
   
   [Header("เวลานับถอยหลัง (วินาที)")]
   public float countdownTime = 0f;
   public bool Meleecontrol = true;

   
   [Header("Debug")]
   public Transform currentTarget;

   public void MeleeAttack()
   {
      if (!Meleecontrol && currentTarget != null)
      {
         return;
      }
      
      Collider[] hits = Physics.OverlapSphere(transform.position, detectionRadius, enemyLayer);
      float closestDist = Mathf.Infinity;

      Transform closest = null;

      foreach (var hit in hits)
      {

         Transform enemyRoot = hit.transform;

         var enemyHealth = enemyRoot.GetComponent<Enemy>();
         if (enemyHealth != null && !enemyHealth.IsAlive) continue;

         Vector3 dir = enemyRoot.position - transform.position;
         float dist = dir.magnitude;


         if (Physics.Raycast(transform.position + Vector3.up, dir.normalized, out RaycastHit rh, dist, obstacleLayer))
         {
            continue;
         }

         if (dist < closestDist)
         {

            //-----------------------------------------------------------------
            IDamageable damageable = enemyRoot.GetComponent<IDamageable>();
            damageable.TakeDamage(Damage);
            VfxSpawner.Instance.SpawnVfx(vfx, enemyRoot.position, enemyRoot.position, 2, 3);
            //-----------------------------------------------------------------

            closestDist = dist;
            closest = enemyRoot;
         }
      }

      Meleecontrol = false;
      currentTarget = closest;
      StartCoroutine(CountdownRoutine());
   }

   private System.Collections.IEnumerator CountdownRoutine()
      {
         float timeLeft = countdownTime;

         while (timeLeft > 0f)
         {
            Debug.Log($"Countdown: {timeLeft}"); 
            yield return new WaitForSeconds(1f);
            timeLeft -= 1f;
         }
         Meleecontrol = true;
         Debug.Log("Countdown Finished!");
      }
   

}
