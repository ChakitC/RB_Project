using System.Collections.Generic;
using UnityEngine;

public abstract class SkillPayloadDef : ScriptableObject
{
    public virtual bool RequiresSkillTimelineEvents => false;

    public virtual void CollectTimelineEventNames(List<string> eventNames)
    {
    }

    public abstract void Execute(SkillCastContext context);
}
