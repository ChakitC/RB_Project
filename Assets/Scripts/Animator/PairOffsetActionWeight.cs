public readonly struct PairOffsetActionWeight
{
    public readonly PairOffsetUpperAction Action;
    public readonly float Weight;
    public readonly UnityEngine.AnimationClip Clip;

    public PairOffsetActionWeight(PairOffsetUpperAction action, float weight, UnityEngine.AnimationClip clip = null)
    {
        Action = action;
        Weight = weight;
        Clip = clip;
    }
}
