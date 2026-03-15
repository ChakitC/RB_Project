using UnityEngine;
using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.BehaviorDesigner.Runtime.Tasks.Actions;
using Opsive.GraphDesigner.Runtime.Variables;

public class AiShoot : Action
{
    public SharedVariable<AllyContext> CTX;
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
        var ctx = (CTX != null) ? CTX.Value : null;

        phase = ShootPhase.Firing;

        float fireTime = (fireDuration != null) ? fireDuration.Value : 3f;
        stateEndTime = Time.time + Mathf.Max(0.01f, fireTime);

        if (ctx != null && ctx.stateHub != null)
        {
            // กันเคสจาก state เก่าค้างอยู่
            ctx.stateHub.RequestCanceledFire();
        }
    }

    public override TaskStatus OnUpdate()
    {
        if (CTX == null || CTX.Value == null)
            return TaskStatus.Failure;

        var ctx = CTX.Value;

        if (ctx.stateHub == null)
            return TaskStatus.Failure;

        GameObject currentTarget = (target != null) ? target.Value : null;

        // ไม่มีเป้าหมาย
        if (currentTarget == null)
        {
            StopFire(ctx);
            bool successWhenLost = returnSuccessWhenTargetLost != null && returnSuccessWhenTargetLost.Value;
            return successWhenLost ? TaskStatus.Success : TaskStatus.Failure;
        }

        // กระสุนหมด / รีโหลดอยู่
        var weaponState = ctx.stateHub.WeaponSM.CurrentId;
        if (weaponState == WeaponStateId.NoBullet || weaponState == WeaponStateId.Reloading)
        {
            StopFire(ctx);
            ctx.stateHub.RequestReload();
            return TaskStatus.Running;
        }

        // ---------- Phase: Firing ----------
        if (phase == ShootPhase.Firing)
        {
            StartFire(ctx);

            if (Time.time >= stateEndTime)
            {
                StopFire(ctx);

                phase = ShootPhase.Waiting;
                float waitTime = (waitDuration != null) ? waitDuration.Value : 5f;
                stateEndTime = Time.time + Mathf.Max(0.01f, waitTime);
            }

            return TaskStatus.Running;
        }

        // ---------- Phase: Waiting ----------
        StopFire(ctx);

        if (Time.time >= stateEndTime)
        {
            // รอครบแล้ว จบ task สำเร็จ
            return TaskStatus.Success;
        }

        return TaskStatus.Running;
    }

    public override void OnEnd()
    {
        var ctx = (CTX != null) ? CTX.Value : null;
        if (ctx != null && ctx.stateHub != null)
            StopFire(ctx);
    }

    private void StartFire(AllyContext ctx)
    {
        ctx.stateHub.RequestOnFire();
    }

    private void StopFire(AllyContext ctx)
    {
        ctx.stateHub.RequestCanceledFire();
    }
}