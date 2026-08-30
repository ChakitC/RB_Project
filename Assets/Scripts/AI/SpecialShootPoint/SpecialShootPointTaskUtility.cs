using UnityEngine;

/// <summary>
/// Shared controller lookup for the Special Shoot Point Behavior Tree tasks.
///
/// Goes through <see cref="EnemyContext"/> first because the controller is registered there, and
/// enemy prefab hierarchies are not uniform — the context, the meter, and this controller are not
/// guaranteed to sit on the same object the Behavior Tree does.
/// </summary>
public static class SpecialShootPointTaskUtility
{
    public static SpecialShootPointController Resolve(GameObject taskOwner)
    {
        if (taskOwner == null)
            return null;

        EnemyContext ctx = taskOwner.GetComponent<EnemyContext>();
        if (ctx == null)
            ctx = taskOwner.GetComponentInParent<EnemyContext>();

        if (ctx != null)
        {
            ctx.ResolveReferences();
            if (ctx.SpecialShootPoints != null)
                return ctx.SpecialShootPoints;
        }

        // Compatibility fallback only, for a tree placed on an object with no resolvable context.
        SpecialShootPointController controller = taskOwner.GetComponent<SpecialShootPointController>();
        if (controller == null)
            controller = taskOwner.GetComponentInParent<SpecialShootPointController>();

        return controller;
    }
}
