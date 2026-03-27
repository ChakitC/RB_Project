using UnityEngine;

public abstract class SkillPayloadDef : ScriptableObject
{
    public abstract void Execute(SkillCastContext context);
}
