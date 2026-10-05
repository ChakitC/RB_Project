using UnityEngine;
using UnityEngine.AI;
using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.BehaviorDesigner.Runtime.Tasks.Actions;
using Opsive.GraphDesigner.Runtime.Variables;

public class AiShoot : Action
{
    public SharedVariable<CharacteContext> context;
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

    [Tooltip("ถ้าเปิด จะไม่หมุนเอง แต่ส่งเป้าให้ AimAxisDriver หมุนใน LateUpdate แทน " +
             "(จำเป็นเมื่อ bone ที่จะหมุนถูก Animator/Animancer เขียนทับ) — " +
             "ตัวเช็ค aimToleranceDegrees ยังทำงานปกติโดยถามค่าจาก driver")]
    public bool delegateRotation;

    [Tooltip("Horizontal turn speed while this task is active (degrees per second).")]
    public float turnSpeed = 720f;

    [Tooltip("ถ้าใส่ จะหมุน transform นี้แทนตัว actor หลัก เช่น model/pivot (เช่น Row bone ของเทอร์เรท)\n" +
             "หมายเหตุ: field นี้ถูกเมินเมื่อ delegateRotation = true — ตัวที่จะหมุนกำหนดบน AimAxisDriver.yawTarget แทน")]
    public SharedVariable<GameObject> rotateRoot;

    [Tooltip("ปกติ NPC ยิงกันในระนาบพื้น เลยควรไม่สนแกน Y")]
    public bool ignoreYAxis = true;

    [Header("Fire Solution")]
    [Tooltip("Stop firing and leave this task when the sensor loses line of sight.")]
    public bool requireLineOfSight = true;

    [Tooltip("Minimum planar distance required before firing. Set to 0 to disable.")]
    [Min(0f)] public float minFireRange = 0.5f;

    [Tooltip("Maximum planar distance allowed while firing. Set to 0 to disable.")]
    [Min(0f)] public float maxFireRange = 10f;

    [Tooltip("Maximum horizontal angle error allowed before firing. Only used when Face Target is enabled.")]
    [Range(0f, 180f)] public float aimToleranceDegrees = 8f;

    [Tooltip("How long a blocked or invalid firing solution may persist before this task fails so the tree can reposition.")]
    [Min(0f)] public float fireSolutionLossTimeout = 0.5f;

    private enum ShootPhase
    {
        Firing,
        Waiting
    }

    private ShootPhase phase;
    private float stateEndTime;
    private Transform actorTransform;
    private Transform _rotateTransform;
    private NavMeshAgent agent;
    private AITargetSensor targetSensor;
    private bool agentRotationCaptured;
    private bool cachedAgentUpdateRotation;
    private bool phaseTimerStarted;
    private float fireSolutionLostTime;
    private Transform cachedTarget;
    private IAITargetable cachedTargetable;
    private Transform cachedAimPoint;
    private AimAxisDriver _aimDriver;
    private AITargetHandle firingTargetHandle;
    private int rotationToken;
    private CharacterMovementMotor executionMotor;
    private StateHub executionStateHub;
    private float reloadElapsed;
    private float phaseElapsed;
    private bool ownsFire;
    private AITargetHandle firingOwnerHandle;
    public float reloadCycleTimeout = 10f;

    public override void OnStart()
    {
        CTX = AIContextBinding.Read(context);
        executionMotor = CTX != null ? CTX.MovementMotor : null;
        executionStateHub = CTX != null ? CTX.stateHub : null;

        actorTransform = CTX != null ? CTX.transform : transform;
        _rotateTransform =
            (rotateRoot != null && rotateRoot.Value != null)
            ? rotateRoot.Value.transform
            : actorTransform;
        _aimDriver = (CTX != null && delegateRotation)
            ? CTX.AimAxisDriver
            : null;
        targetSensor = ResolveTargetSensor();
        agentRotationCaptured = false;
        agent = CTX != null ? CTX.NavigationAgent : null;
        firingTargetHandle = targetSensor != null ? targetSensor.ReadSnapshot.Target : default;
        firingOwnerHandle = new AITargetHandle(CTX);
        rotationToken = 0;
        reloadElapsed = phaseElapsed = 0f;
        ownsFire = false;

        if (faceTarget && agent != null && AICombatTaskUtility.CanAct(CTX))
        {
            if (CTX.MovementMotor != null)
                rotationToken = CTX.MovementMotor.AcquireAgentSuspension(agent, false, true);
            else
            {
                cachedAgentUpdateRotation = agent.updateRotation;
                agent.updateRotation = false;
                agentRotationCaptured = true;
            }
        }

        phase = ShootPhase.Firing;
        phaseTimerStarted = false;
        fireSolutionLostTime = float.NegativeInfinity;
        ClearTargetCache();

        if (AICombatTaskUtility.CanAct(CTX))
        {
            // กันเคสจาก state เก่าค้างอยู่
            CTX.stateHub.RequestCanceledFire();
        }
    }

    public override TaskStatus OnUpdate()
    {
        if (!AIContextBinding.IsCurrent(context, CTX, firingOwnerHandle.Generation) ||
            CTX.stateHub != executionStateHub || CTX.AITargetSensor != targetSensor ||
            (delegateRotation && (CTX.AimAxisDriver == null || CTX.AimAxisDriver != _aimDriver)))
        {
            StopFire(CTX);
            return TaskStatus.Failure;
        }
        if (targetSensor == null) return TaskStatus.Failure;

        

        if (CTX.stateHub == null)
            return TaskStatus.Failure;

        if (!firingOwnerHandle.IsLive || !AICombatTaskUtility.CanAct(CTX))
        {
            StopFire(CTX);
            return TaskStatus.Failure;
        }
        if (AICombatTaskUtility.IsPaused)
        {
            StopFire(CTX);
            return TaskStatus.Running;
        }

        GameObject currentTarget = (target != null) ? target.Value : null;

        // ไม่มีเป้าหมาย
        if (currentTarget == null)
        {
            StopFire(CTX);
            return TargetLostStatus();
        }

        AITargetSnapshot sensorState = targetSensor != null ? targetSensor.ReadSnapshot : AITargetSnapshot.Empty;
        if (targetSensor != null && (sensorState.Target != firingTargetHandle ||
            !AICombatTaskUtility.IsEligible(CTX, firingTargetHandle)))
        {
            StopFire(CTX);
            return TargetLostStatus();
        }
        CacheTarget(currentTarget.transform);
        if (!IsTaskTargetStillValid(currentTarget.transform, sensorState))
        {
            StopFire(CTX);
            return TargetLostStatus();
        }

        Vector3 aimPoint = targetSensor != null ? sensorState.AimPosition : ResolveAimPoint(currentTarget.transform);
        RotateTowardTarget(aimPoint);

        if (!HasValidFiringPosition(aimPoint, sensorState))
            return WaitForFiringSolutionOrFail();

        // กระสุนหมด / รีโหลดอยู่
        WeaponSystem weapon = CTX.WeaponSystem;
        if (weapon == null || weapon.CurrentWeapon == null || weapon.FirePoint == null)
        {
            StopFire(CTX);
            return TaskStatus.Failure;
        }
        if (weapon.IsReloading || weapon.IsMagazineEmpty)
        {
            StopFire(CTX);
            if (!weapon.IsReloading && !weapon.CanReload) return TaskStatus.Failure;
            reloadElapsed += AICombatTaskUtility.Delta(CTX);
            if (reloadElapsed >= reloadCycleTimeout) return TaskStatus.Failure;
            if (!weapon.IsReloading) CTX.stateHub.RequestReload();
            return TaskStatus.Running;
        }
        reloadElapsed = 0f;
        if (!CTX.stateHub.CanShoot())
        {
            StopFire(CTX);
            return TaskStatus.Failure;
        }

        // ---------- Phase: Firing ----------
        if (phase == ShootPhase.Firing)
        {
            if (!IsAimAligned(aimPoint))
                return WaitForFiringSolutionOrFail();

            fireSolutionLostTime = float.NegativeInfinity;

            if (!phaseTimerStarted)
            {
                float fireTime = (fireDuration != null) ? fireDuration.Value : 3f;
                stateEndTime = GetActorTime() + Mathf.Max(0.01f, fireTime);
                phaseTimerStarted = true;
                phaseElapsed = 0f;
            }

            StartFire(CTX);

            phaseElapsed += AICombatTaskUtility.Delta(CTX);
            if (phaseElapsed >= Mathf.Max(.01f, fireDuration != null ? fireDuration.Value : 3f))
            {
                StopFire(CTX);

                phase = ShootPhase.Waiting;
                float waitTime = (waitDuration != null) ? waitDuration.Value : 5f;
                stateEndTime = GetActorTime() + Mathf.Max(0.01f, waitTime);
                phaseElapsed = 0f;
            }

            return TaskStatus.Running;
        }

        // ---------- Phase: Waiting ----------
        fireSolutionLostTime = float.NegativeInfinity;
        StopFire(CTX);

        phaseElapsed += AICombatTaskUtility.Delta(CTX);
        if (phaseElapsed >= Mathf.Max(.01f, waitDuration != null ? waitDuration.Value : 5f))
        {
            // รอครบแล้ว จบ task สำเร็จ
            return TaskStatus.Success;
        }

        return TaskStatus.Running;
    }

    public override void OnEnd()
    {
        bool sameLife = CTX != null && CTX.LifeGeneration == firingOwnerHandle.Generation;
        if (sameLife)
        {
            if (rotationToken != 0 && executionMotor != null)
                executionMotor.ReleaseAgentSuspension(rotationToken);
            if (agentRotationCaptured && agent != null && AICombatTaskUtility.CanAct(CTX))
                agent.updateRotation = cachedAgentUpdateRotation;
            if (delegateRotation && _aimDriver != null) _aimDriver.ClearAimPoint();
            StopFire(CTX);
        }
        rotationToken = 0;
        agentRotationCaptured = false;
        ownsFire = false;
    }

    public override void Reset()
    {
        target = null;
        fireDuration = 3f;
        waitDuration = 5f;
        returnSuccessWhenTargetLost = true;
        faceTarget = false;
        delegateRotation = false;
        turnSpeed = 720f;
        rotateRoot = null;
        ignoreYAxis = true;
        requireLineOfSight = true;
        minFireRange = 0.5f;
        maxFireRange = 10f;
        aimToleranceDegrees = 8f;
        fireSolutionLossTimeout = 0.5f;
    }

    private AITargetSensor ResolveTargetSensor() => CTX != null ? CTX.AITargetSensor : null;

    private void CacheTarget(Transform currentTarget)
    {
        if (cachedTarget == currentTarget)
            return;

        cachedTarget = currentTarget;
        cachedTargetable = FindTargetable(currentTarget);
        cachedAimPoint = cachedTargetable != null ? cachedTargetable.AimPoint : null;
    }

    private void ClearTargetCache()
    {
        cachedTarget = null;
        cachedTargetable = null;
        cachedAimPoint = null;
    }

    private IAITargetable FindTargetable(Transform currentTarget)
    {
        if (currentTarget == null)
            return null;

        MonoBehaviour[] parentComponents =
            currentTarget.GetComponentsInParent<MonoBehaviour>(true);
        for (int i = 0; i < parentComponents.Length; i++)
        {
            if (parentComponents[i] is IAITargetable targetable)
                return targetable;
        }

        MonoBehaviour[] childComponents =
            currentTarget.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < childComponents.Length; i++)
        {
            if (childComponents[i] is IAITargetable targetable)
                return targetable;
        }

        return null;
    }

    private bool IsTaskTargetStillValid(Transform currentTarget, AITargetSnapshot sensorState)
    {
        if (cachedTargetable != null &&
            (!cachedTargetable.IsAlive || !cachedTargetable.IsTargetable))
        {
            return false;
        }

        if (targetSensor == null)
            return true;

        Transform sensorTarget = sensorState.CurrentTarget;
        return sensorTarget != null && IsSameTarget(sensorTarget, currentTarget);
    }

    private static bool IsSameTarget(Transform first, Transform second)
    {
        return first == second ||
               first.IsChildOf(second) ||
               second.IsChildOf(first);
    }

    private Vector3 ResolveAimPoint(Transform currentTarget)
    {
        if (cachedAimPoint != null)
            return cachedAimPoint.position;

        return currentTarget != null ? currentTarget.position : actorTransform.position;
    }

    private bool HasValidFiringPosition(Vector3 aimPoint, AITargetSnapshot sensorState)
    {
        if (targetSensor != null && requireLineOfSight && !sensorState.HasLineOfSight)
            return false;

        Vector3 offset = aimPoint - actorTransform.position;
        offset.y = 0f;
        float distance = offset.magnitude;

        if (minFireRange > 0f && distance < minFireRange)
            return false;

        return maxFireRange <= 0f || distance <= maxFireRange;
    }

    private bool IsAimAligned(Vector3 aimPoint)
    {
        if (delegateRotation)
        {
            if (_aimDriver == null || aimToleranceDegrees <= 0f)
                return true;

            return _aimDriver.AngleTo(aimPoint) <= aimToleranceDegrees;
        }

        if (!faceTarget || aimToleranceDegrees <= 0f || _rotateTransform == null)
            return true;

        Vector3 direction = aimPoint - _rotateTransform.position;
        if (ignoreYAxis) direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
            return true;

        return Vector3.Angle(_rotateTransform.forward, direction) <= aimToleranceDegrees;
    }

    private TaskStatus WaitForFiringSolutionOrFail()
    {
        StopFire(CTX);

        float now = GetActorTime();
        if (float.IsNegativeInfinity(fireSolutionLostTime))
            fireSolutionLostTime = now;

        return fireSolutionLossTimeout <= 0f ||
               now - fireSolutionLostTime >= fireSolutionLossTimeout
            ? TaskStatus.Failure
            : TaskStatus.Running;
    }

    private TaskStatus TargetLostStatus()
    {
        bool successWhenLost =
            returnSuccessWhenTargetLost != null && returnSuccessWhenTargetLost.Value;
        return successWhenLost ? TaskStatus.Success : TaskStatus.Failure;
    }

    private float GetActorTime()
    {
        if (CTX != null && CTX.UsesWorldSlow && TimeSlowManager.Instance != null)
            return TimeSlowManager.Instance.WorldTime;

        return Time.time;
    }

    private void RotateTowardTarget(Vector3 aimPoint)
    {
        if (delegateRotation)
        {
            if (_aimDriver != null)
                _aimDriver.SetAimPoint(aimPoint);
            return;
        }

        if (!faceTarget ||
            turnSpeed <= 0f ||
            _rotateTransform == null ||
            !CTX.stateHub.CanRotate())
        {
            return;
        }

        Vector3 direction = aimPoint - _rotateTransform.position;
        if (ignoreYAxis) direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
            return;

        float deltaTime =
            CTX.UsesWorldSlow && TimeSlowManager.Instance != null
                ? TimeSlowManager.Instance.WorldDeltaTime
                : Time.deltaTime;

        Quaternion targetRotation =
            Quaternion.LookRotation(direction.normalized, Vector3.up);
        _rotateTransform.rotation = Quaternion.RotateTowards(
            _rotateTransform.rotation,
            targetRotation,
            turnSpeed * deltaTime);
    }

    private void StartFire(CharacteContext ctx)
    {
        ownsFire = true;
        ctx.stateHub.RequestOnFire();
    }

    private void StopFire(CharacteContext ctx)
    {
        if (!ownsFire || !firingOwnerHandle.IsLive) { ownsFire = false; return; }
        executionStateHub?.RequestCanceledFire();
        ownsFire = false;
    }

    public override void OnBehaviorTreeStopped(bool paused) { if (!paused) OnEnd(); }
    public override void OnDestroy() => OnEnd();
}
