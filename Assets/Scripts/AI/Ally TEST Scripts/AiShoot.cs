using UnityEngine;
using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.BehaviorDesigner.Runtime.Tasks.Actions;
using Opsive.GraphDesigner.Runtime.Variables;

public class AiShoot : Action
{
    private CharacteContext CTX;
    public SharedVariable<GameObject> target;

    [Header("Shoot Cycle")]
    [Tooltip("ยิงกี่วิ")]
    public SharedVariable<float> fireDuration = 3f;

    [Tooltip("พักกี่วิ")]
    public SharedVariable<float> waitDuration = 5f;

    public SharedVariable<bool> returnSuccessWhenTargetLost = true;

    private enum ShootPhase
    {
        Firing,
        Waiting
    }

    private ShootPhase phase;
    private float stateEndTime;

    public override void OnStart()
    {
        if (CTX == null)
        {
            CTX = gameObject.GetComponentInParent<CharacteContext>();
        }
        

        phase = ShootPhase.Firing;

        float fireTime = (fireDuration != null) ? fireDuration.Value : 3f;
        stateEndTime = Time.time + Mathf.Max(0.01f, fireTime);

        if (CTX != null && CTX.stateHub != null)
        {
            // กันเคสจาก state เก่าค้างอยู่
            CTX.stateHub.RequestCanceledFire();
        }
    }

    public override TaskStatus OnUpdate()
    {
        if (CTX == null)
            return TaskStatus.Failure;

        

        if (CTX.stateHub == null)
            return TaskStatus.Failure;

        GameObject currentTarget = (target != null) ? target.Value : null;

        // ไม่มีเป้าหมาย
        if (currentTarget == null)
        {
            StopFire(CTX);
            bool successWhenLost = returnSuccessWhenTargetLost != null && returnSuccessWhenTargetLost.Value;
            return successWhenLost ? TaskStatus.Success : TaskStatus.Failure;
        }

        // กระสุนหมด / รีโหลดอยู่
        var weaponState = CTX.stateHub.WeaponSM.CurrentId;
        if (weaponState == WeaponStateId.NoBullet || weaponState == WeaponStateId.Reloading)
        {
            StopFire(CTX);
            CTX.stateHub.RequestReload();
            return TaskStatus.Running;
        }

        // ---------- Phase: Firing ----------
        if (phase == ShootPhase.Firing)
        {
            StartFire(CTX);

            if (Time.time >= stateEndTime)
            {
                StopFire(CTX);

                phase = ShootPhase.Waiting;
                float waitTime = (waitDuration != null) ? waitDuration.Value : 5f;
                stateEndTime = Time.time + Mathf.Max(0.01f, waitTime);
            }

            return TaskStatus.Running;
        }

        // ---------- Phase: Waiting ----------
        StopFire(CTX);

        if (Time.time >= stateEndTime)
        {
            // รอครบแล้ว จบ task สำเร็จ
            return TaskStatus.Success;
        }

        return TaskStatus.Running;
    }

    public override void OnEnd()
    {
        
        if (CTX != null && CTX.stateHub != null)
            StopFire(CTX);
    }

    private void StartFire(CharacteContext ctx)
    {
        ctx.stateHub.RequestOnFire();
    }

    private void StopFire(CharacteContext ctx)
    {
        ctx.stateHub.RequestCanceledFire();
    }
}