using UnityEngine;
using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.BehaviorDesigner.Runtime.Tasks.Conditionals;
using Opsive.GraphDesigner.Runtime;
using Opsive.GraphDesigner.Runtime.Variables;

[Opsive.Shared.Utility.Category("Ally/Enemy")]
[NodeDescription("Success ถ้า AITargetSensor เจอศัตรูตัวปัจจุบัน และอัปเดต currentEnemy ให้ BT")]
public class HasEnemyFromSensor : Conditional
{
    public SharedVariable<GameObject> currentEnemy;
    public SharedVariable<bool> InCombat;
    
   

    private AITargetSensor sensor;

    public override void OnStart()
    {
        if (sensor == null)
        {
            sensor = gameObject.GetComponent<AITargetSensor>();
        }
    }

    public override TaskStatus OnUpdate()
    {
        if (sensor == null)
        {
            if (currentEnemy != null) currentEnemy.Value = null;
            if (InCombat != null) InCombat.Value = false;
            return TaskStatus.Failure;
        }

        Transform target = sensor.CurrentTarget;

        if (target != null)
        {
            if (currentEnemy != null) currentEnemy.Value = target.gameObject;
            if (InCombat != null) InCombat.Value = true;
            return TaskStatus.Success;
        }

        if (currentEnemy != null) currentEnemy.Value = null;
        if (InCombat != null) InCombat.Value = false;
        return TaskStatus.Failure;
    }

    public override void Reset()
    {
        sensor = null;
        currentEnemy = null;
        InCombat = false;
    }
}