#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class CharacterPlacementBaselineTests
{
    readonly List<Object> createdObjects = new();

    [TearDown]
    public void TearDown()
    {
        for (int i = createdObjects.Count - 1; i >= 0; i--)
        {
            if (createdObjects[i] != null)
                Object.DestroyImmediate(createdObjects[i]);
        }

        createdObjects.Clear();
    }

    [Test]
    public void LegacyChainTeleportStopsAtFirstAcceptedCandidateInAuthoredOrder()
    {
        ChainAttackTeleportProfileDef profile = Track(
            ScriptableObject.CreateInstance<ChainAttackTeleportProfileDef>());
        profile.useAnchorRotationAsBase = true;
        profile.requireNavMeshAtAnchor = false;
        profile.anchorPositionOffset = Vector3.back;

        Transform anchor = Track(new GameObject("PlacementBaselineAnchor")).transform;
        anchor.position = new Vector3(10f, 0f, 20f);
        anchor.rotation = Quaternion.Euler(0f, 25f, 0f);

        var testedPositions = new List<Vector3>();
        bool resolved = ChainAttackTeleportUtility.TryResolveTeleportPose(
            profile,
            anchor,
            Quaternion.identity,
            out Vector3 resolvedPosition,
            out _,
            poseValidator: (candidatePosition, _) =>
            {
                testedPositions.Add(candidatePosition);
                return testedPositions.Count == 3;
            });

        Assert.That(resolved, Is.True);
        Assert.That(testedPositions, Has.Count.EqualTo(3));
        Assert.That(Vector3.Distance(
                testedPositions[0],
                anchor.TransformPoint(Vector3.back)),
            Is.LessThan(0.001f));
        Assert.That(Vector3.Distance(
                testedPositions[1],
                anchor.TransformPoint(Quaternion.AngleAxis(15f, Vector3.up) * Vector3.back)),
            Is.LessThan(0.001f));
        Assert.That(Vector3.Distance(
                testedPositions[2],
                anchor.TransformPoint(Quaternion.AngleAxis(-15f, Vector3.up) * Vector3.back)),
            Is.LessThan(0.001f));
        Assert.That(Vector3.Distance(resolvedPosition, testedPositions[2]), Is.LessThan(0.001f));
    }

    [Test]
    public void CentralChainTeleportScoresCandidatesWhenNoPoseValidatorIsProvided()
    {
        ChainAttackTeleportProfileDef profile = Track(
            ScriptableObject.CreateInstance<ChainAttackTeleportProfileDef>());
        profile.requireNavMeshAtAnchor = false;
        profile.anchorPositionOffset = Vector3.back;
        profile.obstacleLayers = 1 << 0;

        Transform anchor = Track(new GameObject("CentralPlacementAnchor")).transform;
        anchor.position = Vector3.zero;
        anchor.rotation = Quaternion.identity;

        GameObject blocker = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
        blocker.name = "CentralPlacementBlocker";
        blocker.transform.position = new Vector3(0.4f, 1f, -0.55f);
        blocker.transform.localScale = Vector3.one * 0.2f;
        Physics.SyncTransforms();

        Assert.That(ChainAttackTeleportUtility.TryResolveTeleportPose(
                profile,
                anchor,
                Quaternion.identity,
                out Vector3 resolvedPosition,
                out _), Is.True);
        Assert.That(Vector3.Distance(
                resolvedPosition,
                anchor.TransformPoint(Quaternion.AngleAxis(15f, Vector3.up) * Vector3.back)),
            Is.LessThan(0.001f));
    }

    [Test]
    public void CentralChainTeleportReplacesReservationForSameOwner()
    {
        ChainAttackTeleportProfileDef profile = Track(
            ScriptableObject.CreateInstance<ChainAttackTeleportProfileDef>());
        profile.requireNavMeshAtAnchor = false;
        profile.anchorPositionOffset = Vector3.back;
        profile.clearanceHalfExtents = Vector3.one * 0.5f;
        profile.obstacleLayers = 0;

        Transform anchor = Track(new GameObject("ChainReservationAnchor")).transform;
        anchor.position = new Vector3(10f, 0f, 20f);
        Transform owner = Track(new GameObject("ChainReservationOwner")).transform;
        CharacterPlacementReservationService reservations = new();

        Assert.That(ChainAttackTeleportUtility.TryResolveTeleportPose(
                profile,
                anchor,
                Quaternion.identity,
                out Vector3 firstPosition,
                out _,
                probeRoot: owner,
                reservations: reservations), Is.True);
        Assert.That(reservations.ActiveCount, Is.EqualTo(1));

        anchor.position += Vector3.right * 3f;
        Assert.That(ChainAttackTeleportUtility.TryResolveTeleportPose(
                profile,
                anchor,
                Quaternion.identity,
                out Vector3 secondPosition,
                out _,
                probeRoot: owner,
                reservations: reservations), Is.True);
        Assert.That(reservations.ActiveCount, Is.EqualTo(1));
        Assert.That(Vector3.Distance(firstPosition, secondPosition), Is.GreaterThan(1f));
    }

    T Track<T>(T value) where T : Object
    {
        createdObjects.Add(value);
        return value;
    }
}
#endif
