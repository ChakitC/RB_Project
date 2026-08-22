using System;

public sealed partial class CharacterAnimBrain
{
    /// <summary>
    /// Root-motion ownership.
    ///
    /// The Brain <em>declares</em> what root motion should be doing; a registered adapter
    /// (<c>RootMotionCCDriver</c> for CharacterController actors, <c>RootMotionNavMeshDriver</c>
    /// for NavMesh actors) is what applies it — moving the transform, driving agent flags, and
    /// owning <see cref="UnityEngine.Animator.applyRootMotion"/>.
    ///
    /// When no adapter has registered, the Brain writes <c>applyRootMotion</c> itself so Unity's
    /// built-in root motion still moves the Animator's transform. That fallback is what characters
    /// with no adapter at all — summons, turrets, and any prefab without a
    /// <c>CharacterVisualController</c> to attach one — have always relied on.
    /// </summary>
    private RootMotionPolicy _rootMotion = RootMotionPolicy.Inactive;

    private int _rootMotionAdapterCount;

    /// <summary>The complete declared policy. Prefer this over the individual flags.</summary>
    public RootMotionPolicy RootMotion => _rootMotion;

    /// <summary>Raised whenever the declared policy changes, before any adapter has reacted.</summary>
    public event Action<RootMotionPolicy> RootMotionPolicyChanged;

    // Compatibility façade. Movement, formation, targeting, interruption, and the vertical motor
    // all read these; they stay for as long as those callers do.
    public bool RootMotionActive => _rootMotion.Active;
    public bool RootMotionPlanarOnly => _rootMotion.PlanarOnly;
    public bool RootMotionYawActive => _rootMotion.ApplyYaw;
    public bool RootMotionIgnoresCharacterCollision => _rootMotion.IgnoreCharacterCollision;

    /// <summary>True when an adapter owns <c>Animator.applyRootMotion</c>.</summary>
    private bool RootMotionIsAdapterOwned => _rootMotionAdapterCount > 0;

    /// <summary>
    /// Claims ownership of applying root motion. The caller immediately receives the current policy
    /// so an adapter that registers mid-playback is not left a frame behind.
    /// </summary>
    public void RegisterRootMotionAdapter(Action<RootMotionPolicy> onPolicyChanged)
    {
        if (onPolicyChanged == null)
            return;

        _rootMotionAdapterCount++;
        RootMotionPolicyChanged += onPolicyChanged;
        onPolicyChanged(_rootMotion);
    }

    public void UnregisterRootMotionAdapter(Action<RootMotionPolicy> onPolicyChanged)
    {
        if (onPolicyChanged == null)
            return;

        RootMotionPolicyChanged -= onPolicyChanged;

        if (_rootMotionAdapterCount > 0)
            _rootMotionAdapterCount--;

        // Ownership just came back to the Brain. The departing adapter may have left the Animator
        // flag anywhere, so re-assert the declared policy instead of inheriting its last write.
        WriteAnimatorRootMotionIfUnowned(_rootMotion.Active);
    }

    // ----- The single mutation point -----

    private void PublishRootMotionPolicy(RootMotionPolicy policy)
    {
        bool changed = _rootMotion != policy;
        _rootMotion = policy;

        // Written every time, not only on change. Without an adapter the Animator flag is the only
        // thing that actually applies root motion, and other code can move it out from under us —
        // a driver zeroing it in Awake, a preview tool, a rebuilt model. The declared policy has to
        // win. Subscribers only hear about real changes.
        WriteAnimatorRootMotionIfUnowned(policy.Active);

        if (changed)
            RootMotionPolicyChanged?.Invoke(policy);
    }

    private void SetRootMotionActive(bool active) =>
        PublishRootMotionPolicy(_rootMotion.WithActive(active));

    private void SetRootMotionShape(bool planarOnly, bool ignoreCharacterCollision) =>
        PublishRootMotionPolicy(_rootMotion.WithShape(planarOnly, ignoreCharacterCollision));

    private void ClearRootMotionPolicy() =>
        PublishRootMotionPolicy(RootMotionPolicy.Inactive);

    // ----- Adapterless fallback -----

    private bool AnimatorAppliesRootMotion =>
        animancer != null && animancer.Animator != null && animancer.Animator.applyRootMotion;

    private void WriteAnimatorRootMotionIfUnowned(bool applyRootMotion)
    {
        if (RootMotionIsAdapterOwned)
            return;

        if (animancer == null || animancer.Animator == null)
            return;

        animancer.Animator.applyRootMotion = applyRootMotion;
    }

    /// <summary>
    /// Hands <c>applyRootMotion</c> back to whatever had it before an exclusive state took over.
    /// Only meaningful without an adapter; with one, the policy itself is the truth.
    /// </summary>
    private void RestoreAnimatorRootMotionIfUnowned(bool previousApplyRootMotion) =>
        WriteAnimatorRootMotionIfUnowned(previousApplyRootMotion);
}
