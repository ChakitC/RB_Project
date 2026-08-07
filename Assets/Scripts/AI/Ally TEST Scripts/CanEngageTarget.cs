using UnityEngine;
using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.BehaviorDesigner.Runtime.Tasks.Conditionals;
using Opsive.GraphDesigner.Runtime.Variables;

[Opsive.Shared.Utility.Category("AI/Combat")]
[Opsive.Shared.Utility.Description(
    "Success while `target` still matches AITargetSensor.CurrentTarget and rotation isn't blocked " +
    "(stagger/stun/ChainReady). Place as a guard in front of a Combat sequence with reevaluation " +
    "enabled so a mid-action retarget (taunt) or a stagger interrupt aborts the branch immediately " +
    "instead of every action task re-implementing the same check.")]
public class CanEngageTarget : Conditional
{
    public SharedVariable<GameObject> target;

    private CharacteContext ctx;
    private AITargetSensor sensor;

    public override void OnStart()
    {
        CacheReferences();
    }

    public override TaskStatus OnUpdate()
    {
        CacheReferences();
        return Evaluate() ? TaskStatus.Success : TaskStatus.Failure;
    }

    public override TaskStatus OnReevaluateUpdate()
    {
        CacheReferences();
        return Evaluate() ? TaskStatus.Success : TaskStatus.Failure;
    }

    bool Evaluate()
    {
        if (ctx == null || ctx.stateHub == null)
            return false;

        // Rotation is what "engaging" ultimately requires — every current combat action task
        // (AiShoot, TargetOrbitNavMesh) silently skips its own rotation when this is false instead
        // of aborting, so the character keeps firing/orbiting without ever facing the target.
        if (!ctx.stateHub.CanRotate())
            return false;

        if (target == null || target.Value == null)
            return false;

        // HasEnemyFromSensor keeps this shared variable synced to the sensor via OnTargetChanged,
        // so this is normally already true — kept as a defensive cross-check for trees that feed
        // `target` from somewhere else.
        if (sensor == null)
            return true;

        Transform sensorTarget = sensor.CurrentTarget;
        return sensorTarget != null && IsSameTarget(sensorTarget, target.Value.transform);
    }

    static bool IsSameTarget(Transform a, Transform b)
    {
        return a == b || a.IsChildOf(b) || b.IsChildOf(a);
    }

    void CacheReferences()
    {
        if (ctx == null)
        {
            ctx = gameObject.GetComponent<CharacteContext>();
            if (ctx == null)
                ctx = gameObject.GetComponentInParent<CharacteContext>();
        }

        if (sensor == null && ctx != null)
        {
            AllyContext allyContext = ctx as AllyContext;
            if (allyContext != null && allyContext.AITargetSensor != null)
            {
                sensor = allyContext.AITargetSensor;
            }
            else
            {
                sensor = ctx.GetComponent<AITargetSensor>();
                if (sensor == null)
                    sensor = ctx.GetComponentInChildren<AITargetSensor>(true);
            }
        }
    }

    public override void Reset()
    {
        target = null;
        ctx = null;
        sensor = null;
    }
}
