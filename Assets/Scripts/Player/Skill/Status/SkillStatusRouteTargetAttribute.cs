using System;

/// <summary>
/// ประกาศว่า <see cref="ConditionalStatusRoute"/> field นี้ลง status ให้ใคร.
///
/// นี่คือ source of truth เดียวของ target: resolver, scanner, wizard และ inspector drawer อ่าน
/// attribute นี้ทั้งหมด — ไม่มีที่ไหนเช็ก concrete type ของ payload/step อีก. Payload/Step ใหม่ที่
/// ใช้ target เดิมจึงเพิ่มแค่ field + attribute โดยไม่ต้องแก้ tooling ส่วนกลาง.
///
/// มีสองรูปแบบ:
/// - target คงที่: <c>[SkillStatusRouteTarget(SkillStatusTarget.Self, "Apply Status")]</c>
/// - target ขึ้นกับ behavior: <c>[SkillStatusRouteTarget(nameof(ResolvedTarget), "Heal Area")]</c>
///   โดย member ที่อ้างต้องเป็น property/field/method ไม่มีพารามิเตอร์ที่คืน
///   <see cref="SkillStatusTarget"/> และประกาศอยู่บนชนิดเดียวกับ field (public หรือ private ก็ได้).
/// </summary>
[AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
public sealed class SkillStatusRouteTargetAttribute : Attribute
{
    public SkillStatusRouteTargetAttribute(SkillStatusTarget target, string label = null)
    {
        FixedTarget = target;
        Label = label;
    }

    public SkillStatusRouteTargetAttribute(string targetMemberName, string label = null)
    {
        TargetMemberName = targetMemberName;
        Label = label;
    }

    /// <summary>Target คงที่ — null เมื่อ route อ่าน target จาก <see cref="TargetMemberName"/>.</summary>
    public SkillStatusTarget? FixedTarget { get; }

    /// <summary>ชื่อ member ที่คืน target — null เมื่อ route ใช้ <see cref="FixedTarget"/>.</summary>
    public string TargetMemberName { get; }

    /// <summary>ป้ายที่โชว์ใน destination selector / inspector. null = ใช้ชื่อชนิดที่ประกาศ field.</summary>
    public string Label { get; }
}
