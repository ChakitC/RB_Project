using UnityEngine;
using UnityEngine.AI;
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

    [Header("Aiming")]
    public bool faceTarget;

    [Tooltip("Horizontal turn speed while this task is active (degrees per second).")]
    public float turnSpeed = 720f;

    private enum ShootPhase
    {
        Firing,
        Waiting
    }

    private ShootPhase phase;
    private float stateEndTime;
    private Transform actorTransform;
    private NavMeshAgent agent;
    private bool agentRotationCaptured;
    private bool cachedAgentUpdateRotation;

    public override void OnStart()
    {
        if (CTX == null)
        {
            CTX = gameObject.GetComponentInParent<CharacteContext>();
        }

        actorTransform = CTX != null ? CTX.transform : transform;
        agentRotationCaptured = false;
        agent = actorTransform != null
            ? actorTransform.GetComponent<NavMeshAgent>()
            : null;

        if (faceTarget && agent != null)
        {
            cachedAgentUpdateRotation = agent.updateRotation;
            agent.updateRotation = false;
            agentRotationCaptured = true;
        }

        phase = ShootPhase.Firing;

        float fireTime = (fireDuration != null) ? fireDuration.Value : 3f;
        stateEndTime = TimeSlowManager.Instance.WorldTime + Mathf.Max(0.01f, fireTime);

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

        RotateTowardTarget(currentTarget);

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

            if (TimeSlowManager.Instance.WorldTime >= stateEndTime)
            {
                StopFire(CTX);

                phase = ShootPhase.Waiting;
                float waitTime = (waitDuration != null) ? waitDuration.Value : 5f;
                stateEndTime = TimeSlowManager.Instance.WorldTime + Mathf.Max(0.01f, waitTime);
            }

            return TaskStatus.Running;
        }

        // ---------- Phase: Waiting ----------
        StopFire(CTX);

        if (TimeSlowManager.Instance.WorldTime >= stateEndTime)
        {
            // รอครบแล้ว จบ task สำเร็จ
            return TaskStatus.Success;
        }

        return TaskStatus.Running;
    }

    public override void OnEnd()
    {
        if (agentRotationCaptured && agent != null)
            agent.updateRotation = cachedAgentUpdateRotation;

        agentRotationCaptured = false;

        if (CTX != null && CTX.stateHub != null)
            StopFire(CTX);
    }

    public override void Reset()
    {
        target = null;
        fireDuration = 3f;
        waitDuration = 5f;
        returnSuccessWhenTargetLost = true;
        faceTarget = false;
        turnSpeed = 720f;
    }

    private void RotateTowardTarget(GameObject currentTarget)
    {
        if (!faceTarget ||
            turnSpeed <= 0f ||
            actorTransform == null ||
            currentTarget == null ||
            !CTX.stateHub.CanRotate())
        {
            return;
        }

        Vector3 direction =
            currentTarget.transform.position - actorTransform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
            return;

        float deltaTime =
            CTX.UsesWorldSlow && TimeSlowManager.Instance != null
                ? TimeSlowManager.Instance.WorldDeltaTime
                : Time.deltaTime;

        Quaternion targetRotation =
            Quaternion.LookRotation(direction.normalized, Vector3.up);
        actorTransform.rotation = Quaternion.RotateTowards(
            actorTransform.rotation,
            targetRotation,
            turnSpeed * deltaTime);
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
