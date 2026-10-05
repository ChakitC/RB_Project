using System;
using System.Collections.Generic;
using UnityEngine;

public enum PassiveRuleTrigger { Combat, Equipped, ResourceSpent, ResourceChanged, BeforeShot, ShotCommitted, BeforeDamage }
public enum PassiveConsumePolicy { OnAttempt, OnSuccess }
public enum PassiveOnceScope { None, Attack, Dash }

[Serializable]
public sealed class PassiveRecipeRule
{
    public string id = Guid.NewGuid().ToString("N");
    public PassiveRuleTrigger trigger;
    public PassiveEventType eventType;
    public PassiveOriginFilter originFilter = PassiveOriginFilter.ExternalOnly;
    public string eventSourceId;
    public bool requireTarget;
    public bool requireAttackId;
    public bool oncePerTargetPerChain = true;
    public PassiveOnceScope once;
    public PassiveConsumePolicy consume;
    public PassiveNumber count = new(1);
    public PassiveNumber window = new(0);
    public PassiveNumber cooldown = new(0);
    public PassiveNumber chance = new(1);
    public PassiveCounterConsumeMode counterConsumeMode;
    public PassiveEventSourceKind optionalSource;
    public float sourceDistance = 2f;
    public int sourceValue;
    public PassiveCondition condition = new();
    [SerializeReference] public List<PassiveStep> steps = new();
}
