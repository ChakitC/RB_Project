using UnityEngine;

[UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: null, sourceAssembly: "Assembly-CSharp", sourceClassName: "PartyComboPlacementPolicy")]
public enum PartyFollowUpPlacementPolicy
{
    KeepCurrentPosition = 0,
    PlaceNearEventTarget = 1,
    AlignPlacementPointToEventTarget = 2,
}

[UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: null, sourceAssembly: "Assembly-CSharp", sourceClassName: "PartyComboExitPolicy")]
public enum PartyFollowUpExitPolicy
{
    KeepAtCurrentPosition = 0,
    ReturnToRecordedOrigin = 1,
}

[CreateAssetMenu(fileName = "PartyFollowUpExecution", menuName = "Game/Party Follow-Up/Execution Profile")]
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: null, sourceAssembly: "Assembly-CSharp", sourceClassName: "PartyComboExecutionProfile")]
public sealed class PartyFollowUpExecutionProfile : ScriptableObject
{
    [Header("Placement")]
    [Tooltip("KeepCurrentPosition = อยู่ที่เดิม; PlaceNearEventTarget = วาร์ปตามระยะ; AlignPlacementPointToEventTarget = คำนวณจุดเริ่มจาก Root Motion เพื่อถึงระยะที่กำหนดตอน Placement Point")]
    public PartyFollowUpPlacementPolicy placementPolicy = PartyFollowUpPlacementPolicy.KeepCurrentPosition;
    [Tooltip("ระยะจากเป้าหมาย หน่วยโลก: PlaceNearEventTarget ใช้ที่จุดเริ่ม S; AlignPlacementPointToEventTarget ใช้ที่ Placement Point A แล้วคำนวณย้อนหาจุดเริ่ม S")]
    [Min(0f)] public float desiredRange = 2f;
    [Tooltip("ระยะเยื้องอิงแกนและสเกลของเป้าหมาย ใช้ที่จุดเริ่ม S ในโหมดวาร์ป หรือจุด A ในโหมด Placement Point")]
    public Vector3 localOffset;
    [Tooltip("กำหนดให้หาจุดบน NavMesh ทั้งนี้ FollowUp ของตัวละครที่เคลื่อนที่ได้ยังบังคับใช้ NavMesh ตามกติกา placement กลาง แม้ปิดค่านี้")]
    public bool requireNavMesh = true;
    [Tooltip("ระยะค้นหาจุด NavMesh ใกล้ตำแหน่งที่เสนอ หน่วยระยะโลก ค่าสูงยอมให้จุดวาร์ปขยับจากตำแหน่งเดิมได้ไกลขึ้น")]
    [Min(0.05f)] public float navMeshSampleDistance = 1f;
    [Tooltip("ตรวจและตัดจุดวาร์ปที่ตัว Ally ทะลุสิ่งกีดขวางออก หากไม่มีจุดปลอดภัยจะไม่เริ่ม FollowUp ค่านี้ตรวจการชนของตำแหน่งยืน แยกจากการถูกบังในกล้อง")]
    public bool requireUnobstructedPosition = true;
    [Tooltip("จำนวนจุดที่ลองรอบเป้าหมายต่อการวาร์ป (1–16) ค่ามากเพิ่มโอกาสเจอมุมที่เห็นชัด แต่เพิ่มการตรวจ NavMesh และแนวสายตา ค่าเริ่มต้น 8 จุด")]
    [Range(1, 16)] public int placementCandidateCount = 8;
    [Tooltip("เลเยอร์ของ collider ที่บังสายตากล้อง ควรรวมฉาก ตัวเป้าหมาย และตัวละครอื่น ระบบไม่นับ Trigger เช่นเซนเซอร์ และข้ามตัว Ally ผู้ร่ายที่ตำแหน่งเก่า")]
    public LayerMask visibilityObstructionLayers = ~0;

    [Header("Execution")]
    [Tooltip("เวลาสูงสุดที่รอให้การร่ายยืนยันการใช้สกิลสำเร็จ (commit) หน่วยวินาที ไม่นับช่วง Pause หากเกินเวลาจะยกเลิกการร่ายที่ยังไม่ปล่อยสกิล ควรยาวกว่าช่วงเตรียมร่าย")]
    [Min(0.1f)] public float startTimeoutSeconds = 2f;
    [Tooltip("เวลาสูงสุดที่รอแอนิเมชันหลัง commit จบก่อนคืนการควบคุมให้ Ally หน่วยวินาที ไม่นับช่วง Pause ควรยาวพอสำหรับช่วงจบท่าสกิล")]
    [Min(0.1f)] public float recoveryTimeoutSeconds = 4f;
    [Tooltip("อนุญาตการป้องกัน Ally ระหว่าง FollowUp เช่นอมตะหรือไม่ถูกเลือกเป็นเป้า โดยใช้ตัวเลือกการป้องกันที่ตั้งไว้ใน FieldAllyMember")]
    public bool protectActorDuringExecution = true;
    [Tooltip("ตำแหน่งหลังจบหรือ cleanup: KeepAtCurrentPosition = อยู่ตำแหน่งปัจจุบัน, ReturnToRecordedOrigin = กลับตำแหน่งและทิศทางก่อนเริ่ม FollowUp")]
    public PartyFollowUpExitPolicy exitPolicy = PartyFollowUpExitPolicy.ReturnToRecordedOrigin;

    [Header("Presentation")]
    [Tooltip("ตั้งค่าโลกช้า การโฟกัส และการซูมกล้องของ FollowUp ทุกสกิลที่อ้างอิง Execution Profile นี้จะใช้ค่าชุดเดียวกัน")]
    public PartyFollowUpPresentationProfile presentation = new PartyFollowUpPresentationProfile();
}
