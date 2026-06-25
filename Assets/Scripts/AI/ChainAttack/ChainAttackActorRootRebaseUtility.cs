using UnityEngine;
using UnityEngine.AI;

internal static class ChainAttackActorRootRebaseUtility
{
    public static bool TryRebaseToAnimationRoot(
        CharacteContext context,
        Transform visualRoot,
        Transform animationRoot,
        NavMeshAgent agent,
        out Vector3 appliedPositionDelta,
        out float appliedYawDelta,
        out string failureReason)
    {
        appliedPositionDelta = Vector3.zero;
        appliedYawDelta = 0f;
        failureReason = null;

        if (context == null)
        {
            failureReason = "Missing character context.";
            return false;
        }

        Transform contextRoot = context.transform;
        if (visualRoot == null)
        {
            failureReason = "Missing visual root.";
            return false;
        }

        if (animationRoot == null)
        {
            failureReason = "Missing animation root.";
            return false;
        }

        if (visualRoot == contextRoot || !visualRoot.IsChildOf(contextRoot))
        {
            failureReason = "Visual root must be a child of the character context root.";
            return false;
        }

        if (animationRoot == contextRoot || !animationRoot.IsChildOf(contextRoot))
        {
            failureReason = "Animation root must be a child of the character context root.";
            return false;
        }

        context.ResolveReferences();

        Vector3 originalPosition = contextRoot.position;
        Quaternion originalRotation = contextRoot.rotation;
        Vector3 visualWorldPosition = visualRoot.position;
        Quaternion visualWorldRotation = visualRoot.rotation;

        Vector3 targetPosition = animationRoot.position;
        targetPosition.y = originalPosition.y;
        Quaternion targetRotation = Quaternion.Euler(0f, animationRoot.eulerAngles.y, 0f);

        if (agent != null && agent.enabled && !agent.isOnNavMesh)
        {
            if (!NavMesh.SamplePosition(targetPosition, out NavMeshHit navHit, 1f, NavMesh.AllAreas))
            {
                failureReason = "NavMeshAgent is off NavMesh and no nearby sync position was found.";
                return false;
            }

            targetPosition = navHit.position;
        }

        CharacterController characterController = context.cc;
        bool restoreCharacterController = characterController != null && characterController.enabled;
        if (restoreCharacterController)
            characterController.enabled = false;

        try
        {
            Rigidbody rigidbody = context.rb;
            if (rigidbody != null)
            {
                rigidbody.linearVelocity = Vector3.zero;
                rigidbody.angularVelocity = Vector3.zero;
                rigidbody.position = targetPosition;
                rigidbody.rotation = targetRotation;
            }

            contextRoot.SetPositionAndRotation(targetPosition, targetRotation);

            if (agent != null && agent.enabled)
            {
                agent.Warp(targetPosition);
                agent.nextPosition = targetPosition;
            }

            visualRoot.SetPositionAndRotation(visualWorldPosition, visualWorldRotation);
            Physics.SyncTransforms();
        }
        finally
        {
            if (restoreCharacterController && characterController != null)
                characterController.enabled = true;
        }

        appliedPositionDelta = targetPosition - originalPosition;
        appliedYawDelta = Mathf.DeltaAngle(originalRotation.eulerAngles.y, targetRotation.eulerAngles.y);
        return true;
    }

    public static bool TryCompensateVisualRootToAnimationPose(
        Transform visualRoot,
        Transform animationRoot,
        Vector3 targetWorldPosition,
        float targetWorldYaw,
        out Vector3 appliedPositionDelta,
        out float appliedYawDelta,
        out string failureReason)
    {
        appliedPositionDelta = Vector3.zero;
        appliedYawDelta = 0f;
        failureReason = null;

        if (visualRoot == null)
        {
            failureReason = "Missing visual root.";
            return false;
        }

        if (animationRoot == null || !animationRoot.IsChildOf(visualRoot))
        {
            failureReason = "Animation root must be a child of the visual root.";
            return false;
        }

        Vector3 originalVisualPosition = visualRoot.position;
        Quaternion originalVisualRotation = visualRoot.rotation;
        Vector3 animationPosition = animationRoot.position;
        float animationYaw = animationRoot.eulerAngles.y;

        float yawDelta = Mathf.DeltaAngle(animationYaw, targetWorldYaw);
        Quaternion rotationDelta = Quaternion.AngleAxis(yawDelta, Vector3.up);
        Vector3 animationOffset = animationPosition - originalVisualPosition;
        Vector3 targetVisualPosition = targetWorldPosition - rotationDelta * animationOffset;
        targetVisualPosition.y = originalVisualPosition.y;
        Quaternion targetVisualRotation = rotationDelta * originalVisualRotation;

        visualRoot.SetPositionAndRotation(targetVisualPosition, targetVisualRotation);
        Physics.SyncTransforms();

        appliedPositionDelta = targetVisualPosition - originalVisualPosition;
        appliedYawDelta = yawDelta;
        return true;
    }

    public static bool TryCommitVisualRootCompensation(
        CharacteContext context,
        Transform visualRoot,
        Vector3 authoredLocalPosition,
        Quaternion authoredLocalRotation,
        NavMeshAgent agent,
        out Vector3 appliedPositionDelta,
        out float appliedYawDelta,
        out string failureReason)
    {
        appliedPositionDelta = Vector3.zero;
        appliedYawDelta = 0f;
        failureReason = null;

        if (context == null)
        {
            failureReason = "Missing character context.";
            return false;
        }

        Transform contextRoot = context.transform;
        if (visualRoot == null || visualRoot.parent != contextRoot)
        {
            failureReason = "Visual root must be a direct child of the character context root.";
            return false;
        }

        context.ResolveReferences();

        Vector3 originalContextPosition = contextRoot.position;
        Quaternion originalContextRotation = contextRoot.rotation;
        Vector3 visualWorldPosition = visualRoot.position;
        float visualWorldYaw = visualRoot.eulerAngles.y;
        float authoredLocalYaw = authoredLocalRotation.eulerAngles.y;

        Quaternion targetContextRotation = Quaternion.Euler(
            0f,
            visualWorldYaw - authoredLocalYaw,
            0f);
        Vector3 scaledLocalOffset = Vector3.Scale(authoredLocalPosition, contextRoot.lossyScale);
        Vector3 targetContextPosition = visualWorldPosition - targetContextRotation * scaledLocalOffset;
        targetContextPosition.y = originalContextPosition.y;

        CharacterController characterController = context.cc;
        bool restoreCharacterController = characterController != null && characterController.enabled;
        if (restoreCharacterController)
            characterController.enabled = false;

        try
        {
            Rigidbody rigidbody = context.rb;
            if (rigidbody != null)
            {
                rigidbody.linearVelocity = Vector3.zero;
                rigidbody.angularVelocity = Vector3.zero;
                rigidbody.position = targetContextPosition;
                rigidbody.rotation = targetContextRotation;
            }

            contextRoot.SetPositionAndRotation(targetContextPosition, targetContextRotation);
            visualRoot.localPosition = authoredLocalPosition;
            visualRoot.localRotation = authoredLocalRotation;

            if (agent != null && agent.enabled)
            {
                if (agent.isOnNavMesh)
                {
                    agent.Warp(targetContextPosition);
                    agent.nextPosition = targetContextPosition;
                }
                else if (NavMesh.SamplePosition(
                             targetContextPosition,
                             out NavMeshHit navHit,
                             1f,
                             NavMesh.AllAreas))
                {
                    agent.Warp(navHit.position);
                    agent.nextPosition = navHit.position;
                }
            }

            Physics.SyncTransforms();
        }
        finally
        {
            if (restoreCharacterController && characterController != null)
                characterController.enabled = true;
        }

        appliedPositionDelta = targetContextPosition - originalContextPosition;
        appliedYawDelta = Mathf.DeltaAngle(
            originalContextRotation.eulerAngles.y,
            targetContextRotation.eulerAngles.y);
        return true;
    }
}
