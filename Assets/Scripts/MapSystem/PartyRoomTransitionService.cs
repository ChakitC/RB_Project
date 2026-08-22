using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Places the party inside a room and can put it back where it was. Everything about who the party
/// is comes from <see cref="PartyRuntime"/>: its <see cref="PartyRuntime.Root"/> is the warp root
/// and its <see cref="PartyRuntime.Actors"/> are the members to move, so no scene-wide search or
/// name guessing is involved.
/// </summary>
public sealed class PartyRoomTransitionService
{
    /// <summary>A transform and the world pose it held, so a failed transition can undo the warp.</summary>
    public readonly struct TransformPose
    {
        public readonly Transform Transform;
        public readonly Vector3 Position;
        public readonly Quaternion Rotation;

        public TransformPose(Transform transform)
        {
            Transform = transform;
            Position = transform != null ? transform.position : Vector3.zero;
            Rotation = transform != null ? transform.rotation : Quaternion.identity;
        }
    }

    public sealed class PartyPoseSnapshot
    {
        public TransformPose RootPose;
        public readonly List<TransformPose> ActorPoses = new();
    }

    private const float CompanionWarpForwardOffset = 1.5f;
    private const float CompanionWarpLateralOffset = 0.9f;
    private const float CompanionWarpRowSpacing = 1.25f;
    private static readonly Collider[] WarpCollisionHits = new Collider[32];

    private readonly MonoBehaviour owner;
    private PartyWarpSettings settings;
    private CharacteContext explicitPlayerContext;
    private PartySpawnPoint partySpawnPoint;
    private bool warnedMissingWarpCollisionLayer;

    public PartyRoomTransitionService(MonoBehaviour owner)
    {
        this.owner = owner;
    }

    public void Configure(PartyWarpSettings warpSettings, CharacteContext playerContextOverride)
    {
        settings = warpSettings;
        explicitPlayerContext = playerContextOverride;
    }

    public PartyRuntime ResolveParty()
    {
        if (partySpawnPoint == null)
            partySpawnPoint = UnityEngine.Object.FindFirstObjectByType<PartySpawnPoint>();

        return partySpawnPoint != null ? partySpawnPoint.CurrentParty : null;
    }

    /// <summary>
    /// The player the room warp is anchored on: the explicit override first, then the party's own
    /// player actor. Null means there is no party in the scene yet, which callers treat as
    /// "nothing to move".
    /// </summary>
    public CharacteContext ResolvePlayerContext()
    {
        if (explicitPlayerContext != null)
            return explicitPlayerContext;

        PartyRuntime party = ResolveParty();
        return party != null ? party.Player : null;
    }

    /// <summary>The transform every party member hangs under, or the player itself when unavailable.</summary>
    public Transform ResolvePartyRoot(CharacteContext player)
    {
        PartyRuntime party = ResolveParty();
        if (party != null && party.Root != null)
            return party.Root.transform;

        return player != null ? player.transform : null;
    }

    public PartyPoseSnapshot CapturePartyPose()
    {
        var snapshot = new PartyPoseSnapshot();
        CharacteContext player = ResolvePlayerContext();
        if (player == null)
            return snapshot;

        Transform partyRoot = ResolvePartyRoot(player);
        snapshot.RootPose = new TransformPose(partyRoot != null ? partyRoot : player.transform);

        PartyRuntime party = ResolveParty();
        if (party == null)
        {
            snapshot.ActorPoses.Add(new TransformPose(player.transform));
            return snapshot;
        }

        for (int i = 0; i < party.Actors.Count; i++)
        {
            CharacteContext context = party.Actors[i]?.Context;
            if (context != null && IsPartyMember(context, player))
                snapshot.ActorPoses.Add(new TransformPose(context.transform));
        }

        return snapshot;
    }

    public void RestorePartyPose(PartyPoseSnapshot snapshot)
    {
        if (snapshot == null)
            return;

        Transform root = snapshot.RootPose.Transform;
        if (root != null)
        {
            CharacterController[] controllers = root.GetComponentsInChildren<CharacterController>(true);
            bool[] controllerStates = DisableControllers(controllers);
            root.SetPositionAndRotation(snapshot.RootPose.Position, snapshot.RootPose.Rotation);
            RestoreControllers(controllers, controllerStates);
            SyncWarpedAgentsUnderRoot(root);
        }

        for (int i = 0; i < snapshot.ActorPoses.Count; i++)
            RestoreActorPose(snapshot.ActorPoses[i]);

        Physics.SyncTransforms();
    }

    public bool MovePartyToRoomSpawn(RoomController room, RoomExitDirection? entranceDirection)
    {
        if (room == null)
            return false;

        CharacteContext player = ResolvePlayerContext();
        if (player == null)
            return true;

        Transform spawn = room.GetPlayerSpawnPoint(entranceDirection);
        Transform partyRoot = ResolvePartyRoot(player);
        if (partyRoot != null && partyRoot != player.transform)
        {
            if (!WarpPartyRootToPlayerSpawn(player, partyRoot, spawn.position, spawn.rotation))
                return false;
        }
        else
        {
            if (!WarpCharacter(player, spawn.position, spawn.rotation))
                return false;
        }

        PartyRuntime party = ResolveParty();
        if (party == null)
            return true;

        int companionIndex = 0;
        for (int i = 0; i < party.Actors.Count; i++)
        {
            CharacteContext ctx = party.Actors[i]?.Context;
            if (ctx == null ||
                ctx == player ||
                !ctx.ParticipatesInPartyRuntime ||
                ctx.TargetIdentity != AITargetIdentity.Companion ||
                !ctx.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (!WarpCompanionToRoomSpawn(ctx, player.transform, companionIndex))
                return false;

            companionIndex++;
        }

        return true;
    }

    public bool TryWarpTransientActor(
        CharacteContext actor,
        CharacteContext anchor,
        int formationIndex,
        out Vector3 resolvedPosition)
    {
        resolvedPosition = Vector3.zero;
        if (actor == null || anchor == null || actor == anchor)
            return false;

        int row = Mathf.Max(0, formationIndex) / 2;
        float side = Mathf.Max(0, formationIndex) % 2 == 0 ? -1f : 1f;
        Vector3 position = anchor.transform.position +
                           anchor.transform.forward * (CompanionWarpForwardOffset + row * CompanionWarpRowSpacing) +
                           anchor.transform.right * (CompanionWarpLateralOffset * side);

        bool requireActiveAgent = actor is SummonContext summonContext &&
                                  summonContext.Mobility == SummonMobility.Mobile;
        if (!WarpCharacter(actor, position, anchor.transform.rotation, requireActiveAgent))
            return false;

        resolvedPosition = actor.transform.position;
        return true;
    }

    static bool IsPartyMember(CharacteContext context, CharacteContext player)
    {
        if (context == player)
            return true;

        AITargetIdentity identity = context.TargetIdentity;
        return context.ParticipatesInPartyRuntime &&
               (identity == AITargetIdentity.Player || identity == AITargetIdentity.Companion);
    }

    void RestoreActorPose(TransformPose pose)
    {
        Transform actor = pose.Transform;
        if (actor == null)
            return;

        CharacterController controller = actor.GetComponent<CharacterController>();
        if (controller == null)
            controller = actor.GetComponentInChildren<CharacterController>(true);

        bool controllerWasEnabled = controller != null && controller.enabled;
        if (controllerWasEnabled)
            controller.enabled = false;

        NavMeshAgent agent = actor.GetComponent<NavMeshAgent>();
        if (agent == null)
            agent = actor.GetComponentInChildren<NavMeshAgent>(true);

        if (agent != null && agent.isActiveAndEnabled)
        {
            if (!agent.Warp(pose.Position))
                actor.position = pose.Position;
            else
                SyncWarpedAgent(agent);
        }
        else
        {
            actor.position = pose.Position;
        }

        actor.rotation = pose.Rotation;
        if (controllerWasEnabled)
            controller.enabled = true;
    }

    bool WarpPartyRootToPlayerSpawn(
        CharacteContext player,
        Transform partyRoot,
        Vector3 position,
        Quaternion rotation)
    {
        if (player == null || partyRoot == null)
            return false;

        player.ResolveReferences();

        NavMeshAgent playerAgent = player.GetComponent<NavMeshAgent>();
        if (playerAgent == null)
            playerAgent = player.GetComponentInChildren<NavMeshAgent>(true);

        if (!TryResolveSafeWarpPosition(player, playerAgent, position, rotation, out Vector3 safePosition, out Collider blocker))
        {
            WarnWarpBlocked(player, position, blocker);
            Physics.SyncTransforms();
            return false;
        }

        CharacterController[] controllers = partyRoot.GetComponentsInChildren<CharacterController>(true);
        bool[] controllerEnabledStates = DisableControllers(controllers);

        try
        {
            SetRootPoseForChildPose(partyRoot, player.transform, safePosition, rotation);
            SyncWarpedAgentsUnderRoot(partyRoot);
        }
        finally
        {
            RestoreControllers(controllers, controllerEnabledStates);
            Physics.SyncTransforms();
        }

        return true;
    }

    bool WarpCompanionToRoomSpawn(CharacteContext companion, Transform player, int companionIndex)
    {
        if (companion == null || player == null)
            return false;

        int row = companionIndex / 2;
        float side = companionIndex % 2 == 0 ? -1f : 1f;
        Vector3 formationPosition =
            player.position +
            player.forward * (CompanionWarpForwardOffset + row * CompanionWarpRowSpacing) +
            player.right * (CompanionWarpLateralOffset * side);

        if (WarpCharacter(companion, formationPosition, player.rotation, requireActiveAgentNavMesh: true))
            return true;

        Vector3 centerFallback =
            player.position +
            player.forward * (CompanionWarpForwardOffset + companionIndex * CompanionWarpRowSpacing);

        if (WarpCharacter(companion, centerFallback, player.rotation, requireActiveAgentNavMesh: true))
        {
            Debug.LogWarning(
                $"[MapRunController] Companion '{companion.name}' used the center room-entry fallback at {centerFallback}.",
                owner);
            return true;
        }

        Debug.LogError(
            $"[MapRunController] Companion '{companion.name}' could not be placed on the new room NavMesh.",
            owner);
        return false;
    }

    static void SetRootPoseForChildPose(
        Transform root,
        Transform child,
        Vector3 childWorldPosition,
        Quaternion childWorldRotation)
    {
        if (root == null || child == null)
            return;

        Quaternion childLocalRotationToRoot = Quaternion.Inverse(root.rotation) * child.rotation;
        root.rotation = childWorldRotation * Quaternion.Inverse(childLocalRotationToRoot);
        root.position += childWorldPosition - child.position;
    }

    static bool[] DisableControllers(CharacterController[] controllers)
    {
        if (controllers == null)
            return Array.Empty<bool>();

        bool[] enabledStates = new bool[controllers.Length];
        for (int i = 0; i < controllers.Length; i++)
        {
            CharacterController controller = controllers[i];
            if (controller == null)
                continue;

            enabledStates[i] = controller.enabled;
            if (controller.enabled)
                controller.enabled = false;
        }

        return enabledStates;
    }

    static void RestoreControllers(CharacterController[] controllers, bool[] enabledStates)
    {
        if (controllers == null || enabledStates == null)
            return;

        int count = Mathf.Min(controllers.Length, enabledStates.Length);
        for (int i = 0; i < count; i++)
        {
            if (controllers[i] != null)
                controllers[i].enabled = enabledStates[i];
        }
    }

    void SyncWarpedAgentsUnderRoot(Transform root)
    {
        if (root == null)
            return;

        NavMeshAgent[] agents = root.GetComponentsInChildren<NavMeshAgent>(true);
        for (int i = 0; i < agents.Length; i++)
        {
            NavMeshAgent agent = agents[i];
            if (agent != null && agent.enabled)
                TryWarpAgent(agent, agent.transform.position);
        }
    }

    bool WarpCharacter(
        CharacteContext ctx,
        Vector3 position,
        Quaternion rotation,
        bool requireActiveAgentNavMesh = false)
    {
        if (ctx == null)
            return false;

        ctx.ResolveReferences();

        CharacterController controller = ctx.cc;
        bool controllerWasEnabled = controller != null && controller.enabled;
        if (controllerWasEnabled)
            controller.enabled = false;

        NavMeshAgent agent = ctx.GetComponent<NavMeshAgent>();
        if (agent == null)
            agent = ctx.GetComponentInChildren<NavMeshAgent>(true);

        bool hasActiveAgent = agent != null && agent.isActiveAndEnabled;
        if (requireActiveAgentNavMesh && !hasActiveAgent)
        {
            if (controllerWasEnabled)
                controller.enabled = true;

            Debug.LogWarning(
                $"[MapRunController] Warp for '{ctx.name}' requires an active NavMeshAgent, but none was available.",
                owner);
            return false;
        }

        if (!TryResolveSafeWarpPosition(ctx, agent, position, rotation, out Vector3 safePosition, out Collider blocker))
        {
            if (controllerWasEnabled)
                controller.enabled = true;

            WarnWarpBlocked(ctx, position, blocker);
            Physics.SyncTransforms();
            return false;
        }

        if (hasActiveAgent)
        {
            if (!TryWarpAgent(agent, safePosition))
            {
                if (controllerWasEnabled)
                    controller.enabled = true;

                Debug.LogWarning(
                    $"[MapRunController] NavMesh warp for '{ctx.name}' failed near {safePosition}.",
                    owner);
                Physics.SyncTransforms();
                return false;
            }
        }
        else
        {
            ctx.transform.position = safePosition;
        }

        ctx.transform.rotation = rotation;

        if (controllerWasEnabled)
            controller.enabled = true;

        Physics.SyncTransforms();
        return true;
    }

    bool TryWarpAgent(NavMeshAgent agent, Vector3 position, bool allowNavMeshSample = true)
    {
        if (agent == null || !agent.isActiveAndEnabled)
            return false;

        if (agent.Warp(position))
        {
            SyncWarpedAgent(agent);
            return true;
        }

        if (!allowNavMeshSample)
            return false;

        if (TrySampleAgentNavMeshPosition(agent, position, out Vector3 navMeshPosition) &&
            agent.Warp(navMeshPosition))
        {
            SyncWarpedAgent(agent);
            return true;
        }

        return false;
    }

    bool TryResolveSafeWarpPosition(
        CharacteContext ctx,
        NavMeshAgent agent,
        Vector3 position,
        Quaternion rotation,
        out Vector3 safePosition,
        out Collider blocker)
    {
        safePosition = position;
        blocker = null;
        Physics.SyncTransforms();

        if (TryResolveSafeWarpCandidate(ctx, agent, position, rotation, out safePosition, out blocker))
            return true;

        float searchRadius = Mathf.Max(settings.SafeSearchRadius, settings.NavMeshSampleRadius, 0f);
        if (searchRadius <= 0f)
            return false;

        int steps = Mathf.Max(4, settings.SafeSearchSteps);
        const int ringCount = 3;
        for (int ring = 1; ring <= ringCount; ring++)
        {
            float radius = searchRadius * ring / ringCount;
            float angleOffset = ring % 2 == 0 ? Mathf.PI / steps : 0f;

            for (int i = 0; i < steps; i++)
            {
                float angle = angleOffset + Mathf.PI * 2f * i / steps;
                Vector3 candidate = position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                if (TryResolveSafeWarpCandidate(ctx, agent, candidate, rotation, out safePosition, out blocker))
                    return true;
            }
        }

        return false;
    }

    bool TryResolveSafeWarpCandidate(
        CharacteContext ctx,
        NavMeshAgent agent,
        Vector3 candidate,
        Quaternion rotation,
        out Vector3 safePosition,
        out Collider blocker)
    {
        safePosition = candidate;
        blocker = null;

        if (agent != null && agent.enabled &&
            TrySampleAgentNavMeshPosition(agent, candidate, out Vector3 navMeshPosition))
        {
            if (IsWarpPositionClear(ctx, agent, navMeshPosition, rotation, out blocker))
            {
                safePosition = navMeshPosition;
                return true;
            }
        }

        if (IsWarpPositionClear(ctx, agent, candidate, rotation, out blocker))
        {
            safePosition = candidate;
            return true;
        }

        return false;
    }

    bool IsWarpPositionClear(
        CharacteContext ctx,
        NavMeshAgent agent,
        Vector3 position,
        Quaternion rotation,
        out Collider blocker)
    {
        blocker = null;

        int collisionMask = ResolveWarpCollisionMask();
        if (collisionMask == 0)
            return true;

        GetWarpProbeCapsule(ctx, agent, position, rotation, out Vector3 pointA, out Vector3 pointB, out float radius);
        int hitCount = Physics.OverlapCapsuleNonAlloc(
            pointA,
            pointB,
            radius,
            WarpCollisionHits,
            collisionMask,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = WarpCollisionHits[i];
            WarpCollisionHits[i] = null;

            if (hit == null || IsOwnCollider(ctx, hit))
                continue;

            blocker = hit;
            return false;
        }

        return true;
    }

    void GetWarpProbeCapsule(
        CharacteContext ctx,
        NavMeshAgent agent,
        Vector3 position,
        Quaternion rotation,
        out Vector3 pointA,
        out Vector3 pointB,
        out float radius)
    {
        Vector3 up = rotation * Vector3.up;
        float height;
        Vector3 center;

        CharacterController controller = ctx != null ? ctx.cc : null;
        if (controller != null)
        {
            Vector3 scale = controller.transform.lossyScale;
            Vector3 scaledCenter = Vector3.Scale(controller.center, Abs(scale));
            center = position + rotation * scaledCenter;
            radius = Mathf.Max(0.05f, controller.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z)) + settings.CollisionPadding);
            height = Mathf.Max(controller.height * Mathf.Abs(scale.y), radius * 2f);
        }
        else if (agent != null)
        {
            radius = Mathf.Max(0.05f, agent.radius + settings.CollisionPadding);
            height = Mathf.Max(agent.height, radius * 2f);
            center = position + up * (height * 0.5f);
        }
        else
        {
            radius = Mathf.Max(0.05f, 0.35f + settings.CollisionPadding);
            height = Mathf.Max(1.8f, radius * 2f);
            center = position + up * (height * 0.5f);
        }

        float halfLine = Mathf.Max(0f, height * 0.5f - radius);
        float groundClearance = Mathf.Min(Mathf.Max(0f, settings.GroundClearance), height * 0.25f);
        pointA = center + up * halfLine;
        pointB = center - up * halfLine + up * groundClearance;
    }

    int ResolveWarpCollisionMask()
    {
        if (settings.CollisionMask.value != 0)
            return settings.CollisionMask.value;

        int terrainLayer = LayerMask.NameToLayer(PartyWarpSettings.DefaultCollisionLayerName);
        if (terrainLayer >= 0)
            return 1 << terrainLayer;

        if (!warnedMissingWarpCollisionLayer)
        {
            warnedMissingWarpCollisionLayer = true;
            Debug.LogWarning(
                $"[MapRunController] Layer '{PartyWarpSettings.DefaultCollisionLayerName}' was not found. Warp collision checks are disabled.",
                owner);
        }

        return 0;
    }

    static Vector3 Abs(Vector3 value)
    {
        return new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
    }

    static bool IsOwnCollider(CharacteContext ctx, Collider hit)
    {
        return ctx != null &&
               hit != null &&
               (hit.transform == ctx.transform || hit.transform.IsChildOf(ctx.transform));
    }

    void WarnWarpBlocked(CharacteContext ctx, Vector3 position, Collider blocker)
    {
        string actorName = ctx != null ? ctx.name : "Unknown";
        string blockerName = blocker != null
            ? $"{blocker.name} ({LayerMask.LayerToName(blocker.gameObject.layer)})"
            : "unknown Terrain collider";
        Debug.LogWarning($"[MapRunController] Warp for '{actorName}' was blocked near {position} by {blockerName}.", owner);
    }

    bool TrySampleAgentNavMeshPosition(NavMeshAgent agent, Vector3 position, out Vector3 navMeshPosition)
    {
        navMeshPosition = position;
        if (agent == null)
            return false;

        float sampleRadius = Mathf.Max(settings.NavMeshSampleRadius, agent.radius * 2f, 0.25f);
        int areaMask = agent.areaMask;
        if (NavMesh.SamplePosition(position, out NavMeshHit hit, sampleRadius, areaMask))
        {
            navMeshPosition = hit.position;
            return true;
        }

        if (areaMask != NavMesh.AllAreas &&
            NavMesh.SamplePosition(position, out hit, sampleRadius, NavMesh.AllAreas))
        {
            navMeshPosition = hit.position;
            return true;
        }

        return false;
    }

    static void SyncWarpedAgent(NavMeshAgent agent)
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
            return;

        agent.ResetPath();
        agent.nextPosition = agent.transform.position;
    }
}
