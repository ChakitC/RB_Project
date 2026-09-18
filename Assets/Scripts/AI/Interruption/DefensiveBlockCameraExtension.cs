using Unity.Cinemachine;

// The gameplay rig remains live beneath the shot, including collision and aim.
public sealed class DefensiveBlockCameraExtension : CinemachineExtension
{
    public GameplayCameraController Owner { get; set; }
    protected override void PostPipelineStageCallback(CinemachineVirtualCameraBase vcam,
        CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
    {
        if (stage == CinemachineCore.Stage.Finalize && Owner != null && Owner.isActiveAndEnabled)
            Owner.ApplyDefensiveBlockShot(ref state);
    }
}
