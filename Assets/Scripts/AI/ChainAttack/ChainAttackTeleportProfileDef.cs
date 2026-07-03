using UnityEngine;

[CreateAssetMenu(fileName = "ChainAttackTeleportProfile", menuName = "Game/Chain Attack/Teleport Profile")]
public sealed class ChainAttackTeleportProfileDef : ScriptableObject
{
    [Header("Teleport")]
    [Tooltip("ใช้ rotation ของ target anchor เป็น facing ฐาน ปิดเพื่อใช้ rotation ปัจจุบันของ actor เป็นฐานแทน")]
    public bool useAnchorRotationAsBase = true;
    [Tooltip("บังคับให้จุด teleport ที่ resolve ได้อยู่ใกล้ NavMesh เมื่อมี probe collider จะเช็ค footprint ของ probe กับ NavMesh ด้วย")]
    public bool requireNavMeshAtAnchor = true;
    [Tooltip("ระยะสูงสุดที่ NavMesh.SamplePosition ใช้ตอนดึงจุด teleport ดิบให้ไปติด NavMesh")]
    [Min(0.05f)] public float navMeshSampleDistance = 0.75f;
    [Tooltip("offset (local) จาก target anchor ก่อนใส่ yaw candidates ถ้าเป็นศูนย์ yaw จะเปลี่ยนแค่ rotation และตำแหน่งจะอยู่ที่ anchor")]
    public Vector3 anchorPositionOffset = Vector3.zero;

    [Header("Obstacle / Clearance")]
    [Tooltip("offset จุดศูนย์กลาง (local) ของ legacy clearance box เทียบกับ pose teleport ที่ resolve ได้")]
    public Vector3 clearanceCenterOffset = new Vector3(0f, 1f, 0.45f);
    [Tooltip("half extents ของ legacy clearance box ตั้งแกนใดแกนหนึ่งเป็นศูนย์เพื่อปิด clearance box")]
    public Vector3 clearanceHalfExtents = new Vector3(0.35f, 0.9f, 0.75f);
    [Tooltip("เลเยอร์ที่นับเป็นสิ่งกีดขวางสำหรับการเช็ค overlap, path และ clearance ปล่อยเป็น Nothing เพื่อข้ามการบล็อกด้วย obstacle layer แล้วพึ่งแค่ NavMesh/probe")]
    public LayerMask obstacleLayers = 0;
    [Tooltip("กำหนดว่าการเช็ค obstacle overlap จะรวม trigger collider หรือไม่")]
    public QueryTriggerInteraction obstacleTriggerInteraction = QueryTriggerInteraction.Ignore;

    [Header("Debug")]
    [Tooltip("log การทดสอบ teleport candidate, การ sample NavMesh, การชน obstacle และเหตุผลที่ reject สำหรับ profile นี้")]
    public bool debugLogging;

    public bool HasClearanceProbe =>
        obstacleLayers != 0 &&
        clearanceHalfExtents.x > 0f &&
        clearanceHalfExtents.y > 0f &&
        clearanceHalfExtents.z > 0f;
}
