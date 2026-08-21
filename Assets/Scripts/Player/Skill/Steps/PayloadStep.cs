using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class PayloadStep : SkillEffectStep
{
    [SerializeField, HideInInspector] private SkillPayloadDef payload;

    public SkillPayloadDef Payload => payload;

    public void SetPayload(SkillPayloadDef value) => payload = value;

    public override void CollectUpgradeIds(List<string> ids)
    {
        base.CollectUpgradeIds(ids);
        payload?.CollectUpgradeIds(ids);
    }

    public override SkillExecutionResult ExecuteWithResult(SkillCastContext ctx)
    {
        if (payload == null)
        {
            return SkillExecutionResult.Failed(
                SkillExecutionFailureReason.MissingAuthoringData,
                "PayloadStep has no payload assigned.");
        }

        return payload.ExecuteWithResult(ctx);
    }
}
