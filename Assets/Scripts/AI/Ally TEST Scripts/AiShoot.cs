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

    private bool isFiring;
    private float stateEndTime;

    public override void OnStart()
    {
        isFiring = true;

        float fireTime = (fireDuration != null) ? fireDuration.Value : 3f;
        stateEndTime = Time.time + Mathf.Max(0.01f, fireTime);

        var ctx = (CTX != null) ? CTX.Value : null;
        if (ctx != null && ctx.stateHub != null)
        {
            // กันเคสหลงเหลือ hold fire จาก state ก่อนหน้า
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

        GameObject currentTarget = null;
        if (target != null)
            currentTarget = target.Value;

        // ไม่มีเป้าหมาย
        if (currentTarget == null)
        {
            StopFire(ctx);
            bool successWhenLost = returnSuccessWhenTargetLost != null && returnSuccessWhenTargetLost.Value;
            return successWhenLost ? TaskStatus.Success : TaskStatus.Failure;
        }

        // กระสุนหมด / กำลังรีโหลด -> หยุดยิงก่อน
        var weaponState = ctx.stateHub.WeaponSM.CurrentId;
        if (weaponState == WeaponStateId.NoBullet || weaponState == WeaponStateId.Reloading)
        {
            StopFire(ctx);
            ctx.stateHub.RequestReload();
            return TaskStatus.Running;
        }

        // สลับ phase ยิง <-> รอ
        if (Time.time >= stateEndTime)
        {
            isFiring = !isFiring;

            float duration = isFiring
                ? ((fireDuration != null) ? fireDuration.Value : 3f)
                : ((waitDuration != null) ? waitDuration.Value : 5f);

            stateEndTime = Time.time + Mathf.Max(0.01f, duration);
        }

        if (isFiring)
            StartFire(ctx);
        else
            StopFire(ctx);

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