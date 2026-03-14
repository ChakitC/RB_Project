using System;
using Opsive.BehaviorDesigner.Runtime.Tasks.Actions;
using UnityEngine;

public class SimpleAIShooterTester : MonoBehaviour
{
    [SerializeField] private CharacteContext CTX;
    public Transform target;
    public AllyEnemySensor AllyEnemySensor;
    public bool MeleeOnly = false;
    public float rotateSpeed = 15f;  
    
    void Update()
    {
        if (CTX.stateHub.Isdown || !CTX.stateHub.IsAlive)
        {
            CTX.stateHub.RequestCanceledFire(); 
            return;
        } 
        if (MeleeOnly){return;}
        
        if (CTX.WeaponSystem.magazine <= 0)
        {
            CTX.stateHub.RequestReload();
        }

        target = AllyEnemySensor.currentTarget;
        if (target == null)
        {
            CTX.stateHub.RequestCanceledFire(); 
            return;
        };
        
        RotateToTarget();
        if (!CTX.stateHub.CanShoot())return;
        CTX.stateHub.RequestOnFire();
        
    }
    
    private void RotateToTarget()
    {
        if(!CTX.stateHub.CanMove()) return;
        Vector3 dir = target.position - transform.position;
        dir.y = 0f; // ไม่หันขึ้นลง

        if (dir.sqrMagnitude < 0.001f) return;

        Quaternion targetRot = Quaternion.LookRotation(dir.normalized);
        transform.rotation = Quaternion.Lerp(
            transform.rotation,
            targetRot,
            rotateSpeed * Time.deltaTime
        );
    }

    

    void OnDisable()
    {
        CTX.WeaponSystem.SetFiring(false);
        CTX.stateHub.SetFireHeld(false); 
    }
}