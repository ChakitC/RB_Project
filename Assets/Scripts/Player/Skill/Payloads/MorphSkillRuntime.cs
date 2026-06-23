using UnityEngine;

public sealed class MorphSkillRuntime : MonoBehaviour
{
    private MorphSkillPayloadDef payload;
    private CharacterAnimBrain animBrain;
    private CharacterVisualController visual;
    private HealthSystem healthSystem;
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

        CharacteContext ctx = casterRoot != null
            ? casterRoot.GetComponentInParent<CharacteContext>()
            : null;

        if (ctx != null)
        {
            ctx.ResolveReferences();
            visual = ctx.Visual;
            healthSystem = ctx.HealthSystem;
        }

        if (animBrain == null && ctx != null)
            animBrain = ctx.AnimBrain;

        if (payload == null ||
            (payload.ChangesModel && visual == null) ||
            (payload.ChangesAnimation && animBrain == null))
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

        if (payload.ChangesAnimation && animBrain != null)
            animBrain.SetAnimProfileOverride(payload.MorphAnimProfile);

        applied = true;
    }

    void RevertMorph()
    {
        if (!applied)
            return;

        if (payload != null && payload.ChangesAnimation && animBrain != null)
            animBrain.ClearAnimProfileOverride();

        if (payload != null && payload.ChangesModel && visual != null)
            visual.RestoreDefaultForm();

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
