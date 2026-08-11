using System;

/// <summary>
/// การลง status หนึ่งรายการที่ถูก gate ด้วย upgrade id — โครงที่เดิมถูกประกาศซ้ำเป็น nested
/// <c>ConditionalStatus</c> ในทุก payload/step ที่ลง status ได้.
///
/// ค่าบาลานซ์ทั้งหมดอยู่ใน <see cref="StatusApplicationSpec"/> ตามกติกา "magnitude at apply site".
/// </summary>
[Serializable]
public sealed class ConditionalStatusApplication
{
    public string requiredUpgradeId;

    public StatusApplicationSpec spec = new();
}
