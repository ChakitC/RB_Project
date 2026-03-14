using UnityEngine;
using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.BehaviorDesigner.Runtime.Tasks.Conditionals;
using Opsive.GraphDesigner.Runtime;
using Opsive.GraphDesigner.Runtime.Variables;

[Opsive.Shared.Utility.Category("Ally/Enemy")]
[NodeDescription("Success ถ้ามีเป้าสด มี Line of Sight และเป้าอยู่ในระยะยิง")]
public class IsTargetInShootRange : Conditional
{
    [Header("Refs")]
    [Tooltip("Shared AllyContext ที่ใช้ดึง AITargetSensor ของตัว AI นี้")]
    public SharedVariable<AllyContext> context;

    [Tooltip("Sensor ที่ใช้ตรวจจับเป้าหมาย จะถูกดึงมาจาก context ตอน OnStart")]
    private AITargetSensor sensor;

    [Header("Settings")]
    [Tooltip("ระยะยิงสูงสุด ถ้าเป้าหมายอยู่ใกล้กว่าหรือเท่าค่านี้จะคืน Success")]
    public SharedVariable<float> shootRange = 8f;

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
        if (context == null || context.Value == null)
        {
            sensor = null;
            return;
        }

        var ctx = context.Value;
        sensor = ctx.AITargetSensor;
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

        return dist <= shootRange.Value ? TaskStatus.Success : TaskStatus.Failure;
    }

    private void ClearOutputs()
    {
        if (currentEnemy != null) currentEnemy.Value = null;
        if (currentDistance != null) currentDistance.Value = 0f;
    }

    public override void Reset()
    {
        context = null;
        sensor = null;
        shootRange = 8f;
        requireLiveTarget = true;
        requireLineOfSight = true;
        currentEnemy = null;
        currentDistance = 0f;
    }
}