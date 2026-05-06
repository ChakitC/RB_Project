using UnityEngine;
using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.BehaviorDesigner.Runtime.Tasks.Conditionals;
using Opsive.GraphDesigner.Runtime.Variables;
using VHierarchy.Libs;

[Opsive.Shared.Utility.Category("Ally/Enemy")]
[Opsive.Shared.Utility.Description("Success ถ้ามีเป้าสด มี Line of Sight และเป้าอยู่ในช่วง Castinrange")]
public class Castinrange : Conditional
{
    [Header("Refs")]

    [Tooltip("Sensor ที่ใช้ตรวจจับเป้าหมาย จะถูกดึงมาจาก context ตอน OnStart")]
    private AITargetSensor sensor;

    [Header("Settings")]
    [Tooltip("ระยะ cast น้อยสุด ถ้าเป้าหมายใกล้กว่านี้จะคืน Failure")]
    public SharedVariable<float> minCastRange = 5f;

    [Tooltip("ค่าระยะ cast ฝั่งไกล ถ้าเป้าหมายไกลกว่าค่านี้จะคืน Failure")]
    public SharedVariable<float> maxCastRange = 6f;

    [Tooltip("ถ้าเปิดไว้ จะต้องมี CurrentTarget ที่ยังเห็นอยู่จริงเท่านั้น ถึงจะยิงได้")]
    public SharedVariable<bool> requireLiveTarget = true;

    [Tooltip("ถ้าเปิดไว้ จะต้องมี Line of Sight ถึงเป้าหมายด้วย ถึงจะคืน Success")]
    public SharedVariable<bool> requireLineOfSight = true;

    [Header("Optional Outputs")]
    [Tooltip("จะเขียน GameObject ของศัตรูปัจจุบันลงตัวแปรนี้ ถ้ามีเป้า")]
    public SharedVariable<GameObject> currentEnemy;

    [Tooltip("จะเขียนระยะห่างปัจจุบันจาก sensor ไปยังเป้าหมายลงตัวแปรนี้")]
    public SharedVariable<float> currentDistance;


   

    public override void OnStart()
    {
        if (sensor == null)
        {
            sensor = GetComponent<AITargetSensor>();
        }
        
    }

    public override TaskStatus OnUpdate()
    {
        if (sensor == null)
        {
            ClearOutputs();
            return TaskStatus.Failure;
            
        }

        if (requireLiveTarget.Value)
        {
            if (!sensor.HasLiveTarget || sensor.CurrentTarget == null)
            {
               
                ClearOutputs();
                return TaskStatus.Failure;
            }
        }
        else
        {
            if (!sensor.HasAnyTarget)
            {
               
                ClearOutputs();
                return TaskStatus.Failure;
            }
        }

        if (requireLineOfSight.Value && !sensor.HasLineOfSight)
        {
            
            ClearOutputs();
            return TaskStatus.Failure;
        }

        float dist = sensor.TargetDistance;

        if (currentDistance != null)
            currentDistance.Value = dist;

        if (sensor.CurrentTarget != null && currentEnemy != null)
            currentEnemy.Value = sensor.CurrentTarget.gameObject;

        float lowerSetting = Mathf.Min(minCastRange.Value, maxCastRange.Value);
        float upperSetting = Mathf.Max(minCastRange.Value, maxCastRange.Value);
        float minRange = Mathf.Max(0f, lowerSetting);
        float maxRange = Mathf.Max(0f, upperSetting);

        return dist >= minRange && dist <= maxRange ? TaskStatus.Success : TaskStatus.Failure;
    }

    private void ClearOutputs()
    {
        if (currentEnemy != null) currentEnemy.Value = null;
        if (currentDistance != null) currentDistance.Value = 0f;
    }

    public override void Reset()
    {
        sensor = null;
        minCastRange = 5f;
        maxCastRange = 6f;
        requireLiveTarget = true;
        requireLineOfSight = true;
        currentEnemy = null;
        currentDistance = 0f;
    }
}
