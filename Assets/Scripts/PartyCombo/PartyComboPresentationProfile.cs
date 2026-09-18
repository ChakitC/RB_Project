using System;
using UnityEngine;

[Serializable]
public sealed class PartyComboPresentationProfile
{
    [Tooltip("เปิดเอฟเฟกต์โลกช้าและกล้องโฟกัส Ally ระหว่างร่าย Combo จนถึงจุดปล่อยสกิล สกิลที่ปล่อยทันทีจะข้ามเอฟเฟกต์นี้")]
    public bool enabled = true;
    [Tooltip("ความเร็วโลกตอนเริ่มร่าย: 1 = ปกติ, 0.25 = หนึ่งในสี่ โดย Ally ผู้ร่ายไม่ถูกทำให้ช้า ค่ายิ่งต่ำโลกยิ่งช้า")]
    [Range(0.05f, 1f)] public float initialWorldScale = 0.25f;
    [Tooltip("กราฟคืนความเร็วโลก: แกน X คือความคืบหน้าจากเริ่มร่ายถึงจุดปล่อยสกิล (0–1) แกน Y คือสัดส่วนการคืนความเร็ว (0 = ช้าตามค่าเริ่มต้น, 1 = ปกติ) เมื่อถึงจุดปล่อยสกิลจะคืนความเร็วปกติ")]
    public AnimationCurve recoveryCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [Tooltip("เวลาเลื่อนกล้องเข้าโฟกัส Ally หน่วยวินาที ค่าน้อยเข้าหาเร็ว ค่ามากเข้าหานุ่มนวล หากสกิลปล่อยเร็วกว่าช่วงนี้ อาจเห็นการซูมเพียงเล็กน้อย")]
    [Min(0.01f)] public float cameraBlendInSeconds = 0.15f;
    [Tooltip("เวลาค้างโฟกัสที่ Ally หลังถึงจุดปล่อยสกิล ก่อนเลื่อนกล้องกลับ หน่วยวินาที ระหว่างนี้กล้องยังเลื่อนเข้าได้ ค่า 0 = ไม่ค้าง ไม่นับช่วง Pause และไม่ยืดโลกช้าหรือจังหวะออกสกิล การยกเลิกก่อนปล่อยสกิลจะข้ามช่วงค้าง")]
    [Min(0f)] public float cameraHoldSeconds = 0.25f;
    [Tooltip("เวลาเลื่อนกล้องกลับหา Player เมื่อปล่อยสกิลหรือยกเลิก หน่วยวินาที ค่าน้อยกลับเร็ว ค่ามากกลับช้าลง")]
    [Min(0.01f)] public float cameraBlendOutSeconds = 0.25f;
    [Tooltip("สัดส่วนการเลื่อนจุดโฟกัสจาก Player ไปหา Ally และเป้าหมาย: 0 = อยู่ที่ Player, 1 = เลื่อนไปเต็มระยะที่คำนวณได้ แต่ยังถูกจำกัดด้วย Maximum Focus Offset")]
    [Range(0f, 1f)] public float allyFocusWeight = 0.6f;
    [Tooltip("สัดส่วนผสมตำแหน่งเป้าหมายเข้ากับจุดโฟกัส Ally: 0 = เน้น Ally, 0.5 = กึ่งกลางระหว่างทั้งคู่, 1 = เน้นเป้าหมาย")]
    [Range(0f, 1f)] public float targetFocusWeight = 0.25f;
    [Tooltip("ระยะสูงสุดที่จุดโฟกัสเลื่อนออกจากจุดติดตาม Player หน่วยระยะโลก ค่าสูงเลื่อนไปหา Ally ได้ไกลขึ้น ไม่ใช่ระยะห่างตัวกล้อง")]
    [Min(0f)] public float maximumFocusOffset = 4f;
    [Tooltip("ระยะห่างสูงสุดระหว่าง Player กับ Ally ที่อนุญาตให้กล้องโฟกัส หน่วยระยะโลก หากไกลกว่านี้จะข้ามหรือจบการโฟกัส แต่ส่วนโลกช้ายังทำงานได้")]
    [Min(0f)] public float maximumActorDistance = 12f;
    [Tooltip("จำนวนองศาที่ลดจากมุมมองกล้อง (FOV) ระหว่างโฟกัส ค่ายิ่งมากภาพยิ่งซูมเข้า ค่า 0 ไม่ซูมแต่ยังเลื่อนโฟกัสได้ โดย FOV สุดท้ายไม่ต่ำกว่า 20 องศา")]
    [Range(0f, 15f)] public float fieldOfViewReduction = 4f;

    public float EvaluateWorldScale(float progress)
    {
        progress = Mathf.Clamp01(progress);
        if (progress >= 1f)
            return 1f;
        float recovery = recoveryCurve != null && recoveryCurve.length > 0
            ? Mathf.Clamp01(recoveryCurve.Evaluate(progress))
            : progress;
        return Mathf.Lerp(Mathf.Clamp(initialWorldScale, 0.05f, 1f), 1f, recovery);
    }
}
