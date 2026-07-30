using UnityEngine;
using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.BehaviorDesigner.Runtime.Tasks.Conditionals;
using Opsive.GraphDesigner.Runtime;
using Opsive.GraphDesigner.Runtime.Variables;

[Opsive.Shared.Utility.Category("Ally/Utility")]
[Opsive.Shared.Utility.Description("เช็คว่ามี reference ไปที่ Player หรือยัง")]
public class HasPlayer : Conditional
{
    [Tooltip("ตัวแปร Player ที่ใช้แชร์ใน Behavior Tree")]
    public SharedVariable<GameObject> player;
    public SharedVariable<bool> InCombat;

    public override TaskStatus OnUpdate()
    {

        if (player.Value == null)
        {
            var found = GameObject.FindGameObjectWithTag("Player");
            if (found != null)
            {
                player.Value = found;
            }
        }


        if (player.Value != null && !InCombat.Value)
        {
            return TaskStatus.Success;
        }
        else
        {
            return TaskStatus.Failure;


        }
    }
}
