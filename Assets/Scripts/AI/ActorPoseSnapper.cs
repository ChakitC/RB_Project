using UnityEngine;

public static class ActorPoseSnapper
{
    public static void Snap(Transform actorTransform, CharacterController cc, Rigidbody rb,
                            Vector3 pos, Quaternion rot)
    {
        bool restoreCC = cc != null && cc.enabled;
        if (restoreCC) cc.enabled = false;

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.position = pos;
            rb.rotation = rot;
        }

        if (actorTransform != null)
            actorTransform.SetPositionAndRotation(pos, rot);

        Physics.SyncTransforms();

        if (restoreCC && cc != null) cc.enabled = true;
    }
}
