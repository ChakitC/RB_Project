using UnityEngine;

public sealed class CharacterPlacementRequest
{
    public readonly struct Candidate
    {
        public Candidate(
            Vector3 position,
            Quaternion rotation,
            float preferredAngleError,
            int authoredOrder,
            Vector3? desiredImpactPosition = null)
        {
            Position = position;
            Rotation = rotation == default ? Quaternion.identity : rotation;
            PreferredAngleError = Mathf.Max(0f, preferredAngleError);
            AuthoredOrder = authoredOrder;
            DesiredImpactPosition = desiredImpactPosition;
        }

        public Vector3 Position { get; }
        public Quaternion Rotation { get; }
        public float PreferredAngleError { get; }
        public int AuthoredOrder { get; }
        public Vector3? DesiredImpactPosition { get; }
    }

    public readonly struct AnchorSnapshot
    {
        public AnchorSnapshot(
            Vector3 position,
            Quaternion rotation,
            AITargetIdentity targetIdentity)
        {
            Position = position;
            Rotation = rotation == default ? Quaternion.identity : rotation;
            TargetIdentity = targetIdentity;
        }

        public static AnchorSnapshot Capture(
            Transform anchor,
            AITargetIdentity targetIdentity = AITargetIdentity.Generic)
        {
            return new AnchorSnapshot(
                anchor != null ? anchor.position : Vector3.zero,
                anchor != null ? anchor.rotation : Quaternion.identity,
                targetIdentity);
        }

        public Vector3 Position { get; }
        public Quaternion Rotation { get; }
        public AITargetIdentity TargetIdentity { get; }
    }

    public CharacterPlacementRequest(
        Transform actorRoot,
        Collider positionCollider,
        CharacterPlacementFootprint footprint,
        AITargetIdentity targetIdentity,
        Transform targetRoot,
        AnchorSnapshot targetAnchor,
        Candidate[] candidates,
        CharacterPlacementAnimationInput animation,
        float impactNormalizedTime,
        CharacterPlacementPolicyDef policy,
        LayerMask worldCollisionLayers,
        LayerMask actorCollisionLayers,
        Transform ignoreRoot,
        Object reservationOwner,
        bool effectivePlanarRootMotion,
        bool animationRequired,
        bool mobileActor,
        CharacterPlacementRuntimePolicy runtimePolicy = default,
        System.Func<Vector3, Quaternion, bool> poseValidator = null,
        Collider ignoredCollider = null,
        System.Collections.Generic.IReadOnlyList<CharacterPlacementReservationService.StaticReservation>
            additionalReservations = null,
        bool transientReservation = false)
    {
        ActorRoot = actorRoot;
        PositionCollider = positionCollider;
        Footprint = footprint;
        TargetIdentity = targetIdentity;
        TargetRoot = targetRoot;
        TargetAnchor = targetAnchor;
        Candidates = candidates;
        Animation = animation;
        ImpactNormalizedTime = Mathf.Clamp01(impactNormalizedTime);
        Policy = policy;
        WorldCollisionLayers = worldCollisionLayers;
        ActorCollisionLayers = actorCollisionLayers;
        IgnoreRoot = ignoreRoot;
        ReservationOwner = reservationOwner;
        EffectivePlanarRootMotion = effectivePlanarRootMotion;
        AnimationRequired = animationRequired;
        MobileActor = mobileActor;
        RuntimePolicy = runtimePolicy;
        PoseValidator = poseValidator;
        IgnoredCollider = ignoredCollider;
        AdditionalReservations = additionalReservations;
        TransientReservation = transientReservation;
    }

    public Transform ActorRoot { get; }
    public Collider PositionCollider { get; }
    public CharacterPlacementFootprint Footprint { get; }
    public AITargetIdentity TargetIdentity { get; }
    public Transform TargetRoot { get; }
    public AnchorSnapshot TargetAnchor { get; }
    public Candidate[] Candidates { get; }
    public CharacterPlacementAnimationInput Animation { get; }
    public float ImpactNormalizedTime { get; }
    public CharacterPlacementPolicyDef Policy { get; }
    public LayerMask WorldCollisionLayers { get; }
    public LayerMask ActorCollisionLayers { get; }
    public Transform IgnoreRoot { get; }
    public Object ReservationOwner { get; }
    public bool EffectivePlanarRootMotion { get; }
    public bool AnimationRequired { get; }
    public bool MobileActor { get; }
    public CharacterPlacementRuntimePolicy RuntimePolicy { get; }
    public System.Func<Vector3, Quaternion, bool> PoseValidator { get; }
    public Collider IgnoredCollider { get; }
    public System.Collections.Generic.IReadOnlyList<CharacterPlacementReservationService.StaticReservation>
        AdditionalReservations { get; }
    public bool TransientReservation { get; }
}
