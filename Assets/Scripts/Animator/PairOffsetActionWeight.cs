public readonly struct PairOffsetActionWeight
{
    public readonly PairOffsetUpperAction Action;
    public readonly float Weight;

    public PairOffsetActionWeight(PairOffsetUpperAction action, float weight)
    {
        Action = action;
        Weight = weight;
    }
}
