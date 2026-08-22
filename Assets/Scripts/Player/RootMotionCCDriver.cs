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

    private System.Action<RootMotionPolicy> _onPolicyChanged;
    private CharacterAnimBrain _subscribedBrain;
    private RootMotionPolicy _policy;

    public bool ZeroY => zeroY;

    void Awake()
    {
        _onPolicyChanged ??= OnRootMotionPolicyChanged;

        ResolveReferences();
        ResolveCharacterCollisionLayers();

        if (animator)
            animator.applyRootMotion = false;

        SubscribeToBrain();

        // Re-subscribing to the same Brain is a no-op, so re-apply the policy explicitly or a
        // rebuilt model keeps the flag we just cleared while root motion is still running.
        OnRootMotionPolicyChanged(brain ? brain.RootMotion : RootMotionPolicy.Inactive);
    }

    void OnEnable()
    {
        SubscribeToBrain();
    }

    /// <summary>
    /// This driver owns <see cref="Animator.applyRootMotion"/> for its actor. The Brain only
    /// declares the policy; without a registered adapter it would write the flag itself.
    /// </summary>
    void SubscribeToBrain()
    {
        _onPolicyChanged ??= OnRootMotionPolicyChanged;

        if (_subscribedBrain == brain)
            return;

        UnsubscribeFromBrain();

        if (!brain)
            return;

        _subscribedBrain = brain;
        brain.RegisterRootMotionAdapter(_onPolicyChanged);
    }

    void UnsubscribeFromBrain()
    {
        if (!_subscribedBrain)
        {
            _subscribedBrain = null;
            return;
        }

        _subscribedBrain.UnregisterRootMotionAdapter(_onPolicyChanged);
        _subscribedBrain = null;
    }

    void OnRootMotionPolicyChanged(RootMotionPolicy policy)
    {
        _policy = policy;

        if (animator)
            animator.applyRootMotion = policy.Active;

        SyncCharacterCollisionPolicy();
    }

    public void Configure(CharacterAnimBrain animBrain, CharacterController characterController, Animator sourceAnimator, bool suppressY = false)
    {
        // Restore the previous CharacterController before rebinding, or its excludeLayers stay
        // overridden forever and the cached mask gets written back to the new one instead.
        RestoreCharacterCollision();

        brain = animBrain;
        cc = characterController;
        animator = sourceAnimator;
        zeroY = suppressY;
        ResolveReferences();
        ResolveCharacterCollisionLayers();

        if (animator)
            animator.applyRootMotion = false;

        SubscribeToBrain();

        // Re-subscribing to the same Brain is a no-op, so re-apply the policy explicitly or a
        // rebuilt model keeps the flag we just cleared while root motion is still running.
        OnRootMotionPolicyChanged(brain ? brain.RootMotion : RootMotionPolicy.Inactive);
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
        UnsubscribeFromBrain();
        RestoreCharacterCollision();
    }

    void OnDestroy()
    {
        UnsubscribeFromBrain();
        RestoreCharacterCollision();
    }

    void OnAnimatorMove()
    {
        if (!_policy.Active) return;
        if (!cc || !animator) return;

        SyncCharacterCollisionPolicy();

        Vector3 delta = RootMotionDeltaUtility.GetPositionDelta(
            animator,
            zeroY || _policy.PlanarOnly);

        cc.Move(delta);

        if (applyRootRotation || _policy.ApplyYaw)
        {
            float yawDelta = RootMotionDeltaUtility.GetYawDelta(animator);
            Transform actorRoot = cc.transform;
            actorRoot.rotation *= Quaternion.AngleAxis(yawDelta, Vector3.up);
        }
    }

    void SyncCharacterCollisionPolicy()
    {
        bool shouldIgnore =
            _policy.Active &&
            _policy.IgnoreCharacterCollision &&
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
