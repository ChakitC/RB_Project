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

    public PairOffsetBasePoseWeight(PairOffsetBasePose pose, float weight)
    {
        Pose = pose;
        Weight = weight;
    }
}
