using UnityEngine;

/// <summary>
/// ค่าจูนทั้งหมดของ dash + perfect dodge ตัว <see cref="DashSystem"/> ไม่เก็บค่าพวกนี้เองแล้ว
/// เพื่อไม่ให้มี source of truth สองทาง (asset กับ prefab) แบบเดิม
/// </summary>
[CreateAssetMenu(menuName = "Game/Characters/Dash Setting", fileName = "DashSetting")]
public sealed class DashSetting : ScriptableObject
{
    [Header("Dash")]
    [Min(0f)] public float dashDistance = 5f;
    [Min(0.01f)] public float dashDuration = 0.15f;
    [Min(0f)] public float dashInvincibleTime = 0.15f;
    [Min(0f)] public float dashCost = 10f;
    public LayerMask obstacleMask = ~0;

    [Header("IFrame")]
    [Tooltip("เลเยอร์ที่ปิดการชนระหว่าง dash")]
    public LayerMask dashIFrameExclude;

    [Header("Perfect Dodge")]
    [Tooltip("ช่วงต้นของ i-frame ที่นับเป็น perfect dodge (วินาที) ตั้ง 0 = ใช้ทั้ง dashInvincibleTime")]
    [Min(0f)] public float perfectDodgeWindow = 0.15f;

    [Tooltip("world time scale ตอนสโลว์ 0.2 = ศัตรูช้าลงเหลือ 20%")]
    [Range(0.05f, 1f)] public float perfectDashSlowScale = 0.2f;

    [Min(0f)] public float perfectDashSlowDuration = 0.35f;

    [Tooltip("รูปทรงของสโลว์ตามเวลา 1 = สโลว์เต็ม, 0 = ความเร็วปกติ ปล่อยว่าง = สโลว์เต็มตลอดช่วง")]
    public AnimationCurve perfectDashSlowShape = AnimationCurve.Constant(0f, 1f, 1f);

    [Tooltip("ยิงไม่กินกระสุนกี่วินาทีหลังหลบสำเร็จ")]
    [Min(0f)] public float perfectDashFreeAmmoDuration = 0.35f;

    [Tooltip("ตัวละครนี้ได้ world slow + เอฟเฟคจอตอนหลบสำเร็จมั้ย " +
             "เปิดเฉพาะผู้เล่น AI ที่ dash ได้ควรปิด ไม่งั้นจอผู้เล่นจะมืดตอน AI หลบ")]
    public bool playsPerfectDodgeFeedback = true;

    [Tooltip("หน้าตาของจอตอนสโลว์ ปล่อยว่าง = สโลว์อย่างเดียวไม่มีเอฟเฟคจอ")]
    public WorldSlowPostFxSetting worldSlowVisual;

    [Tooltip("เลเยอร์ที่ถือว่าเป็นภัยคุกคามตอนสแกนรอบตัว ปล่อยว่าง (Nothing) = ปิดการสแกน")]
    public LayerMask perfectDodgeThreatLayers;

    [Min(0f)] public float perfectDodgeThreatScanPadding = 0.25f;
}
