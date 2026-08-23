using UnityEngine;

/// <summary>
/// One cast's hold on the character it was aimed at.
///
/// A raw <see cref="CharacteContext"/> reference cannot answer the question a targeted skill
/// actually needs answered. Once the object is destroyed the reference becomes Unity's
/// fake-null, which is indistinguishable from "the caller never supplied a target" - and those
/// two cases must settle the cast transaction in opposite directions. Never supplying a target
/// is broken authoring and must refund; losing a target mid-flight is a gameplay outcome and
/// must not.
///
/// The handle also owns the last delivery point it managed to resolve, so a delivery whose
/// target disappears finishes at a sensible place in the world instead of snapping to the origin.
/// </summary>
public sealed class SkillTargetHandle
{
    /// <summary>Shared handle for a cast that was never given a target.</summary>
    public static readonly SkillTargetHandle None = new SkillTargetHandle();

    CharacteContext context;
    Vector3 lastKnownDeliveryPoint;

    /// <summary>
    /// True when a caller locked a target onto this cast. False is the ONLY state that means
    /// "no target": a target that was locked and has since been destroyed still reads true.
    /// </summary>
    public bool WasAssigned { get; }

    /// <summary>
    /// Identity of the originally locked target, for diagnostics and for rejecting a recycled
    /// instance id. Never use this to look the object back up - a cast locks one specific actor
    /// and must never silently retarget.
    /// </summary>
    public int OriginalInstanceId { get; }

    /// <summary>Extra height added above the target's bounds when resolving the delivery point.</summary>
    public float DeliveryClearance { get; }

    SkillTargetHandle()
    {
        WasAssigned = false;
        OriginalInstanceId = 0;
        DeliveryClearance = 0f;
        lastKnownDeliveryPoint = Vector3.zero;
    }

    public SkillTargetHandle(CharacteContext target, float deliveryClearance = 0f)
    {
        DeliveryClearance = deliveryClearance;

        if (target == null)
        {
            WasAssigned = false;
            OriginalInstanceId = 0;
            lastKnownDeliveryPoint = Vector3.zero;
            return;
        }

        context = target;
        WasAssigned = true;
        OriginalInstanceId = target.GetInstanceID();
        lastKnownDeliveryPoint = CharacterTargetHeightUtility.ResolveOverheadPoint(target, deliveryClearance);
    }

    /// <summary>Locks <paramref name="target"/>, or returns <see cref="None"/> when there is nothing to lock.</summary>
    public static SkillTargetHandle For(CharacteContext target, float deliveryClearance = 0f)
    {
        return target != null ? new SkillTargetHandle(target, deliveryClearance) : None;
    }

    /// <summary>True when this cast was given a target, whether or not that target still exists.</summary>
    public static bool IsAssigned(SkillTargetHandle handle)
    {
        return handle != null && handle.WasAssigned;
    }

    /// <summary>
    /// Resolves the locked target if it still exists. Owns the fake-null check and clears its own
    /// cached reference on the way out, so no caller has to write <c>context == null</c> - a
    /// comparison that is easy to get subtly wrong across the codebase.
    /// </summary>
    public bool TryResolveLiveContext(out CharacteContext resolved)
    {
        resolved = null;

        if (!WasAssigned)
            return false;

        CharacteContext current = context;

        // Deliberately Unity's overloaded == so a destroyed object reports null.
        if (current == null)
        {
            context = null;
            return false;
        }

        // Instance ids are recycled. If this slot now holds a different object, the original
        // target is gone and this cast must not follow whatever took its place.
        if (current.GetInstanceID() != OriginalInstanceId)
        {
            context = null;
            return false;
        }

        resolved = current;
        return true;
    }

    /// <summary>
    /// True when the target still exists AND is a valid recipient for a gameplay effect.
    /// Existence and eligibility are separate questions: a downed ally still has a live
    /// reference, and a delivery still travels to them, but nothing lands.
    /// </summary>
    public bool TryResolveEffectTarget(out CharacteContext resolved)
    {
        if (!TryResolveLiveContext(out resolved))
            return false;

        if (!resolved.isActiveAndEnabled)
        {
            resolved = null;
            return false;
        }

        resolved.ResolveReferences();
        HealthSystem health = resolved.HealthSystem;

        if (health == null || !health.IsAlive)
        {
            resolved = null;
            return false;
        }

        return true;
    }

    /// <summary>
    /// World point a delivery should finish at.
    ///
    /// Resolved lazily: while the target lives this recomputes from bounds and refreshes the
    /// cache; once it is gone the cached point is returned unchanged. Note that the cached point
    /// is the last one the system successfully resolved, NOT the target's position at the exact
    /// frame it was destroyed - without a per-frame ticker or a destruction callback that frame
    /// is not observable, and callers should not assume otherwise.
    /// </summary>
    public Vector3 ResolveDeliveryPoint()
    {
        return ResolveDeliveryPoint(DeliveryClearance);
    }

    /// <summary>
    /// Same as <see cref="ResolveDeliveryPoint()"/> but with the clearance the caller wants. How
    /// far above the head something should finish is a presentation decision that belongs to the
    /// payload doing the delivering, not to whoever locked the target.
    /// </summary>
    public Vector3 ResolveDeliveryPoint(float clearance)
    {
        if (TryResolveLiveContext(out CharacteContext live))
            lastKnownDeliveryPoint = CharacterTargetHeightUtility.ResolveOverheadPoint(live, clearance);

        return lastKnownDeliveryPoint;
    }

    /// <summary>Last resolved delivery point without attempting a fresh resolve.</summary>
    public Vector3 LastKnownDeliveryPoint => lastKnownDeliveryPoint;
}
