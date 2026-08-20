using UnityEngine;
using UnityEngine.AI;
using Opsive.BehaviorDesigner.Runtime;

/// <summary>
/// Captures one actor's control/physics/agent state for the duration of the stage intro,
/// warps it onto its marker, and restores the exact pose and component states afterwards.
/// Nothing here is player- or ally-specific beyond what <see cref="CharacteContext"/> exposes,
/// so the same scope drives the player, both party slots, and the helper.
/// </summary>
internal sealed class StageIntroActorScope
{
    const ControlBlockFlags StageIntroControlBlocks =
        ControlBlockFlags.Move |
        ControlBlockFlags.Shoot |
        ControlBlockFlags.Skill |
        ControlBlockFlags.Rotate;

    readonly CharacteContext ctx;
    readonly Transform actorTransform;

    // Set only for the Helper role: the helper is a summon and is hidden by default, so the intro has
    // to hold it on screen explicitly.
    readonly AllyHelperManager cinematicHelper;

    bool applied;
    bool cinematicHelperHeld;

    // Pose
    Vector3 savedPosition;
    Quaternion savedRotation;

    // Control
    int controlBlockToken;

    // CharacterController
    CharacterController characterController;
    bool savedCharacterControllerEnabled;
    bool characterControllerCaptured;

    // Rigidbody
    Rigidbody rigidBody;
    bool savedRigidbodyIsKinematic;
    bool savedRigidbodyUseGravity;
    bool rigidbodyCaptured;

    // Ally autonomy
    BehaviorTree behaviorTree;
    bool savedBehaviorTreeEnabled;
    bool behaviorTreeCaptured;

    AgentMoveDriver agentMoveDriver;
    bool savedAgentMoveDriverEnabled;
    bool agentMoveDriverCaptured;

    // Overhead bar (world-space, so SetHudVisible does not reach it)
    CharacterVisualController visual;
    bool savedOverheadBarVisible;
    bool overheadBarCaptured;

    NavMeshAgent agent;
    bool savedAgentEnabled;
    bool savedAgentIsStopped;
    bool savedAgentUpdatePosition;
    bool savedAgentUpdateRotation;
    bool savedAgentHadPath;
    Vector3 savedAgentDestination;
    bool agentCaptured;

    public StageIntroActorScope(CharacteContext context, AllyHelperManager cinematicHelper = null)
    {
        ctx = context;
        actorTransform = context != null ? context.transform : null;
        this.cinematicHelper = cinematicHelper;
    }

    public bool IsValid => ctx != null && actorTransform != null;

    public void Apply(Vector3 position, Quaternion rotation)
    {
        if (applied || !IsValid)
            return;

        applied = true;

        ctx.ResolveReferences();

        // Must run before CaptureAndStopAutonomy: activating the helper's GameObject would otherwise
        // re-enable the agent and behavior tree we just switched off.
        if (cinematicHelper != null)
        {
            cinematicHelper.BeginCinematicAppearance();
            cinematicHelperHeld = true;
        }

        savedPosition = actorTransform.position;
        savedRotation = actorTransform.rotation;

        CaptureAndStopAutonomy();
        CancelActiveIntent();

        controlBlockToken = ctx.stateHub != null
            ? ctx.stateHub.AcquireExternalControlBlockToken(StageIntroControlBlocks)
            : 0;

        ctx.AnimDriver?.InterruptActivePlaybackForExternalControlLoss();

        Teleport(position, rotation);
    }

    /// <summary>
    /// Starts the intro pose. Kept separate from <see cref="Apply"/> so the rig can lock and place the
    /// actors while the screen is still black, then begin the performance on the same frame the fade
    /// starts — otherwise the intro is already partway through by the time anyone can see it.
    /// </summary>
    public void BeginIntroPose()
    {
        if (!applied || !IsValid)
            return;

        ctx.AnimDriver?.TryPlayStageIntro();
    }

    public void Restore()
    {
        if (!applied)
            return;

        applied = false;

        if (!IsValid)
            return;

        ctx.AnimDriver?.StopStageIntro();

        // Pose first, then re-seat the agent on the NavMesh, then hand components back their state.
        Teleport(savedPosition, savedRotation);

        RestoreAgent();

        if (characterControllerCaptured && characterController != null)
            characterController.enabled = savedCharacterControllerEnabled;
        characterControllerCaptured = false;

        if (rigidbodyCaptured && rigidBody != null)
        {
            rigidBody.linearVelocity = Vector3.zero;
            rigidBody.angularVelocity = Vector3.zero;
            rigidBody.useGravity = savedRigidbodyUseGravity;
            rigidBody.isKinematic = savedRigidbodyIsKinematic;
        }
        rigidbodyCaptured = false;

        if (agentMoveDriverCaptured && agentMoveDriver != null)
            agentMoveDriver.enabled = savedAgentMoveDriverEnabled;
        agentMoveDriverCaptured = false;

        if (behaviorTreeCaptured && behaviorTree != null)
            behaviorTree.enabled = savedBehaviorTreeEnabled;
        behaviorTreeCaptured = false;

        if (overheadBarCaptured && visual != null)
            visual.SetOverheadBarVisible(savedOverheadBarVisible);
        overheadBarCaptured = false;

        CancelActiveIntent();

        if (controlBlockToken != 0)
        {
            ctx.stateHub?.ReleaseExternalControlBlockToken(controlBlockToken);
            controlBlockToken = 0;
        }

        // Last: this deactivates the helper again, so everything above must already be restored.
        if (cinematicHelperHeld && cinematicHelper != null)
            cinematicHelper.EndCinematicAppearance();
        cinematicHelperHeld = false;
    }

    void CaptureAndStopAutonomy()
    {
        var ally = ctx as AllyContext;

        behaviorTree = ally != null ? ally.BehaviorTree : null;
        if (behaviorTree == null)
            behaviorTree = ctx.GetComponentInChildren<BehaviorTree>(true);
        if (behaviorTree != null)
        {
            savedBehaviorTreeEnabled = behaviorTree.enabled;
            behaviorTreeCaptured = true;
            behaviorTree.enabled = false;
        }

        agentMoveDriver = ally != null ? ally.AgentMoveDriver : null;
        if (agentMoveDriver == null)
            agentMoveDriver = ctx.GetComponentInChildren<AgentMoveDriver>(true);
        if (agentMoveDriver != null)
        {
            savedAgentMoveDriverEnabled = agentMoveDriver.enabled;
            agentMoveDriverCaptured = true;
            agentMoveDriver.enabled = false;
        }

        agent = ally != null ? ally.agent : null;
        if (agent == null)
            agent = ctx.GetComponentInChildren<NavMeshAgent>(true);
        if (agent != null)
        {
            savedAgentEnabled = agent.enabled;
            savedAgentUpdatePosition = agent.updatePosition;
            savedAgentUpdateRotation = agent.updateRotation;
            savedAgentIsStopped = agent.enabled && agent.isOnNavMesh && agent.isStopped;
            savedAgentHadPath = agent.enabled && agent.isOnNavMesh && (agent.hasPath || agent.pathPending);
            savedAgentDestination = savedAgentHadPath ? agent.destination : actorTransform.position;
            agentCaptured = true;

            if (agent.enabled && agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.ResetPath();
            }

            agent.updatePosition = false;
            agent.updateRotation = false;
            agent.enabled = false;
        }

        visual = ctx.Visual != null ? ctx.Visual : ctx.GetComponentInChildren<CharacterVisualController>(true);
        if (visual != null)
        {
            savedOverheadBarVisible = visual.SetOverheadBarVisible(false);
            overheadBarCaptured = true;
        }

        characterController = ctx.cc != null ? ctx.cc : ctx.GetComponentInChildren<CharacterController>(true);
        if (characterController != null)
        {
            savedCharacterControllerEnabled = characterController.enabled;
            characterControllerCaptured = true;
            characterController.enabled = false;
        }

        rigidBody = ctx.rb != null ? ctx.rb : ctx.GetComponentInChildren<Rigidbody>(true);
        if (rigidBody != null)
        {
            savedRigidbodyIsKinematic = rigidBody.isKinematic;
            savedRigidbodyUseGravity = rigidBody.useGravity;
            rigidbodyCaptured = true;

            rigidBody.linearVelocity = Vector3.zero;
            rigidBody.angularVelocity = Vector3.zero;
            rigidBody.useGravity = false;
            rigidBody.isKinematic = true;
        }
    }

    void RestoreAgent()
    {
        if (!agentCaptured || agent == null)
        {
            agentCaptured = false;
            return;
        }

        agentCaptured = false;

        if (!savedAgentEnabled)
        {
            agent.updatePosition = savedAgentUpdatePosition;
            agent.updateRotation = savedAgentUpdateRotation;
            return;
        }

        agent.enabled = true;

        if (NavMesh.SamplePosition(actorTransform.position, out NavMeshHit hit, 3f, NavMesh.AllAreas))
            agent.Warp(hit.position);
        else if (agent.isOnNavMesh)
            agent.nextPosition = actorTransform.position;

        agent.updatePosition = savedAgentUpdatePosition;
        agent.updateRotation = savedAgentUpdateRotation;

        if (agent.isOnNavMesh)
        {
            agent.isStopped = savedAgentIsStopped;

            if (savedAgentHadPath && !savedAgentIsStopped)
                agent.SetDestination(savedAgentDestination);
        }
    }

    void CancelActiveIntent()
    {
        ctx.moveInput = Vector2.zero;
        ctx.lookInput = Vector2.zero;
        ctx.WeaponSystem?.SetFiring(false);
        ctx.stateHub?.RequestCanceledFire();
        ctx.WeaponSystem?.OnAim(false);

        if (ctx.DashSystem != null && ctx.DashSystem.IsDashing)
            ctx.DashSystem.CancelDash();
    }

    void Teleport(Vector3 position, Quaternion rotation)
    {
        actorTransform.SetPositionAndRotation(position, rotation);
        Physics.SyncTransforms();
    }
}
