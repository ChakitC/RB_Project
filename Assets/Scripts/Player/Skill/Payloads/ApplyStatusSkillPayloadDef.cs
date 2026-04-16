using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

[HideMonoScript]
[CreateAssetMenu(fileName = "Apply Status Skill Payload", menuName = "Game/Skill Payload/Apply Status")]
public sealed class ApplyStatusSkillPayloadDef : SkillPayloadDef
{
    [Serializable]
    public sealed class StatusApplication
    {
        [SerializeField, AssetsOnly, Required, InlineEditor]
        [LabelText("Status Effect")]
        public StatusEffectDef effect;

        [SerializeField, Min(1)]
        [LabelText("Stacks")]
        public int stacks = 1;
    }

    [PropertyOrder(-20)]
    [InfoBox("Applies one or more status effects directly to the caster through StatusEffectController.")]
    [SerializeField, BoxGroup("Setup")]
    [LabelText("Status Effects")]
    [ListDrawerSettings(DefaultExpandedState = true, DraggableItems = true, ShowFoldout = true)]
    private List<StatusApplication> applications = new();

    [SerializeField, HideInInspector] private StatusEffectDef effect;
    [SerializeField, HideInInspector, Min(1)] private int stacks = 1;

    [SerializeField, BoxGroup("Setup"), ToggleLeft]
    [LabelText("Prefer Caster Root")]
    private bool preferCasterRoot = true;

    public override void Execute(SkillCastContext context)
    {
        if (context == null)
        {
            return;
        }

        StatusEffectController controller = ResolveController(context);
        if (controller == null)
        {
            Debug.LogWarning(
                $"Skill '{context.SkillDef?.name ?? name}' could not find a {nameof(StatusEffectController)} on the caster.",
                this);
            return;
        }

        GameObject source = context.CasterObject != null
            ? context.CasterObject
            : controller.gameObject;

        bool appliedAny = false;

        if (applications != null && applications.Count > 0)
        {
            for (int i = 0; i < applications.Count; i++)
            {
                StatusApplication application = applications[i];
                if (application == null || application.effect == null)
                    continue;

                controller.ApplyEffect(application.effect, source, Mathf.Max(1, application.stacks));
                appliedAny = true;
            }
        }
        else if (effect != null)
        {
            controller.ApplyEffect(effect, source, Mathf.Max(1, stacks));
            appliedAny = true;
        }

        if (!appliedAny)
            Debug.LogError($"Skill payload '{name}' is missing its status effect configuration.", this);
    }

    StatusEffectController ResolveController(SkillCastContext context)
    {
        if (context == null)
            return null;

        if (preferCasterRoot && context.CasterRoot != null)
        {
            StatusEffectController rootController = FindController(context.CasterRoot.gameObject);
            if (rootController != null)
                return rootController;
        }

        if (context.CasterObject != null)
        {
            StatusEffectController casterController = FindController(context.CasterObject);
            if (casterController != null)
                return casterController;
        }

        if (context.CasterRoot != null)
            return FindController(context.CasterRoot.gameObject);

        return null;
    }

    void OnValidate()
    {
        if (effect == null)
            return;

        applications ??= new List<StatusApplication>();
        if (applications.Count > 0)
            return;

        applications.Add(new StatusApplication
        {
            effect = effect,
            stacks = Mathf.Max(1, stacks)
        });
    }

    static StatusEffectController FindController(GameObject target)
    {
        if (target == null)
            return null;

        StatusEffectController controller = target.GetComponent<StatusEffectController>();
        if (controller != null)
            return controller;

        controller = target.GetComponentInParent<StatusEffectController>();
        if (controller != null)
            return controller;

        return target.GetComponentInChildren<StatusEffectController>();
    }
}
