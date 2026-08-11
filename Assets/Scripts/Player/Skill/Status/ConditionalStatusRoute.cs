using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// รายการ status ที่ปลดล็อกด้วย upgrade id ของจุดหนึ่งใน skill (payload หรือ step).
///
/// Route ไม่หา actor เอง และไม่รู้ว่าตัวเองลงให้ใคร — เจ้าของ behavior เป็นคนหา
/// <see cref="StatusEffectController"/>, source และ fallback duration แล้วค่อยเรียก
/// <see cref="ApplyUnlocked"/> ตอนที่ควรลง status จริง. ส่วน "ลงให้ใคร" ประกาศไว้ที่ field
/// ด้วย <see cref="SkillStatusRouteTargetAttribute"/> สำหรับ editor tooling อ่าน.
/// </summary>
[Serializable]
public sealed class ConditionalStatusRoute
{
    /// <summary>ชื่อ field ของ list — ใช้ประกอบ property path ฝั่ง editor.</summary>
    public const string ApplicationsFieldName = "applications";

    [SerializeField]
    private List<ConditionalStatusApplication> applications = new();

    public IReadOnlyList<ConditionalStatusApplication> Applications => applications;

    /// <summary>มี entry ที่ชี้ StatusEffectDef จริงอย่างน้อยหนึ่งตัวไหม.</summary>
    public bool HasConfiguredApplication
    {
        get
        {
            for (int i = 0; applications != null && i < applications.Count; i++)
            {
                if (applications[i]?.spec?.effect != null)
                    return true;
            }

            return false;
        }
    }

    /// <summary>
    /// ลง status ทุกตัวที่ upgrade id ถูกปลดล็อกแล้ว. คืน true ถ้าลงอย่างน้อยหนึ่งตัว —
    /// caller บางตัวใช้ค่านี้ตัดสินว่า payload ถูก author ไว้ครบหรือเปล่า.
    /// </summary>
    public bool ApplyUnlocked(
        SkillCastContext context,
        StatusEffectController controller,
        GameObject source,
        float fallbackDuration)
    {
        if (context == null || controller == null || applications == null)
            return false;

        bool appliedAny = false;
        for (int i = 0; i < applications.Count; i++)
        {
            ConditionalStatusApplication application = applications[i];
            if (application == null || !context.HasUpgrade(application.requiredUpgradeId))
                continue;

            StatusApplicationSpec spec = application.spec;
            if (spec?.effect == null)
                continue;

            controller.ApplyEffect(spec, source, fallbackDuration);
            appliedAny = true;
        }

        return appliedAny;
    }

    public void CollectUpgradeIds(List<string> ids)
    {
        for (int i = 0; applications != null && i < applications.Count; i++)
        {
            ConditionalStatusApplication application = applications[i];
            if (application != null)
                SkillUpgradeIdCollection.AddUnique(ids, application.requiredUpgradeId);
        }
    }

    public void CollectValidationIssues(List<string> issues, string contextLabel)
    {
        if (issues == null || applications == null)
            return;

        // แถวที่ยังว่างทั้งแถว = กำลัง author อยู่ ไม่ใช่ error — เช็คเฉพาะแถวที่ชี้ Def แล้ว
        // (กติกาเดียวกับที่ ApplyStatusSkillPayloadDef ใช้อยู่เดิม)
        for (int i = 0; i < applications.Count; i++)
        {
            ConditionalStatusApplication application = applications[i];
            if (application?.spec?.effect == null)
                continue;

            if (string.IsNullOrWhiteSpace(application.requiredUpgradeId))
            {
                issues.Add(
                    $"{contextLabel}[{i}]: conditional status '{application.spec.effect.name}' has no " +
                    "required upgrade id — it would never unlock.");
            }

            application.spec.CollectValidationIssues(issues, $"{contextLabel}[{i}]");
        }
    }
}
