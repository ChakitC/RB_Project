using UnityEngine;

public readonly struct CharacterPlacementResult
{
    CharacterPlacementResult(
        bool isValid,
        Vector3 startPosition,
        Quaternion startRotation,
        Vector3 impactPosition,
        Quaternion impactRotation,
        int candidateIndex,
        CharacterPlacementScore score,
        string failureReason)
    {
        IsValid = isValid;
        StartPosition = startPosition;
        StartRotation = startRotation;
        ImpactPosition = impactPosition;
        ImpactRotation = impactRotation;
        CandidateIndex = candidateIndex;
        Score = score;
        FailureReason = failureReason;
    }

    public bool IsValid { get; }
    public Vector3 StartPosition { get; }
    public Quaternion StartRotation { get; }
    public Vector3 ImpactPosition { get; }
    public Quaternion ImpactRotation { get; }
    public int CandidateIndex { get; }
    public CharacterPlacementScore Score { get; }
    public string FailureReason { get; }

    public static CharacterPlacementResult Success(
        Vector3 startPosition,
        Quaternion startRotation,
        Vector3 impactPosition,
        Quaternion impactRotation,
        int candidateIndex,
        CharacterPlacementScore score)
    {
        return new CharacterPlacementResult(
            true,
            startPosition,
            startRotation,
            impactPosition,
            impactRotation,
            candidateIndex,
            score,
            null);
    }

    public static CharacterPlacementResult Failed(string failureReason)
    {
        return new CharacterPlacementResult(
            false,
            Vector3.zero,
            Quaternion.identity,
            Vector3.zero,
            Quaternion.identity,
            -1,
            default,
            failureReason ?? "Character placement failed.");
    }
}
