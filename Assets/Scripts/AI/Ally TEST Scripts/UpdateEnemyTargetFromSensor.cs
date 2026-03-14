using UnityEngine;
using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.BehaviorDesigner.Runtime.Tasks.Actions;
using Opsive.GraphDesigner.Runtime;
using Opsive.GraphDesigner.Runtime.Variables;

[Opsive.Shared.Utility.Category("Ally/Enemy")]
[NodeDescription("ดึงเป้าหมายจาก AllyEnemySensor มาใส่ในตัวแปร BT")]
public class UpdateEnemyTargetFromSensor : Action
{
    public SharedVariable<GameObject> currentEnemy;

    private AllyEnemySensor _sensor;

    public override void OnStart()
    {
        if (_sensor == null)
            _sensor = GetComponent<AllyEnemySensor>();
    }

    public override TaskStatus OnUpdate()
    {
        if (_sensor == null)
            return TaskStatus.Success;

        if (_sensor.currentTarget != null)
        {
            currentEnemy.Value = _sensor.currentTarget.gameObject;
        }
        else
        {
            currentEnemy.Value = null;
        }
        
        return TaskStatus.Failure;   
    }
}