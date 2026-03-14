using UnityEngine;
using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.BehaviorDesigner.Runtime.Tasks.Conditionals;
using Opsive.GraphDesigner.Runtime.Variables;
using Unity.Behavior;

[Opsive.Shared.Utility.Category("Ally/Enemy")]
[NodeDescription("Success ถ้า AllyEnemySensor เจอศัตรู และอัปเดต currentEnemy ให้ BT")]
public class HasEnemyFromSensor : Conditional
{
    public SharedVariable<GameObject> currentEnemy;
    public SharedVariable<bool> InCombat;

    private AllyEnemySensor _sensor;

    public override void OnStart()
    {
        if (_sensor == null)
            _sensor = GetComponent<AllyEnemySensor>();
    }

    public override TaskStatus OnUpdate()
    {
        if (_sensor == null)
            return TaskStatus.Failure;

        var target = _sensor.currentTarget;

        if (target != null)
        {
            currentEnemy.Value = target.gameObject;
            InCombat.Value = true;
            return TaskStatus.Success;
        }
        else
        {
            InCombat.Value = false;
        }
        
        currentEnemy.Value = null;
        return TaskStatus.Failure;
    }
}