/// <summary>
/// ใครโดน status ที่ลงผ่าน conditional status route หนึ่งเส้น.
///
/// เป็น runtime enum (ไม่ใช่ editor-only) เพราะ payload/step ประกาศ target ของ route ด้วย
/// <see cref="SkillStatusRouteTargetAttribute"/> ไว้ข้าง field ที่ serialize จริง — editor tooling
/// อ่านค่านี้จากตัว declaration แทนการ hard-code ชนิด payload ไว้ใน resolver.
/// </summary>
public enum SkillStatusTarget
{
    Self,
    Allies,
    TauntedEnemies,
}
