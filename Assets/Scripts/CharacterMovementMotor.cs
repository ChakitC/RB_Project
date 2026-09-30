using UnityEngine;
using UnityEngine.AI;

/// <summary>Applies collision-constrained displacement. Gameplay timing stays with the caller.</summary>
[DisallowMultipleComponent]
public sealed class CharacterMovementMotor : MonoBehaviour
{
    private CharacteContext ctx;
    private Transform boundRoot;
    private CharacterController boundController;
    private NavMeshAgent boundAgent;
    private bool warnedMissingBody;
    private readonly CharacterNavigationSuspension navigation = new();
    private readonly System.Collections.Generic.HashSet<MonoBehaviour> verticalOwners = new();

    public Transform ActorRoot => boundRoot ? boundRoot : ctx ? ctx.transform : transform;
    private CharacterController Controller => ctx && ctx.cc ? ctx.cc : boundController;
    private NavMeshAgent Agent => ctx && ctx.NavigationAgent ? ctx.NavigationAgent : boundAgent;

    public bool IsAgentTransformSuspended => navigation.OwnsPosition;
    public bool RootMotionOwnsY
    {
        get
        {
            foreach (var owner in verticalOwners)
                if (owner && owner.isActiveAndEnabled) return true;
            return false;
        }
    }

    public void SetRootMotionVerticalOwner(MonoBehaviour owner, bool ownsY)
    {
        if (ownsY) verticalOwners.Add(owner);
        else verticalOwners.Remove(owner);
    }

    public int AcquireAgentSuspension(NavMeshAgent agent, bool position = true, bool rotation = true)
        => navigation.Acquire(agent ? agent : Agent, position, rotation);

    public void ReleaseAgentSuspension(int token, bool resumeAutonomy = false)
        => navigation.Release(token, resumeAutonomy);

    void Update() => navigation.Tick();
    void OnEnable() => navigation.Tick();
    void OnDisable()
    {
        navigation.ReleaseAll();
        verticalOwners.Clear();
    }
    void OnDestroy() => navigation.ReleaseAll();

    public void Bind(CharacteContext context)
    {
        if (ctx != context)
        {
            boundRoot = null;
            boundController = null;
            boundAgent = null;
        }
        ctx = context;
    }

    /// <summary>Bind explicit authoring once at initialization/model rebind. Context peers stay live.</summary>
    public void Configure(CharacteContext context, Transform actorRoot,
        CharacterController controller = null, NavMeshAgent agent = null)
    {
        Bind(context);
        boundRoot = actorRoot;
        boundController = controller;
        boundAgent = agent;
    }

    /// <summary>Read-only lookup. Use context.EnsureMovementMotor() for explicit initialization.</summary>
    public static CharacterMovementMotor Resolve(CharacteContext context, Transform fallbackRoot)
    {
        if (context && context.MovementMotor) return context.MovementMotor;
        Transform root = context ? context.transform : fallbackRoot;
        if (!root) return null;
        return root.GetComponent<CharacterMovementMotor>();
    }

    public Vector3 Move(Vector3 delta, CharacterMovementSettings settings)
        => ApplyMovement(delta, settings, ActorRoot, Controller, Agent);

    private Vector3 ApplyMovement(Vector3 delta, CharacterMovementSettings settings,
        Transform root, CharacterController controller, NavMeshAgent agent)
    {
        if (delta.sqrMagnitude == 0f) return Vector3.zero;
        Vector3 before = root.position;
        if (controller)
        {
            if (!controller.enabled || !controller.gameObject.activeInHierarchy) return Vector3.zero;
            MoveWithController(delta, settings.IgnoreLayers, root, controller);
        }
        else
        {
            if (!MoveWithBodySweep(delta, settings, root, agent)) return Vector3.zero;
        }
        SyncAgent(agent, root);
        return root.position - before;
    }

    private static void MoveWithController(Vector3 delta, int ignoreLayers,
        Transform root, CharacterController controller)
    {
        Vector3 before = controller.transform.position;
        int excluded = controller.excludeLayers;
        try
        {
            controller.excludeLayers = excluded | ignoreLayers;
            controller.Move(delta);
        }
        finally { controller.excludeLayers = excluded; }

        if (controller.transform != root && controller.transform.IsChildOf(root))
        {
            Vector3 applied = controller.transform.position - before;
            controller.transform.position = before;
            root.position += applied;
        }
    }

    private bool MoveWithBodySweep(Vector3 delta, CharacterMovementSettings settings,
        Transform root, NavMeshAgent agent)
    {
        if (!TryResolveBodyShape(settings, root, agent, out var body, out var shape))
        {
            if (!warnedMissingBody)
                Debug.LogWarning($"[CharacterMovementMotor] '{root.name}' needs an enabled CharacterController or a solid CharacterPositionCollider for movement.", this);
            warnedMissingBody = true;
            return false;
        }
        warnedMissingBody = false;
        int mask = settings.CollisionMask & ~settings.IgnoreLayers;
        root.position += settings.Slide
            ? CharacterBodySweepUtility.ResolveSlidingDelta(shape, delta, settings.Padding,
                mask, settings.Triggers, root, body)
            : CharacterBodySweepUtility.ResolveSafeDelta(shape, delta, settings.Padding,
                mask, settings.Triggers, root);
        return true;
    }

    private bool TryResolveBodyShape(CharacterMovementSettings settings, Transform root,
        NavMeshAgent agent, out Collider body, out CharacterBodySweepShape shape)
    {
        body = ctx && ctx.ColliderRefs ? ctx.ColliderRefs.CharacterPositionCollider : null;
        if (!CharacterBodySweepUtility.IsUsableBody(body)) body = settings.FallbackBody;
        if (CharacterBodySweepUtility.TryResolveShape(body, out shape)) return true;
        if (!settings.AllowAgentShape || !agent || !agent.enabled) return false;

        float radius = Mathf.Max(0.001f, agent.radius);
        float height = Mathf.Max(agent.height, radius * 2f + 0.001f);
        Vector3 center = root.position + Vector3.up * (height * 0.5f);
        Vector3 half = Vector3.up * Mathf.Max(0f, height * 0.5f - radius);
        shape = new CharacterBodySweepShape(CharacterBodySweepKind.Capsule,
            center + half, center - half, radius, Vector3.zero, Quaternion.identity);
        return true;
    }

    public static void SyncAgent(NavMeshAgent agent, Transform root)
    {
        if (agent && agent.isActiveAndEnabled && agent.isOnNavMesh)
            agent.nextPosition = root.position;
    }

    /// <summary>Only for a position already admitted by the caller's landing/recovery checks.</summary>
    public bool PlaceOnNavMesh(Vector3 position)
    {
        NavMeshAgent agent = Agent;
        Transform actorRoot = ActorRoot;
        if (!agent || !agent.isActiveAndEnabled || !agent.Warp(position)) return false;
        actorRoot.position = position;
        SyncAgent(agent, actorRoot);
        navigation.Tick();
        return true;
    }
}
