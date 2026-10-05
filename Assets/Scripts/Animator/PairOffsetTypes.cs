public enum PairOffsetBasePose
{
    None = 0,
    Idle = 1,
    Forward = 2,
    Backward = 3,
    Left = 4,
    Right = 5,
    ForwardLeft = 6,
    ForwardRight = 7,
    BackwardLeft = 8,
    BackwardRight = 9,
    Slide = 10,
    Sprint = 11,
    CrouchIdle = 12,
    CrouchMove = 13,
}

public enum PairOffsetUpperAction
{
    None = 0,
    ShootPulse = 1,
    ShootHold = 2,
    Reload = 3,
}

public readonly struct PairOffsetBasePoseWeight
{
    public readonly PairOffsetBasePose Pose;
    public readonly float Weight;
    public readonly UnityEngine.AnimationClip Clip;

    public PairOffsetBasePoseWeight(PairOffsetBasePose pose, float weight, UnityEngine.AnimationClip clip = null)
    {
        Pose = pose;
        Weight = weight;
        Clip = clip;
    }
}
