# Status Application Spec Unification Plan

## Purpose

ทำให้ทุกระบบที่นำ Status ไปใช้ถือ `StatusEffectDef` เป็นตัวตน/กติกากลาง และถือ
`StatusApplicationSpec` เป็นค่าบาลานซ์ของ application นั้น เพื่อให้ Skill, Passive,
Projectile และ Pickup ใช้ Def เดิมร่วมกันได้โดยไม่ต้องสร้าง Status asset ใหม่เพียงเพราะ
ความแรงหรือระยะเวลาไม่เท่ากัน

เอกสารนี้เป็น handoff สำหรับ implementation section ถัดไป ยังไม่ใช่หลักฐานว่างานด้านล่าง
ถูก implement ครบแล้ว

## Agreed Product Rules

### Ownership

`StatusEffectDef` เป็นเจ้าของ:

- identity (`effectId`), icon, category และ VFX
- `stackMode` และ `maxStacks`
- control blocks, locomotion/stunned behavior
- tags และ trigger rules
- ค่าบาลานซ์ตั้งต้นสำหรับ application ที่ยังไม่ได้ override

`StatusApplicationSpec` เป็นเจ้าของค่าราย application:

- initial stacks
- modifiers
- duration
- tick damage/heal
- tick interval

ห้าม override `stackMode`, `maxStacks`, control behavior, tags, trigger rules หรือ presentation
จาก Skill หากพฤติกรรมเหล่านี้ต่างกันให้สร้าง `StatusEffectDef` คนละตัว

### Inheritance And Override

- Entry ใหม่เริ่มจากการติดตามค่าของ `StatusEffectDef`
- การแก้ครั้งแรกใน Inspector ต้องคัดลอกค่าที่เห็นมาเป็น application override โดยอัตโนมัติ
- Override เป็นความสามารถ ไม่ใช่ข้อบังคับ; entry ที่ไม่แก้ยังตาม Def ต่อไป
- Modifiers รองรับ explicit empty override ซึ่งหมายถึง application นี้ไม่มี stat modifier
- Duration, tick damage และ tick interval ต้องมีสถานะ override แยก ห้ามใช้ `0` เป็น sentinel
- เมื่อ override เปิด ค่า `0` คือค่าจริง:
  - duration `0` = permanent
  - tick damage `0` = ปิด damage/heal
  - tick interval `0` = ปิด tick
- ทุก channel ต้องมี Reset กลับไปใช้ fallback

Duration resolution order:

1. application duration override (รวมค่า `0`)
2. `FinalSkillStats.effectDuration` จาก Skill/Upgrade Tree หากมี
3. `StatusEffectDef.duration`

### Status Identity And Competition

- Applications ที่ใช้ Def เดียวกันถือเป็น Status identity เดียวกัน
- การ refresh, stack, strongest-only หรือ independent ใช้กฎจาก Def
- ความแรงที่ต่างกันอย่างเดียวไม่ใช่เหตุผลให้สร้าง Def ใหม่
- หากสอง Status ต้องอยู่ร่วมกันโดยไม่แข่งขันกัน ให้ใช้ Def คนละตัวเพราะเป็นคนละ identity

## Current Repository State To Preserve

มีงานที่ทำไว้แล้วใน working tree และต้องตรวจ/ต่อยอด ห้ามย้อนทิ้ง:

- `Assets/Scripts/StatusEffects/StatusApplicationSpec.cs`
  มี explicit modifier override state แล้ว
- `Assets/Scripts/Editor/StatusApplicationSpecDrawer.cs`
  แสดง Def modifiers, clone-on-first-edit, add/remove/reorder, reset และ effect-change prompt
- `Docs/SYSTEMS/SKILL_SYSTEM.md`
  มีคำอธิบาย modifier override Inspector
- `Assets/Scripts/Editor/LegacyStatusSpecMigrationTool.cs`
  มี migration tool รุ่นปัจจุบันอยู่ใน working tree แต่ยังย้ายได้เฉพาะ legacy flat fields

Repository มี unrelated dirty files อยู่แล้ว ให้แตะเฉพาะไฟล์/asset ที่ migration รายงานและห้าม
revert งานของผู้ใช้หรือ section อื่น

## Target Runtime Model

### StatusApplicationSpec

คง fields ที่ serialize ได้ตรงและอ่าน YAML ง่าย ไม่สร้าง generic optional abstraction:

- `StatusEffectDef effect`
- `int stacks`
- `List<StatusEffectModifier> modifiers`
- hidden serialized `bool modifiersOverrideEnabled`
- `float durationOverride` + hidden serialized enable flag
- `float tickDamageOverride` + hidden serialized enable flag
- `float tickIntervalOverride` + hidden serialized enable flag

Backward compatibility rule ระหว่าง migration เท่านั้น:

- modifiers list เก่าที่ non-empty ต้องถือว่า override แม้ enable flag ยังเป็น false
- หลัง migrate/force-reserialize สำเร็จ ให้ข้อมูลใหม่เขียน enable flag ชัดเจน

เพิ่ม API/query ที่สื่อความหมายตรง เช่น:

- `HasModifierOverride`
- `HasDurationOverride`
- `HasTickDamageOverride`
- `HasTickIntervalOverride`
- resolver ที่รับ skill-duration fallback โดยไม่ mutate serialized spec

อย่าคืน list จาก Def แล้วเปิดให้ runtime แก้โดย reference; resolved modifier list ของ instance ต้องเป็น copy
เหมือน invariant ปัจจุบัน

### StatusEffectInstance And Controller

- `StatusEffectInstance` resolve modifiers, duration, tick damage และ tick interval ครั้งเดียวจาก Spec
- `ShouldTick`/`AdvanceTick` ต้องอ่าน resolved tick interval ของ instance ไม่อ่าน Def ตรง
- `ResolvedTickDamage` และ resolved modifiers ยังคงเป็นค่าราย instance
- `StatusEffectController.ApplyEffect(StatusApplicationSpec, ...)` เป็น API หลักสำหรับ apply sites
- overload ที่รับ Def ตรงเก็บไว้เฉพาะระบบที่ไม่ใช่ authored application และพิสูจน์แล้วว่าต้องใช้;
  Skill/Passive/Pickup/Projectile ห้ามเรียก overload นี้
- refactor duration fallback ให้ส่งเข้า controller/resolver อย่างชัดเจน แทนการใช้ sentinel `0`
- Stack cap และ stack mode ยังคงอ่านจาก Def

## Unify Every Apply Site

### Already Uses StatusApplicationSpec

ตรวจ regression และเปลี่ยนไปใช้ scalar override API ใหม่:

- `ApplyStatusSkillPayloadDef` ทั้ง unconditional/conditional
- `ApplyStatusOnHitModule`
- conditional status ของ `TauntSkillPayloadDef`
- conditional status ของ `HealAreaStep`
- `PassiveActionDefinition`
- `ApplyStatusPickupEffectDef`
- weapon reload buff runtime spec

### Morph

ไฟล์หลัก:

- `Assets/Scripts/Player/Skill/Payloads/MorphSkillPayloadDef.cs`
- `Assets/Scripts/Player/Skill/Payloads/MorphSkillRuntime.cs`

เปลี่ยน `MorphStatusApplication` จาก `StatusEffectDef + stacks` เป็น `StatusApplicationSpec` และให้
runtime เรียก Spec overload เท่านั้น รายการ Def ที่ใช้ถอดตอน revert ยังคง derive จาก instance/spec
ที่ apply สำเร็จ ห้ามอาศัยการแก้ Def

### HealArea

ไฟล์หลัก:

- `Assets/Scripts/Player/Skill/Steps/HealAreaStep.cs`

เปลี่ยน unconditional `List<StatusEffectDef>` เป็นรายการ wrapper ที่ถือ `StatusApplicationSpec`
เหมือน conditional list ทั้งสองเส้นทางต้องใช้ duration precedence เดียวกัน

### Taunt As Skill-Owned Status

ไฟล์หลัก:

- `Assets/Scripts/Player/Skill/Payloads/TauntSkillPayloadDef.cs`
- `Assets/Scripts/Player/Skill/Payloads/TauntSkillRuntime.cs`
- `Assets/Scripts/AI/Ai Taget And Sensor/AITargetSensor.cs`
- `Assets/Scripts/StatusEffects/StatusEffectController.cs`
- `Assets/Scripts/StatusEffects/StatusEffectInstance.cs`

เป้าหมาย:

- เพิ่ม primary `StatusApplicationSpec` บน Taunt payload
- ย้าย `Taunted` Def ownership ออกจาก serialized field ของ `AITargetSensor`
- Skill apply primary taunt Spec ไปยัง `StatusEffectController` ของเป้าหมาย
- Taunt Def ทุกตัวใช้ tag กลางที่ประกาศเป็น constant เดียว เช่น `StatusEffectTags.Taunt`
- Taunt validation ต้อง error หาก Def ไม่มี tag, `separatePerSource` ไม่เป็น true หรือ stack mode
  ไม่ใช่ `RefreshDuration`

กติกาการแข่งขัน:

- Taunt application ล่าสุดเป็นผู้ชนะ
- เมื่อ instance ล่าสุดหมด ให้ย้อนกลับไป instance ก่อนหน้าที่ยัง active
- ผู้ใช้ Skill คนเดิม refresh instance ของตัวเอง
- ผู้ใช้คนละคนเก็บคนละ instance ผ่าน `separatePerSource`

เพื่อให้ deterministic ให้เพิ่ม monotonic application/refresh sequence บน `StatusEffectInstance`
หรือข้อมูลเทียบลำดับที่เทียบได้แน่นอน ห้ามใช้ลำดับ list โดยบังเอิญ หาก instance ถูก refresh ต้องถือว่า
เป็น Taunt ล่าสุด

`AITargetSensor` ต้อง derive taunt source จาก active tagged instances และ `instance.Source` ทุกครั้งที่
resolve/cache invalidates ห้ามเก็บ Taunt Def เฉพาะตัว เมื่อ source ถูก destroy หรือไม่ resolve เป็น
`CharacteContext` ที่ valid ให้ข้ามไปตัวถัดไป

ขณะแตะ Taunt targeting ให้ทำตาม project rule: discover character actors ผ่าน active
`CharacteContext` และตรวจระยะจาก context; ใช้ physics เฉพาะ layer/line-of-sight/obstruction ที่ต้องอาศัย
collider geometry

ตรวจ callers ของ `AITargetSensor.ApplyTaunt`; ถ้าเหลือเฉพาะ `TauntSkillRuntime` ให้ลบ API เก่าเมื่อ
migration สำเร็จ ไม่เก็บ dead compatibility API

## Inspector Requirements

ต่อยอด `StatusApplicationSpecDrawer` ให้ใช้ทุก apply site ผ่าน `[CustomPropertyDrawer]`:

- แสดงสถานะ `Using Status Effect Def Values` หรือ `Using Application Override`
- Modifiers แสดงค่าจาก Def แต่แก้ครั้งแรกแล้ว clone ทั้ง list
- เพิ่ม/ลบ/เรียงได้ และ override list ว่างได้
- Duration, tick damage และ tick interval แสดง resolved source/value ชัดเจน
- แก้ scalar ครั้งแรกต้องเปิด flag และเก็บค่าเดียวกับที่ผู้ใช้แก้
- แต่ละ channel มี Reset; อาจมี Reset All แต่ห้ามแทน Reset ราย channel
- เปลี่ยน Effect ขณะมี override ต้องถาม Reset To New Def / Keep Override / Cancel
- หาก Keep Override ให้รักษาทุก channel ไม่ใช่เฉพาะ modifiers
- UI ต้องทำงานทั้ง Odin-backed inspector และหน้าที่วาดด้วย Unity `SerializedProperty`
- รองรับ Undo/Redo, dirty state และ prefab/asset serialization ตามปกติ

อย่าใช้ `[ShowInInspector]` property เป็นกลไกหลัก เพราะหน้าปัจจุบันบางจุดวาดผ่าน
`SerializedProperty` และจะไม่เห็น non-serialized Odin property

## Clean Migration Strategy

เป้าหมายสุดท้าย: schema เดียว ไม่มี hidden legacy fields, ไม่มี `ResolvedSpec()` fallback และไม่มี
one-off migration tool ค้างใน production source

### Phase 1: Transitional Schema

1. เพิ่ม target Spec fields/wrappers ใหม่โดยยังคง field เก่าชั่วคราว
2. ขยาย migration tool ให้รองรับ:
   - legacy `effect/stacks -> spec`
   - legacy `statusEffect/statusInitialStacks -> statusSpec`
   - Morph status list shape
   - HealArea unconditional status list shape
   - primary Taunt Def ที่เดิมอยู่บน `AITargetSensor` ไปยัง Taunt skill assets ที่เกี่ยวข้อง
3. Tool ต้อง scan main assets และ embedded sub-assets โดยกัน managed-reference cycle เหมือน tool ปัจจุบัน
4. Dry run รายงาน asset path, object/sub-asset, property path, source Def, stacks และ target path
5. ห้ามแก้ YAML ด้วย regex; ใช้ Unity serialization/AssetDatabase เพื่อรักษา managed references และ GUID

### Phase 2: Execute And Prove Migration

1. บันทึก dry-run report และจำนวน candidate/field/list entries
2. รัน migration ผ่าน Unity Editor
3. Save และ force-reserialize เฉพาะ asset ที่ migration แตะ
4. รัน dry run ซ้ำ ต้องเหลือ 0 legacy values
5. เปรียบเทียบก่อน–หลัง:
   - จำนวน status entries
   - Def GUID ต่อ entry
   - stacks และลำดับรายการ
   - conditional upgrade IDs
   - embedded payload ownership
6. เปิดตรวจอย่างน้อย skill assets ที่พบแล้ว:
   - `Assets/Data/Skills/Aires/Aires_Skill_2.asset`
   - `Assets/Data/Skills/Aires/Aires_Skill_3.asset`
7. หาก asset ใด migrate ไม่ครบ ให้หยุด ห้ามเข้าสู่ Phase 3

### Phase 3: Remove Legacy

หลัง Phase 2 ผ่านเท่านั้น:

- ลบ hidden legacy fields ทุก owner type
- ลบ `ResolvedSpec()`/`ResolvedStatusSpec()` fallback methods แล้วให้ callers อ่าน Spec โดยตรง
- ลบ Morph/Heal old list fields
- ลบ `AITargetSensor.tauntedEffectDef` และ direct Def apply path
- ลบ migration comments ที่ไม่จริงแล้ว
- force-reserialize touched assets อีกครั้งเพื่อให้ unknown YAML fields หลุดออก
- รัน repository search ยืนยันว่าไม่มีชื่อ legacy fields/old fallback methods ใน target modules
- ลบ migration tool ชั่วคราวเมื่อ final verification ผ่าน

## Validation And Tests

### Runtime Behavior

เพิ่ม/ขยาย tests หรือ smoke tests ให้ครอบคลุม:

- no override ใช้ Def modifiers/duration/tick damage/tick interval
- modifier edit clone แล้วไม่ mutate Def
- explicit empty modifier override ให้ resolved list ว่าง
- scalar override ค่า non-zero และ `0`
- duration precedence: application > skill stats > Def
- Reset กลับไปติดตาม Def
- Def เดียวกันต่าง magnitude แข่งขันตาม stack mode เดิม
- StrongestOnly comparison ใช้ resolved modifiers
- tick scheduler ใช้ resolved tick interval
- Morph apply/revert ถอดเฉพาะ Status ที่ตัวเองลง
- HealArea unconditional/conditional ใช้ Spec และ skill duration fallback ถูกต้อง

### Taunt

- Taunt คนเดียว apply และ refresh source เดิม
- source B taunt หลัง source A แล้ว B เป็นเป้าหมาย
- B หมดอายุแล้ว fallback กลับ A หาก A ยัง active
- source ถูก destroy แล้ว fallback อย่างปลอดภัย
- Taunt Def คนละตัวแต่ tag เดียวกันแข่งขันตาม application sequence
- invalid Taunt Def authoring ถูก validator จับ
- line of sight, radius, layer และ caster exclusion ไม่ regression

### Editor/Data

- Drawer ปรากฏใน Skill Inspector และ Active Skill Tree embedded step view
- clone-on-edit, add/remove/reorder, Reset และ effect-change dialog รองรับ Undo/Redo
- migration dry run หลัง migrate รายงาน 0
- ไม่มี legacy serialized data ใน target assets
- ตรวจ git diff ของ asset ทุกไฟล์ที่ถูก rewrite; ห้ามมี unrelated prefab/scene/data churn

### Required Build Validation

หากแก้ C# ให้รันเฉพาะคำสั่งที่ `AGENTS.md` อนุญาต:

```powershell
powershell -ExecutionPolicy Bypass -File 'P:\Game_RB_Project\RB_Project\Assets\Scripts\CheckAssemblyBuild.ps1'
```

จากนั้น force Unity script refresh และยืนยันทั้ง `Assembly-CSharp` กับ
`Assembly-CSharp-Editor` compile สำเร็จ ตรวจ Console สำหรับ error ใหม่

## Documentation Updates

อัปเดตอย่างน้อย:

- `Docs/SYSTEMS/SKILL_SYSTEM.md` — application ownership, override channels และ duration precedence
- `Docs/SYSTEMS/AI_AND_TARGETING.md` — tagged multi-Taunt resolution และ latest/fallback rule
- `Docs/PREFABS_AND_AUTHORING.md` — นำ `tauntedEffectDef` ออกจาก prefab setup และระบุ Taunt Def validation

ลบข้อความ legacy migration ที่หมดอายุหลัง schema cleanup สำเร็จ

## Completion Criteria

งานถือว่าเสร็จเมื่อครบทุกข้อ:

- ไม่มี Skill/Passive/Pickup/Projectile authored apply site เรียก Def-only overload
- Morph, HealArea และ primary Taunt ใช้ `StatusApplicationSpec`
- scalar override รองรับค่า `0` แบบ explicit
- multi-Taunt latest-wins/fallback ทำงาน deterministic
- asset migration ผ่านและไม่มี legacy fields/fallback code/tool เหลือ
- Inspector ใช้งานได้ทุกจุดและไม่แก้ `StatusEffectDef` โดยไม่ตั้งใจ
- runtime build และ Unity Editor compilation ไม่มี error ใหม่
- docs ทั้งสามส่วนตรงกับ implementation สุดท้าย

## Recommended Execution Order

1. เพิ่ม scalar override model/resolution และ tests
2. ขยาย drawer และทดสอบ serialization/Undo
3. เปลี่ยน runtime apply sites ให้รับ Spec โดยยังอยู่ใน transitional schema
4. implement tagged multi-Taunt และ validation
5. ขยาย migration tool + dry run
6. migrate/verify assets จริง
7. ลบ legacy schema/tool และ force-reserialize
8. รัน behavior tests, build, Unity compile และตรวจ asset diff
9. อัปเดต docs และทำ final repository search

