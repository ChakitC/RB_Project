using System;
using System.Collections.Generic;

/// <summary>
/// Tag กลางที่ระบบอื่นใช้ค้นหา StatusEffectDef โดยไม่ต้องถือ reference ไปยัง Def ตัวใดตัวหนึ่ง —
/// ทำให้มี Taunt Def ได้หลายตัว (คนละ VFX/คนละ modifier) แต่ยังแข่งขันกันเป็น "taunt" เดียวกัน.
/// </summary>
public static class StatusEffectTags
{
    /// <summary>ติดบน StatusEffectDef ทุกตัวที่ทำให้เป้าหมายถูกยั่วให้หันมาหาผู้ลง (AITargetSensor อ่านค่านี้).</summary>
    public const string Taunt = "Taunt";

    public static bool Has(StatusEffectDef definition, string tag)
    {
        if (definition == null || string.IsNullOrWhiteSpace(tag))
            return false;

        List<string> tags = definition.tags;
        if (tags == null)
            return false;

        for (int i = 0; i < tags.Count; i++)
        {
            if (string.Equals(tags[i], tag, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
