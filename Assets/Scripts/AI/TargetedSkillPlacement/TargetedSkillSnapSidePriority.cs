using UnityEngine;

// pattern การกวาด + การหามุมทิศตรง รวมที่เดียว ใช้ร่วม root-motion (resolver) และ legacy (utility)
internal static class TargetedSkillSnapSidePriority
{
    const float MinPlanarSqrMagnitude = 0.0001f;

    // กวาดออกจาก baseYaw แบบสมมาตร: 0, ±15, ±30 … ±165, 180 (25 ค่า ครบ 360°)
    public static readonly float[] CandidateYawOffsets =
    {
        0f,
        15f, -15f,
        30f, -30f,
        45f, -45f,
        60f, -60f,
        75f, -75f,
        90f, -90f,
        105f, -105f,
        120f, -120f,
        135f, -135f,
        150f, -150f,
        165f, -165f,
        180f,
    };

    // baseYaw = มุมทิศตรง (ฝั่ง actor) ถ้าหาได้, ไม่งั้น 0
    public static float ResolveBaseYaw(
        Transform anchorTransform,
        Vector3 anchorPositionOffset,
        Vector3? preferredActorPosition)
    {
        if (preferredActorPosition.HasValue &&
            TryGetPreferredYaw(
                anchorTransform, anchorPositionOffset, preferredActorPosition.Value, out float yaw))
        {
            return yaw;
        }

        return 0f;
    }

    static bool TryGetPreferredYaw(
        Transform anchorTransform,
        Vector3 anchorPositionOffset,
        Vector3 preferredActorPosition,
        out float preferredYaw)
    {
        preferredYaw = 0f;
        if (anchorTransform == null)
            return false;

        Vector3 baseDir = anchorTransform.rotation * anchorPositionOffset;  // ใช้ anchor.rotation เสมอ
        baseDir.y = 0f;
        if (baseDir.sqrMagnitude < MinPlanarSqrMagnitude)
            return false;   // offset ไม่มีระยะ XZ

        Vector3 actorDir = preferredActorPosition - anchorTransform.position;
        actorDir.y = 0f;
        if (actorDir.sqrMagnitude < MinPlanarSqrMagnitude)
            return false;   // actor ซ้อน anchor

        preferredYaw = Vector3.SignedAngle(baseDir, actorDir, Vector3.up);
        return true;
    }
}
