#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

/// <summary>
/// One gameplay projectile must have exactly one component that owns its movement, its lifetime,
/// and its root activation, and that component is <see cref="Projectile"/>.
///
/// The rule is deliberately reflective rather than a blocklist of known-bad types, so a future
/// vendor mover or a hand-rolled "just moves the bullet" script is caught the day it is dropped
/// onto a prefab root. A component on the root trips the rule when it either drives its own
/// physics step (declares FixedUpdate) or holds a serialized handle on the root's Rigidbody or
/// Collider - the two things a second owner needs in order to fight <see cref="Projectile"/>.
/// </summary>
public static class ProjectileRootOwnerRule
{
    /// <summary>
    /// Appends one issue string per conflicting component found on <paramref name="projectileRoot"/>.
    /// Returns true when the root has a single owner.
    /// </summary>
    public static bool Validate(GameObject projectileRoot, List<string> issues)
    {
        if (projectileRoot == null)
            return true;

        var projectile = projectileRoot.GetComponent<Projectile>();
        if (projectile == null)
            return true;

        bool clean = true;
        var components = projectileRoot.GetComponents<MonoBehaviour>();

        for (int i = 0; i < components.Length; i++)
        {
            MonoBehaviour component = components[i];
            if (component == null || component is Projectile)
                continue;

            string reason = DescribeConflict(component, projectileRoot);
            if (reason == null)
                continue;

            clean = false;
            issues?.Add(
                $"'{projectileRoot.name}' has a second movement/lifetime owner on its root: " +
                $"{component.GetType().Name} ({reason}). A gameplay projectile must let Projectile " +
                $"own movement, lifetime, and pool return. Move presentation-only work to a component " +
                $"that does not write the Rigidbody, time a lifetime, or toggle the root.");
        }

        return clean;
    }

    /// <summary>Null when the component is harmless; otherwise a short reason for the report.</summary>
    public static string DescribeConflict(MonoBehaviour component, GameObject projectileRoot)
    {
        if (component == null || component is Projectile)
            return null;

        Type type = component.GetType();

        if (DeclaresMethod(type, "FixedUpdate"))
            return "declares its own FixedUpdate movement step";

        string bodyField = FindRootBodyField(component, type, projectileRoot);
        if (bodyField != null)
            return $"holds a serialized reference to the root's physics component via '{bodyField}'";

        return null;
    }

    static bool DeclaresMethod(Type type, string methodName)
    {
        const BindingFlags flags =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        for (Type t = type; t != null && t != typeof(MonoBehaviour); t = t.BaseType)
        {
            if (t.GetMethod(methodName, flags) != null)
                return true;
        }

        return false;
    }

    static string FindRootBodyField(MonoBehaviour component, Type type, GameObject projectileRoot)
    {
        const BindingFlags flags =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        for (Type t = type; t != null && t != typeof(MonoBehaviour); t = t.BaseType)
        {
            FieldInfo[] fields = t.GetFields(flags);
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];
                if (!typeof(Rigidbody).IsAssignableFrom(field.FieldType) &&
                    !typeof(Collider).IsAssignableFrom(field.FieldType))
                    continue;

                if (field.GetValue(component) is not Component value)
                    continue;

                if (value.gameObject == projectileRoot)
                    return field.Name;
            }
        }

        return null;
    }
}
#endif
