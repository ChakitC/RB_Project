using System;
using UnityEngine;

[Serializable]
public sealed class ThirdPersonCharacterProfile
{
    [Header("Camera Pivot")]
    public Vector3 pivotOffset = new(0f, 1.55f, 0f);
    public Vector3 shoulderOffset = new(0.48f, -0.2f, 0f);
    [Min(0f)] public float verticalArmLength = 0.25f;
    [Min(0.1f)] public float cameraDistance = 3.2f;
    [Min(0.1f)] public float aimCameraDistance = 2.7f;
    [Range(0f, 1f)] public float cameraSide = 1f;
    public Vector3 followDamping = new(0.08f, 0.12f, 0.08f);

    [Header("View")]
    [Range(-89f, 0f)] public float minimumPitch = -55f;
    [Range(0f, 89f)] public float maximumPitch = 72f;
    [Range(20f, 100f)] public float freeLookFov = 60f;
    [Range(20f, 100f)] public float shoulderAimFov = 48f;
    [Min(0.01f)] public float yawSensitivityMultiplier = 1f;
    [Min(0.01f)] public float pitchSensitivityMultiplier = 1f;

    [Header("Upper Body Aim")]
    [Range(0f, 1f)] public float chestAimWeight = 0.55f;
    [Range(0f, 1f)] public float upperChestAimWeight = 0.7f;
    [Range(0f, 1f)] public float spineAimWeight = 0.35f;
    [Range(0f, 90f)] public float maximumUpperBodyPitch = 55f;
    [Range(0f, 90f)] public float maximumUpperBodyYaw = 65f;

    [Header("Camera Occlusion")]
    [Min(0.05f)] public float collisionRadius = 0.22f;
    [Min(0f)] public float fadeStartDistance = 1.05f;
    [Min(0f)] public float fadeFullyHiddenDistance = 0.5f;

    public static ThirdPersonCharacterProfile CreateDefault()
    {
        return new ThirdPersonCharacterProfile();
    }
}
