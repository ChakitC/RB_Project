using UnityEngine;
using UnityEngine.AI;

public static class CharacterPlacementProbeUtility
{
    public static bool TryGetFootprint(
        GameObject prefab,
        SummonMobility mobility,
        out CharacterPlacementFootprint footprint,
        out string error)
    {
        footprint = default;
        error = string.Empty;
        if (prefab == null)
        {
            error = "Summon prefab is missing.";
            return false;
        }

        if (mobility == SummonMobility.Mobile)
        {
            CharacterController controller = prefab.GetComponentInChildren<CharacterController>(true);
            if (controller != null)
            {
                footprint = CharacterPlacementFootprintUtility.CreateVerticalCapsuleFootprint(
                    prefab.transform,
                    controller.transform,
                    controller.center,
                    controller.radius,
                    controller.height);
                return true;
            }

            NavMeshAgent agent = prefab.GetComponentInChildren<NavMeshAgent>(true);
            if (agent != null)
            {
                footprint = CharacterPlacementFootprintUtility.CreateVerticalCapsuleFootprint(
                    prefab.transform,
                    agent.transform,
                    Vector3.zero,
                    agent.radius,
                    agent.height);
                return true;
            }

            error = "Mobile summon requires CharacterController or NavMeshAgent footprint.";
            return false;
        }

        CharacterColliderRefs refs = prefab.GetComponentInChildren<CharacterColliderRefs>(true);
        if (refs == null || refs.CharacterPositionCollider == null)
        {
            error = "Stationary summon requires CharacterColliderRefs.CharacterPositionCollider.";
            return false;
        }

        if (CharacterPlacementFootprintUtility.TryGetColliderFootprint(
            refs.CharacterPositionCollider,
            prefab.transform,
            out footprint,
            out error))
            return true;

        if (refs.CharacterPositionCollider != null &&
            error.StartsWith("Unsupported placement footprint collider", System.StringComparison.Ordinal))
        {
            error = $"Unsupported stationary footprint collider '{refs.CharacterPositionCollider.GetType().Name}'. Use BoxCollider, CapsuleCollider, or SphereCollider.";
        }

        return false;
    }

}
