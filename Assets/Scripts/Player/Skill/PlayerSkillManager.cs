using UnityEngine;

public class PlayerSkillManager : MonoBehaviour
{
    private CharacteContext ctx;
    public ISkillUser skillUser;
    public SkillSlot[] slots;
    
    private void Awake()
    {
        skillUser = GetComponent<ISkillUser>();
        if (skillUser == null)
            Debug.LogError("ไม่มี Component ที่ implement ISkillUser อยู่บน GameObject นี้");
        
        foreach (var slot in slots)
        {
            if (slot.skillAsset == null)
            {
                slot.runtimeSkill = null;
                continue;
            }

            // 1) สร้าง SkillInstance จาก SkillGemDefinition
            var instance = new SkillInstance
            {
                def   = slot.skillAsset,
                level = 1, 
            };

            // 2) แปลง SupportGemDefinition Inspector → SupportInstance
            if (slot.supportAssets != null)
            {
                foreach (var supportAsset in slot.supportAssets)
                {
                    if (supportAsset == null) continue;

                    var supportInstance = new SupportInstance
                    {
                        def   = supportAsset,
                        level = 1,
                    };

                    instance.supports.Add(supportInstance);
                }
            }

            slot.runtimeSkill = instance;
        }
    }

    private void Update()
    {
        foreach (var slot in slots)
        {
            if (Input.GetKeyDown(slot.hotkey))
                TryCast(slot);
        }
    }

    private void TryCast(SkillSlot slot)
    {
        var skill = slot.runtimeSkill;
        if (skill == null) return;

        if (!skill.CanCast(skillUser)) return;

        skill.Cast(skillUser);
    }

    public void ClearSlot(int index)
    {
        if (index < 0 || index >= slots.Length) return;

        slots[index].skillAsset   = null;
        slots[index].supportAssets = null;
        slots[index].runtimeSkill = null;
    }

    public void AssignSkillToSlot(int index, SkillGemDefinition asset, int level = 1)
    {
        if (index < 0 || index >= slots.Length) return;

        slots[index].skillAsset = asset;
        slots[index].runtimeSkill = asset == null
            ? null
            : new SkillInstance { def = asset, level = level };
        
    }
   
   
    
}
