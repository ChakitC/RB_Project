using UnityEngine;

public class RootMotionCCDriver : MonoBehaviour
{
    [SerializeField] private CharacterAnimBrain brain;
    [SerializeField] private bool zeroY = false;
    [SerializeField] private bool applyRootRotation = false;
    [SerializeField] private LayerMask characterCollisionLayers;

    [SerializeField] private CharacterController cc;
    [SerializeField] private Animator animator;
    [SerializeField] private CharacteContext ctx;

    private int _collisionIgnoreToken;
    private bool _directCollisionOverrideActive;
    private LayerMask _cachedExcludeLayers;

    public bool ZeroY => zeroY;

    void Awake()
    {
        ResolveReferences();
        ResolveCharacterCollisionLayers();

        if (animator)
            animator.applyRootMotion = false;
    }

    public void Configure(CharacterAnimBrain animBrain, CharacterController characterController, Animator sourceAnimator, bool suppressY = false)
    {
        brain = animBrain;
        cc = characterController;
        animator = sourceAnimator;
        zeroY = suppressY;
        ResolveReferences();
        ResolveCharacterCollisionLayers();

        if (animator)
            animator.applyRootMotion = false;
    }

    void ResolveReferences()
    {
        if (!cc)
            cc = GetComponent<CharacterController>();
        if (!cc)
            cc = GetComponentInParent<CharacterController>();
        if (!cc)
            cc = GetComponentInChildren<CharacterController>(true);

        if (!animator)
            animator = GetComponent<Animator>();
        if (!animator)
            animator = GetComponentInParent<Animator>();
        if (!animator)
            animator = GetComponentInChildren<Animator>(true);

        if (!brain)
            brain = GetComponent<CharacterAnimBrain>();
        if (!brain)
            brain = GetComponentInParent<CharacterAnimBrain>();
        if (!brain)
            brain = GetComponentInChildren<CharacterAnimBrain>(true);

        if (!ctx)
            ctx = GetComponent<CharacteContext>();
        if (!ctx)
            ctx = GetComponentInParent<CharacteContext>();
        if (!ctx)
            ctx = GetComponentInChildren<CharacteContext>(true);
    }

    void ResolveCharacterCollisionLayers()
    {
        if (characterCollisionLayers == 0)
            characterCollisionLayers = LayerMask.GetMask("Player", "Enemy", "Ally");
    }

    void Update()
    {
        SyncCharacterCollisionPolicy();
    }

    void OnDisable()
    {
        RestoreCharacterCollision();
    }

    void OnDestroy()
    {
        RestoreCharacterCollision();
    }

    void OnAnimatorMove()
    {
        if (!brain || !brain.RootMotionActive) return;
        if (!cc || !animator) return;

        SyncCharacterCollisionPolicy();

        Vector3 delta = RootMotionDeltaUtility.GetPositionDelta(
            animator,
            zeroY || brain.RootMotionPlanarOnly);

        cc.Move(delta);

        if (applyRootRotation || brain.RootMotionYawActive)
        {
            float yawDelta = RootMotionDeltaUtility.GetYawDelta(animator);
            Transform actorRoot = cc.transform;
            actorRoot.rotation *= Quaternion.AngleAxis(yawDelta, Vector3.up);
        }
    }

    void SyncCharacterCollisionPolicy()
    {
        bool shouldIgnore =
            brain != null &&
            brain.RootMotionActive &&
            brain.RootMotionIgnoresCharacterCollision &&
            characterCollisionLayers != 0;

        if (shouldIgnore)
            ApplyCharacterCollisionIgnore();
        else
            RestoreCharacterCollision();
    }

    void ApplyCharacterCollisionIgnore()
    {
        if (_collisionIgnoreToken != 0 || _directCollisionOverrideActive)
            return;

        if (ctx != null && ctx.DashSystem != null)
        {
            int token = ctx.DashSystem.AcquireExternalCollisionIgnoreToken(characterCollisionLayers);
            if (token != 0)
            {
                _collisionIgnoreToken = token;
                return;
            }
        }

        if (cc == null)
            return;

        _cachedExcludeLayers = cc.excludeLayers;
        cc.excludeLayers = _cachedExcludeLayers | characterCollisionLayers;
        _directCollisionOverrideActive = true;
    }

    void RestoreCharacterCollision()
    {
        if (_collisionIgnoreToken != 0)
        {
            if (ctx != null && ctx.DashSystem != null)
                ctx.DashSystem.ReleaseExternalCollisionIgnoreToken(_collisionIgnoreToken);
            _collisionIgnoreToken = 0;
        }

        if (!_directCollisionOverrideActive)
            return;

        if (cc != null)
            cc.excludeLayers = _cachedExcludeLayers;

        _directCollisionOverrideActive = false;
    }
}
