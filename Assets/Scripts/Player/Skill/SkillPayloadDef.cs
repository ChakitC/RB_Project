using System.Collections.Generic;
using Animancer;
using UnityEngine;

public abstract class SkillPayloadDef : ScriptableObject
{
    public virtual bool RequiresSkillTimelineEvents => false;

    public virtual void CollectTimelineEventNames(List<StringReference> eventNames)
    {
    }

    public abstract void Execute(SkillCastContext context);
}
