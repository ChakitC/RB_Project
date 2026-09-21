using UnityEngine;
using UnityEngine.AI;

// Owns only timed approach displacement. Skill playback, damage and outcome remain attack-owned.
internal sealed class DefensiveBlockApproachMotor
{
    static readonly RaycastHit[] GroundHits = new RaycastHit[16];
    readonly CharacteContext ctx;
    readonly CharacterPlacementFootprint footprint;
    readonly Vector3 origin;
    readonly float duration;
    readonly LayerMask worldLayers;
    readonly NavMeshAgent agent;
    readonly AgentMoveDriver move;
    readonly bool moveEnabled, updatePosition, updateRotation, stopped, kinematic, ownsAgent;
    readonly int controlToken;
    Vector3 previous;
    float elapsed;
    bool released;
    public Vector3 Destination { get; }
    public float Progress => Mathf.Clamp01(elapsed / duration);

    public static bool TryPlan(CharacteContext actor, Vector3 guardPosition, Quaternion guardRotation,
        float standOff, LayerMask world, out Vector3 destination, out CharacterPlacementFootprint shape)
    {
        destination = guardPosition + guardRotation * Vector3.forward * standOff;
        shape = default;
        if (actor == null || !CharacterPlacementFootprintUtility.TryGetColliderFootprint(
            actor.ColliderRefs != null ? actor.ColliderRefs.CharacterPositionCollider : null, actor.transform, out shape, out _)) return false;
        Vector3 delta = destination - actor.transform.position;
        delta.y = 0f;
        Vector3 guardForward = guardRotation * Vector3.forward;
        // Close frontal attacks keep their current position for the full authored
        // duration. Never pull them back to stand-off or catch an actor past the guard.
        if (Vector3.Dot(actor.transform.forward, guardForward) > -.25f ||
            Vector3.Dot(actor.transform.position - guardPosition, guardForward) <= 0f) return false;
        if (Vector3.Dot(delta, actor.transform.forward) < .05f)
            destination = actor.transform.position;
        if (!NavMesh.SamplePosition(destination, out var end, .25f, NavMesh.AllAreas) ||
            Mathf.Abs(end.position.y - destination.y) > .2f ||
            NavMesh.Raycast(actor.transform.position, end.position, out _, NavMesh.AllAreas)) return false;
        destination = end.position;
        return HasGround(destination, world) && PathClear(actor, shape, destination, world);
    }

    static bool HasGround(Vector3 position, LayerMask world)
    {
        int count = Physics.RaycastNonAlloc(position + Vector3.up * .2f, Vector3.down, GroundHits,
            .4f, world, QueryTriggerInteraction.Ignore);
        if (count == GroundHits.Length) return false;
        for (int i = 0; i < count; i++)
            if (GroundHits[i].normal.y >= .5f && GroundHits[i].collider.GetComponentInParent<CharacteContext>() == null) return true;
        return false;
    }

    static bool PathClear(CharacteContext actor, CharacterPlacementFootprint shape, Vector3 target, LayerMask world)
    {
        Vector3 delta = target - actor.transform.position;
        delta.y = 0f;
        if (delta.sqrMagnitude < .000001f) return true;
        var sweep = DefensiveBlockSlideMotor.ResolveShape(shape, actor.transform);
        float allowed = CharacterBodySweepUtility.ResolveAllowedDistance(sweep, delta.normalized,
            delta.magnitude, .02f, world, QueryTriggerInteraction.Ignore, actor.transform);
        return allowed + .005f >= delta.magnitude;
    }

    public DefensiveBlockApproachMotor(CharacteContext actor, CharacterPlacementFootprint shape,
        Vector3 destination, float seconds, LayerMask world)
    {
        ctx = actor; footprint = shape; Destination = destination; duration = seconds; worldLayers = world;
        origin = previous = ctx.transform.position;
        // Do not block Skill: a cast accepted before release must still commit its existing
        // reservation. Its exclusive Skill animation already rejects new ordinary skills.
        controlToken = ctx.stateHub != null ? ctx.stateHub.AcquireExternalControlBlockToken(
            ControlBlockFlags.Move | ControlBlockFlags.Rotate | ControlBlockFlags.Shoot) : 0;
        if (ctx is EnemyContext enemy) { agent = enemy.Agent; move = enemy.AgentMoveDriver; }
        if (move != null) { moveEnabled = move.enabled; move.enabled = false; }
        ownsAgent = agent != null && agent.enabled && agent.isOnNavMesh;
        if (ownsAgent)
        {
            updatePosition = agent.updatePosition; updateRotation = agent.updateRotation; stopped = agent.isStopped;
            agent.isStopped = true; agent.updatePosition = false; agent.updateRotation = false;
            agent.nextPosition = origin;
        }
        if (ctx.rb != null) { kinematic = ctx.rb.isKinematic; ctx.rb.isKinematic = true; }
    }

    public bool Advance(float dt)
    {
        if (released || ctx == null || Vector3.Distance(ctx.transform.position, previous) > .15f) return false;
        if (dt <= 0f) return true;
        elapsed = Mathf.Min(duration, elapsed + dt);
        Vector3 desired = Vector3.Lerp(origin, Destination, Progress);
        if (!PathClear(ctx, footprint, desired, worldLayers) ||
            NavMesh.Raycast(ctx.transform.position, desired, out _, NavMesh.AllAreas) ||
            !NavMesh.SamplePosition(desired, out var floor, .2f, NavMesh.AllAreas) ||
            Mathf.Abs(floor.position.y - desired.y) > .2f) return false;
        // A stale NavMesh alone does not prove that a floor still exists.
        if (!HasGround(floor.position, worldLayers)) return false;
        ctx.transform.position = previous = floor.position;
        if (ownsAgent && agent != null && agent.enabled && agent.isOnNavMesh) agent.nextPosition = previous;
        return true;
    }

    public void Release()
    {
        if (released) return;
        released = true;
        if (ctx == null) return;
        if (ownsAgent && agent != null && agent.enabled)
        {
            if (agent.isOnNavMesh) { agent.nextPosition = ctx.transform.position; agent.isStopped = stopped; }
            agent.updatePosition = updatePosition; agent.updateRotation = updateRotation;
        }
        if (move != null) move.enabled = moveEnabled;
        if (ctx.rb != null) ctx.rb.isKinematic = kinematic;
        if (controlToken != 0) ctx.stateHub?.ReleaseExternalControlBlockToken(controlToken);
    }
}
