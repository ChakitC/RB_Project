using UnityEngine;

/// <summary>
/// Finds the point just above a character's head without touching its skeleton.
///
/// Bone lookup is deliberately avoided: a "head" bone is not present on every rig in this
/// project, and rigs that do have one place it at wildly different heights relative to the
/// visual top of the character. Bounds are the one measurement every character prefab can
/// answer, whether it is driven by a CharacterController, a plain collider, or renderers only.
/// </summary>
public static class CharacterTargetHeightUtility
{
    /// <summary>Fallback height for a character that reports no usable bounds at all.</summary>
    public const float FallbackHeight = 1.8f;

    /// <summary>
    /// World point above the target's head. <paramref name="clearance"/> is added on top of the
    /// resolved bounds so a delivery finishes visibly above the character rather than inside it.
    /// </summary>
    public static Vector3 ResolveOverheadPoint(CharacteContext target, float clearance = 0f)
    {
        if (target == null)
            return Vector3.zero;

        return ResolveOverheadPoint(target.gameObject, target.transform, clearance);
    }

    public static Vector3 ResolveOverheadPoint(GameObject targetObject, Transform targetRoot, float clearance = 0f)
    {
        if (targetRoot == null)
            return Vector3.zero;

        Vector3 basePosition = targetRoot.position;
        float top = basePosition.y + FallbackHeight;

        if (targetObject != null && TryResolveTop(targetObject, out float resolvedTop))
            top = resolvedTop;

        return new Vector3(basePosition.x, top + clearance, basePosition.z);
    }

    static bool TryResolveTop(GameObject targetObject, out float top)
    {
        top = 0f;

        // CharacterController first: it is the authored capsule, so it tracks crouch/stance
        // changes that a static collider or a renderer bound would not.
        CharacterController controller = targetObject.GetComponentInChildren<CharacterController>(true);
        if (controller != null)
        {
            Vector3 worldCenter = controller.transform.TransformPoint(controller.center);
            top = worldCenter.y + (controller.height * 0.5f);
            return true;
        }

        if (TryResolveColliderTop(targetObject, out top))
            return true;

        return TryResolveRendererTop(targetObject, out top);
    }

    static bool TryResolveColliderTop(GameObject targetObject, out float top)
    {
        top = 0f;
        bool found = false;

        Collider[] colliders = targetObject.GetComponentsInChildren<Collider>(false);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];

            // Trigger volumes are hitboxes, detection ranges, and pickup radii in this project.
            // They routinely dwarf the character, so measuring them would place the delivery
            // point metres above the actual head.
            if (collider == null || collider.isTrigger)
                continue;

            float colliderTop = collider.bounds.max.y;
            if (!found || colliderTop > top)
            {
                top = colliderTop;
                found = true;
            }
        }

        return found;
    }

    static bool TryResolveRendererTop(GameObject targetObject, out float top)
    {
        top = 0f;
        bool found = false;

        Renderer[] renderers = targetObject.GetComponentsInChildren<Renderer>(false);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !renderer.enabled)
                continue;

            float rendererTop = renderer.bounds.max.y;
            if (!found || rendererTop > top)
            {
                top = rendererTop;
                found = true;
            }
        }

        return found;
    }
}
