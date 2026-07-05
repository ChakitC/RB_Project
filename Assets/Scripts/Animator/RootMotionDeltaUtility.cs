using UnityEngine;

internal static class RootMotionDeltaUtility
{
    public static Vector3 GetPositionDelta(Animator animator, bool suppressY)
    {
        Vector3 delta = animator.deltaPosition;
        if (suppressY)
            delta.y = 0f;

        return delta;
    }

    public static float GetYawDelta(Animator animator)
    {
        return Mathf.DeltaAngle(0f, animator.deltaRotation.eulerAngles.y);
    }
}
