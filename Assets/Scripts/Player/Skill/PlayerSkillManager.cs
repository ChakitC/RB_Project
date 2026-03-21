using UnityEngine;

public class PlayerSkillManager : MonoBehaviour
{
    private CharacteContext ctx;
    public ISkillUser skillUser;
    public SkillSlot[] slots;

    private void Awake()
    {
        ctx = GetComponent<CharacteContext>();
        skillUser = GetComponent<ISkillUser>();

        if (skillUser == null)
            Debug.LogError("PlayerSkillManager requires an ISkillUser component.");

        foreach (var slot in slots)
        {
            if (slot.skillAsset == null)
            {
                slot.runtimeSkill = null;
                continue;
            }

            var instance = new SkillInstance
            {
                def = slot.skillAsset,
                level = 1
            };

            if (slot.supportAssets != null)
            {
                foreach (var supportAsset in slot.supportAssets)
                {
                    if (supportAsset == null)
                        continue;

                    var supportInstance = new SupportInstance
                    {
                        def = supportAsset,
                        level = 1
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
        if (skill == null)
            return;

        if (ctx != null && ctx.stateHub != null && !ctx.stateHub.CanUseSkill())
            return;

        if (!skill.CanCast(skillUser))
            return;

        skill.Cast(skillUser);

        if (skill.def != null && skill.def.castCue != null)
        {
            Transform castOrigin = skillUser != null && skillUser.CastOrigin != null
                ? skillUser.CastOrigin
                : transform;

            AudioService.Instance.PlayAttached(skill.def.castCue, castOrigin, Vector3.zero);
        }
    }

    public void ClearSlot(int index)
    {
        if (index < 0 || index >= slots.Length)
            return;

        slots[index].skillAsset = null;
        slots[index].supportAssets = null;
        slots[index].runtimeSkill = null;
    }

    public void AssignSkillToSlot(int index, SkillGemDefinition asset, int level = 1)
    {
        if (index < 0 || index >= slots.Length)
            return;

        slots[index].skillAsset = asset;
        slots[index].runtimeSkill = asset == null
            ? null
            : new SkillInstance { def = asset, level = level };
    }
}
