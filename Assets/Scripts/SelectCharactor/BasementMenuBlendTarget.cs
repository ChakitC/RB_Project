using Unity.Cinemachine;
using UnityEngine;

// Session-only reference for Cinemachine's target-aware orientation blending.
public sealed class BasementMenuBlendTarget : CinemachineExtension
{
    public Transform Target { get; set; }
    public Vector3 Offset { get; set; }
    public Quaternion AuthoredOrientation { get; set; } = Quaternion.identity;

    protected override void PostPipelineStageCallback(CinemachineVirtualCameraBase vcam,
        CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
    {
        if (stage != CinemachineCore.Stage.Finalize || Target == null) return;
        state.ReferenceLookAt = Target.position + Target.rotation * Offset;

        // The target-aware blend preserves yaw/pitch offsets. Put the authored roll
        // in Dutch so it is also blended, without changing the endpoint orientation.
        Vector3 forward = state.RawOrientation * Vector3.forward;
        if (Vector3.Cross(forward, state.ReferenceUp).sqrMagnitude < .000001f) return;
        Quaternion upright = Quaternion.LookRotation(forward, state.ReferenceUp);
        // Cinemachine writes RawOrientation back to the camera Transform after
        // this callback. Read roll from the authored pose, not that previous output.
        Vector3 authoredForward = AuthoredOrientation * Vector3.forward;
        Quaternion authoredUpright = Quaternion.LookRotation(authoredForward, state.ReferenceUp);
        float roll = Vector3.SignedAngle(authoredUpright * Vector3.up,
            AuthoredOrientation * Vector3.up, authoredForward);
        state.RawOrientation = upright;
        var lens = state.Lens;
        lens.Dutch += roll;
        state.Lens = lens;
    }
}
