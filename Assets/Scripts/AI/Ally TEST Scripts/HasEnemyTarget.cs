using UnityEngine;
using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.BehaviorDesigner.Runtime.Tasks.Conditionals;
using Opsive.GraphDesigner.Runtime;
using Opsive.GraphDesigner.Runtime.Variables;

[Opsive.Shared.Utility.Category("Ally/Enemy")]
[Opsive.Shared.Utility.Description("Success ถ้า AITargetSensor เจอศัตรูตัวปัจจุบัน และอัปเดต currentEnemy ให้ BT")]
public class HasEnemyFromSensor : Conditional
{
    public SharedVariable<GameObject> currentEnemy;
    public SharedVariable<bool> InCombat;

    private AITargetSensor sensor;
    private AITargetSensor subscribedSensor;

    public override void OnAwake()
    {
        CacheSensor();
        SubscribeToSensor();
        SyncOutputsFromSensor();
    }

    public override void OnBehaviorTreeStarted()
    {
        CacheSensor();
        SubscribeToSensor();
        SyncOutputsFromSensor();
    }

    public override void OnStart()
    {
        CacheSensor();
        SubscribeToSensor();
        SyncOutputsFromSensor();
    }

    public override TaskStatus OnUpdate()
    {
        CacheSensor();
        SubscribeToSensor();

        return SyncOutputsFromSensor()
            ? TaskStatus.Success
            : TaskStatus.Failure;
    }

    public override void OnBehaviorTreeStopped(bool paused)
    {
        UnsubscribeFromSensor();

        if (!paused)
            ClearOutputs();
    }

    public override void OnDestroy()
    {
        UnsubscribeFromSensor();
        base.OnDestroy();
    }

    private void CacheSensor()
    {
        if (sensor != null)
            return;

        AllyContext allyContext = gameObject.GetComponent<AllyContext>();
        if (allyContext == null)
            allyContext = gameObject.GetComponentInParent<AllyContext>();

        if (allyContext != null && allyContext.AITargetSensor != null)
        {
            sensor = allyContext.AITargetSensor;
            return;
        }

        sensor = gameObject.GetComponent<AITargetSensor>();
        if (sensor == null)
            sensor = gameObject.GetComponentInParent<AITargetSensor>();
        if (sensor == null)
            sensor = gameObject.GetComponentInChildren<AITargetSensor>(true);

        if (allyContext != null && allyContext.AITargetSensor == null)
            allyContext.AITargetSensor = sensor;
    }

    private void SubscribeToSensor()
    {
        if (sensor == subscribedSensor)
            return;

        UnsubscribeFromSensor();

        if (sensor == null)
            return;

        subscribedSensor = sensor;
        subscribedSensor.OnVisibleTargetChanged += HandleSensorVisibleTargetChanged;
    }

    private void UnsubscribeFromSensor()
    {
        if (subscribedSensor == null)
            return;

        subscribedSensor.OnVisibleTargetChanged -= HandleSensorVisibleTargetChanged;
        subscribedSensor = null;
    }

    private void HandleSensorVisibleTargetChanged(Transform oldTarget, Transform newTarget)
    {
        SyncOutputsFromTarget(newTarget);
    }

    private bool SyncOutputsFromSensor()
    {
        if (sensor == null)
        {
            ClearOutputs();
            return false;
        }

        return SyncOutputsFromTarget(sensor.VisibleTarget);
    }

    private bool SyncOutputsFromTarget(Transform target)
    {
        if (target != null)
        {
            if (currentEnemy != null) currentEnemy.Value = target.gameObject;
            if (InCombat != null) InCombat.Value = true;
            return true;
        }

        ClearOutputs();
        return false;
    }

    private void ClearOutputs()
    {
        if (currentEnemy != null) currentEnemy.Value = null;
        if (InCombat != null) InCombat.Value = false;
    }

    public override void Reset()
    {
        UnsubscribeFromSensor();
        sensor = null;
        currentEnemy = null;
        InCombat = false;
    }
}
