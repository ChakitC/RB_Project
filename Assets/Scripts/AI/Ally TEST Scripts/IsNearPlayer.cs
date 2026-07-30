using UnityEngine;
using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.BehaviorDesigner.Runtime.Tasks.Conditionals;
using Opsive.GraphDesigner.Runtime;
using Opsive.GraphDesigner.Runtime.Variables;

[Opsive.Shared.Utility.Category("Ally/Follow")]
[Opsive.Shared.Utility.Description("Success ถ้า Ally อยู่ใกล้ Player (<= nearDistance)")]
public class IsNearPlayer : Conditional
{
    [Tooltip("ตัวแปร Player ที่แชร์ใน Behavior Tree")]
    public SharedVariable<GameObject> player;

    [Tooltip("ระยะที่ถือว่า \"ใกล้\" ผู้เล่น")]
    public SharedVariable<float> nearDistance;

    public override TaskStatus OnUpdate()
    {
        if (player == null || player.Value == null)
        {
            return TaskStatus.Failure;
        }

        Vector3 myPos = transform.position;
        Vector3 playerPos = player.Value.transform.position;

        // ทำงานในระนาบพื้น
        myPos.y = 0f;
        playerPos.y = 0f;

        float dist = Vector3.Distance(myPos, playerPos);

        return dist <= nearDistance.Value ? TaskStatus.Success : TaskStatus.Failure;
    }
}
