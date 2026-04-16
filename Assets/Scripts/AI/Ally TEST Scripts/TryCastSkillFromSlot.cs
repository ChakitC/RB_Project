using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.BehaviorDesigner.Runtime.Tasks.Actions;
using UnityEngine;

public class TryCastSkillFromSlot : ActionNode
    {
        [SerializeField] int SlotIndex;
        private CharacteContext CTX;
        public override void OnStart()
        {
            if (CTX == null)
            {
                CTX = gameObject.GetComponent<CharacteContext>();    
            }
            CTX.SkillManager.TryCastSlot(SlotIndex);
        }
    }
