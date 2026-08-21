using System.Collections.Generic;
using UnityEngine;

public sealed class MorphSkillRuntime : MonoBehaviour
{
    private MorphSkillPayloadDef payload;
    private CharacterAnimBrain animBrain;
    private CharacterAnimDriver animDriver;
    private CharacterVisualController visual;
    private HealthSystem healthSystem;
    private StatusEffectController statusController;
    private GameObject statusSource;
    private readonly List<StatusEffectDef> appliedStatusDefs = new();
    private Transform casterRoot;
    private int requestId;

    private bool applyPending;
    private bool applied;
    private float revertAt;
    private bool shuttingDown;

    public void Initialize(SkillCastContext castContext, MorphSkillPayloadDef payloadDef)
    {
        payload = payloadDef;
        animBrain = castContext != null ? castContext.AnimBrain : null;
        requestId = castContext != null ? castContext.RequestId : 0;
        casterRoot = castContext != null ? castContext.CasterRoot : null;

        // The cast already resolved and bound the caster's hub once; reuse it.
        CharacteContext ctx = castContext != null ? castContext.CasterContext : null;
        if (ctx == null && casterRoot != null)
            ctx = CharacterContextModuleLookup.ResolveContext(casterRoot.gameObject);

        if (ctx != null)
        {
            ctx.ResolveReferences();
            visual = ctx.Visual;
            healthSystem = ctx.HealthSystem;
            animDriver = ctx.AnimDriver;
        }

        if (animBrain == null && ctx != null)
            animBrain = ctx.AnimBrain;

        // StatusEffectController ผ่าน context ก่อน แล้วค่อย fallback หา hierarchy — prefab บางตัววาง
        // controller ไว้บน child branch ไม่ใช่ root
        statusController = castContext != null && castContext.CasterStatusEffects != null
            ? castContext.CasterStatusEffects
            : CharacterContextModuleLookup.ResolveStatusEffects(
                casterRoot != null ? casterRoot.gameObject : null, ctx);
        statusSource = castContext != null ? castContext.CasterObject : null;

        if (payload == null ||
            (payload.ChangesModel && visual == null) ||
            (payload.ChangesAnimation && (animBrain == null || animDriver == null)) ||
            (payload.ChangesStatus && statusController == null))
        {
            Shutdown();
            return;
        }

        applyPending = true;

        if (animBrain != null)
            animBrain.SkillCastInterrupted += OnSkillCastInterrupted;

        if (healthSystem != null)
            healthSystem.CharacterDead += OnCharacterDead;
    }

    void Update()
    {
        if (shuttingDown)
            return;

        if (casterRoot == null)
        {
            Shutdown();
            return;
        }

        if (applyPending)
        {
            applyPending = false;
            ApplyMorph();
            revertAt = Time.time + Mathf.Max(0f, payload.Duration);
            return;
        }

        if (applied && Time.time >= revertAt)
            Shutdown();
    }

    void ApplyMorph()
    {
        if (payload == null)
            return;

        if (payload.ChangesModel && visual != null)
            visual.ApplyFormOverride(payload.MorphModelPrefab, payload.MorphController, payload.MorphAvatar);

        if (payload.ChangesAnimation && animDriver != null)
            animDriver.SetAnimProfileOverride(payload.MorphAnimProfile);

        if (payload.ChangesStatus && statusController != null)
        {
            var apps = payload.StatusApplications;
            for (int i = 0; i < apps.Count; i++)
            {
                StatusApplicationSpec spec = apps[i]?.spec;
                if (spec?.effect == null)
                    continue;

                // จำ Def จาก instance ที่ apply สำเร็จจริงเท่านั้น — ห้าม derive จากการแก้ Def
                StatusEffectInstance instance = statusController.ApplyEffect(spec, statusSource);
                if (instance?.Definition != null && !appliedStatusDefs.Contains(instance.Definition))
                    appliedStatusDefs.Add(instance.Definition);   // จำไว้ถอดตอน revert
            }
        }

        applied = true;
    }

    void RevertMorph()
    {
        if (!applied)
            return;

        if (payload != null && payload.ChangesAnimation && animDriver != null)
            animDriver.ClearAnimProfileOverride();

        if (payload != null && payload.ChangesModel && visual != null)
            visual.RestoreDefaultForm();

        if (statusController != null)
        {
            for (int i = 0; i < appliedStatusDefs.Count; i++)
                statusController.RemoveEffect(appliedStatusDefs[i]);   // RemoveEffect แมตช์ด้วย effectId
        }
        appliedStatusDefs.Clear();

        applied = false;
    }

    void OnSkillCastInterrupted(int interruptedRequestId)
    {
        if (interruptedRequestId != requestId)
            return;

        if (!applied)
            Shutdown();
    }

    void OnCharacterDead()
    {
        Shutdown();
    }

    void OnDisable()
    {
        Unsubscribe();
    }

    void OnDestroy()
    {
        Unsubscribe();
    }

    void Unsubscribe()
    {
        if (animBrain != null)
            animBrain.SkillCastInterrupted -= OnSkillCastInterrupted;

        if (healthSystem != null)
            healthSystem.CharacterDead -= OnCharacterDead;
    }

    void Shutdown()
    {
        if (shuttingDown)
            return;

        shuttingDown = true;
        RevertMorph();
        Unsubscribe();
        Destroy(gameObject);
    }
}
