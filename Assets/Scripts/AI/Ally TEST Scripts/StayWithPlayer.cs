using UnityEngine;
using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.BehaviorDesigner.Runtime.Tasks.Conditionals;
using Opsive.GraphDesigner.Runtime;               // สำหรับ NodeDescription/Category
using Opsive.GraphDesigner.Runtime.Variables;    // สำหรับ SharedVariable<T>

[Opsive.Shared.Utility.Category("Ally/Utility")]
[Opsive.Shared.Utility.Description("เช็คว่า Ally ควรอยู่ในโหมดตามผู้เล่นหรือเปล่า")]
public class StayWithPlayer : Conditional
{
    [Tooltip("ถ้า true = Ally จะอยู่ในโหมดตามผู้เล่น")]
    public SharedVariable<bool> stayWithPlayer;

    public override TaskStatus OnUpdate()
    {
        // ถ้าไม่ได้ assign ตัวแปรไว้ในกราฟ ให้ถือว่าให้ตามผู้เล่นเสมอ
        if (stayWithPlayer == null)
        {
            return TaskStatus.Success;
        }

        // true = Success (ให้ไปทำกิ่ง Follow Player ต่อ)
        // false = Failure (กิ่งนี้ไม่รัน เช่น ไปใช้โหมดอื่นแทน)
        return stayWithPlayer.Value ? TaskStatus.Success : TaskStatus.Failure;
    }
}
