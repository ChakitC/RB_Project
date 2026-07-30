using UnityEngine;
using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.BehaviorDesigner.Runtime.Tasks.Conditionals;
using Opsive.GraphDesigner.Runtime;               // NodeDescription / Category
using Opsive.GraphDesigner.Runtime.Variables;    // SharedVariable<T>

[Opsive.Shared.Utility.Category("Ally/Follow")]
[Opsive.Shared.Utility.Description("Success ถ้า Ally อยู่ไกลจาก Player มากกว่า followMax")]
public class IsTooFarFromPlayer : Conditional
{
    [Tooltip("ตัวแปร Player ที่แชร์ใน Behavior Tree")]
    public SharedVariable<GameObject> player;

    [Tooltip("ระยะสูงสุดที่ถือว่ายัง \"อยู่ใกล้\" ผู้เล่น ถ้าเกินกว่านี้จะถือว่าไกลเกินไป")]
    public SharedVariable<float> followMax;

    public override TaskStatus OnUpdate()
    {
        if (player == null || player.Value == null) {
            return TaskStatus.Failure;
        }

        var myPos      = transform.position;
        var playerPos  = player.Value.transform.position;

        // ไม่อยากให้แกน Y มีผลมากนัก ตัดเป็นระนาบพื้น
        myPos.y     = 0f;
        playerPos.y = 0f;

        float dist = Vector3.Distance(myPos, playerPos);

        // ไกลกว่า followMax → Success
        return dist > followMax.Value ? TaskStatus.Success : TaskStatus.Failure;
    }
}
