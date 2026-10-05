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
    public static SpecialShootPointController Resolve(CharacteContext context)
    {
        return (context as EnemyContext)?.SpecialShootPoints;
    }
}
