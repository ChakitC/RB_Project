# Combo Skill System Migration Handoff

## สถานะเอกสาร

- จุดประสงค์: ส่งต่องานออกแบบและ implementation สำหรับเปลี่ยนระบบ Chain Attack ไปเป็นระบบ Combo Skill แบบผู้เล่นกดตอบสนองต่อเงื่อนไขของสมาชิกในทีม
- สถานะโค้ด ณ วันที่เขียน: ยังไม่ได้แก้ gameplay code, prefab, scene หรือ Input Actions
- ขอบเขตของเอกสารนี้: architecture, contracts, migration phases, file ownership, validation และ acceptance criteria
- ชื่อที่ผู้เล่นเห็น: **Combo Skill**
- ชื่อภายในโค้ดที่แนะนำ: **Party Combo Skill** เพื่อไม่ให้ชนความหมายกับ `MeleeComboSO`
- ตรวจทานกับโค้ดจริงล่าสุด: **2026-09-04** — ตรวจ cast transaction รวม immediate path,
  party spawn/binding, character-owned skill runtime, Helper snapshot, pause clock,
  combat-fact identity, serialized enum values, save/load, target handle/pooling,
  feature rollback และข้อจำกัด test assembly แล้ว

---

## 0. Blocking Contracts ก่อนเริ่มเขียนระบบ

เจ็ดข้อนี้เป็น prerequisite ของ vertical slice ไม่ใช่งานเก็บท้าย:

1. เพิ่ม request-scoped `OnCommitted` ใน `SkillCastRequest` และส่งผ่าน public
   `CharacterSkillManager.TryStartExternalSkill`; ห้ามใช้ `CastReleased` เป็นหลักฐานว่า cooldown
   commit แล้ว และห้ามออกแบบให้ caller ต้องรอ method คืน `RequestId` ก่อนจึงผูก callback (§2.4)
2. MVP ใช้ **fixed Combo Skill ที่ไม่มี skill-tree/loadout upgrade** แต่ยังต้องมี combo-owned runtime
   entry แยกจาก generic external/Helper entry เพื่อกัน Helper snapshot รั่วเข้ามา (§2.3)
3. bind controller/executor จาก `PartyRuntimeBinder` ก่อน UI bind; อย่าพึ่ง static
   `PartySpawnPoint.Spawned` สำหรับ first initialization (§3.5, §10.6)
4. แยก transient AI action ออกจาก hard reservation และกำหนด execution concurrency ให้ C กดต่อ
   ได้ตอน B ยัง recovery (§8, §10.5)
5. สร้าง `PartyComboExecutionProfile` เป็น data contract ของระบบใหม่ตั้งแต่แรก; implementation
   ภายใน reuse placement/transition utility เดิมได้ แต่ห้าม serialize Chain-only type ลง Combo asset (§6.1)
6. เพิ่ม `FactId` ให้ `PassiveEventContext`, กำหนด dedupe key และ normalize `ChainId == 0`
   ครั้งเดียวก่อน trigger evaluation (§7.4)
7. vertical slice ต้องใช้เฉพาะ trigger source ที่มีระบบรองรับจริงแล้ว — Status lifecycle,
   `EnteredBreak` และ `ComboSkillCommitted` ที่ระบบนี้เป็นเจ้าของเอง; Final Strike, Arts Reaction
   และ tag `Infliction` ยังไม่มีในโปรเจกต์ อย่าวางแผน Phase ใดให้รอของสามอย่างนี้ และก่อน append
   Combo facts ต้องตรึง explicit numeric values ของ `PassiveEventType` ตาม Phase 0.5 (§3.7, §7.1)

ถ้ายังไม่ปิดเจ็ด contract นี้ การทำ HUD หรือ author content ก่อนจะทำให้ระบบดูเหมือนใช้ได้ แต่มีโอกาส
เปิด combo ต่อจาก payload ที่ fail, รับ Helper upgrade ผิดชุด, bind หลุดตอน spawn, chain สะดุด
ระหว่าง actor recovery, asset ใหม่ติด dependency เก่า หรือ dedupe fact ผิดรายการ

---

## 1. Product Intent

เปลี่ยนระบบจาก Chain Attack แบบเดิม:

```text
ศัตรูเข้า ChainReady
  -> ผู้เล่นเล็งเป้าและกด F
  -> ระบบรัน ChainAttackSequenceDef ที่กำหนดสมาชิกหลายคนไว้ล่วงหน้า
  -> sequence จบแล้วศัตรูเข้า Stun
```

ไปเป็น Combo Skill แบบใหม่:

```text
ผู้เล่นหรือสมาชิกทีมสร้าง Combat Fact
  -> เงื่อนไข Combo Skill ของสมาชิก B สำเร็จ
  -> HUD เปิด Combo Opportunity ของ B ชั่วคราว
  -> ผู้เล่นกดปุ่มของ B
  -> B หยุด AI ชั่วคราวและใช้ Combo Skill กับเป้าที่ถูกล็อกไว้
  -> เมื่อ cast commit สำเร็จ ระบบปล่อย ComboSkillCommitted fact
  -> fact อาจเปิด Combo Opportunity ของ C
```

หลักการสำคัญ:

1. ตัวละครที่ไม่ได้ควบคุมยังใช้ Normal Attack ผ่าน AI ตามปกติ
2. AI ไม่ตัดสินใจกด Battle Skill, Combo Skill หรือ Ultimate แทนผู้เล่น
3. Combo Skill ต้องเกิดจาก trigger ที่กำหนดไว้ในข้อมูลของตัวละคร
4. Trigger เพียงสร้างโอกาสให้กด ไม่เริ่มท่าอัตโนมัติ
5. Combo Skill ไม่เสีย Energy/SP แต่ยังต้องเคารพ cooldown/charge ของสกิล
6. ผู้เล่นต้องกดทุก link ของ combo chain เอง
7. การจัดทีมควรมีคุณค่าจาก trigger compatibility ระหว่างสมาชิก

---

## 2. ข้อสรุปการออกแบบที่ใช้เป็นฐาน

ให้ถือรายการนี้เป็นค่าเริ่มต้นของ implementation จนกว่าจะมี product decision ใหม่:

### 2.1 Combo Skill ไม่ใช่การเปลี่ยนชื่อ Chain Attack ตรง ๆ

ระบบใหม่เป็น event-driven opportunity system ส่วนระบบเดิมเป็น scripted multi-actor sequence การพยายามขยาย `SkillChainDef` และ `ChainAttackSequenceDef` เดิมให้รองรับทุกอย่างจะทำให้ trigger, UI, cost, target และ execution lifecycle ผูกกันแน่นเกินไป

ให้สร้าง orchestration layer ใหม่ แต่ reuse execution infrastructure ของ Chain เดิม

### 2.2 หนึ่ง Combo Opportunity เป็นของตัวละครหนึ่งคน

- หนึ่ง offer อ้างถึง actor เจ้าของ Combo Skill เพียงคนเดียว
- หนึ่ง offer อ้างถึง target snapshot เพียงเป้าหมายเดียว เว้นแต่ skill เป็น self/party-targeted โดย explicit policy
- สมาชิกต่างคนสามารถมี offer พร้อมกันได้
- actor คนเดียวมี active offer ได้สูงสุดหนึ่งรายการใน MVP
- trigger ใหม่ของ actor เดิมใช้ replacement policy ที่ระบุใน definition; ค่าเริ่มต้นคือ refresh เฉพาะ definition และ target เดิม มิฉะนั้นเก็บ offer เดิมไว้จนหมดอายุ
- หาก combat fact เดียว match definition candidates มากกว่าหนึ่งรายการของ actor เดียว ให้เลือก
  `priority` สูงสุด; ถ้าเท่ากัน definition ที่มาก่อนใน stable authoring order ชนะ ห้ามพึ่ง
  `Dictionary` iteration order โดย stable order คือ order ที่ character-definition resolver ส่งออก
  (MVP ปกติมีหนึ่งรายการ) การตัดสิน candidate ทำก่อนใช้ active-offer refresh/replacement policy

### 2.3 Cooldown มีแหล่งข้อมูลเดียว

ใช้ `SkillInstance`/shared `SkillChargeState` ของ `SkillGemDefinition` เป็น authoritative cooldown

อย่าสร้าง cooldown อีกชั้นใน `PartyComboSkillDef` ถ้าไม่มีเหตุผลที่แยกจาก skill charge จริง ๆ เพราะจะเกิดปัญหา UI บอกพร้อมแต่ trigger controller บอกไม่พร้อม หรือกลับกัน

การ execute Combo Skill ต้องเรียก `CharacterSkillManager.TryStartExternalSkill` ด้วย:

```csharp
costPolicy: SkillCastCostPolicy.IgnoreEnergyRespectCharge
stampCooldown: true
```

ผลลัพธ์คือไม่เสีย Energy แต่ต้องมี charge และ commit cooldown ผ่าน transaction เดิม

**กฎบังคับเรื่อง charge pool:** cooldown query ของ HUD และ cast ของ executor ต้อง resolve จาก
`CharacterSkillEntry` ตัวเดียวกันที่ได้จาก `CharacterSkillManager` เท่านั้น
`CharacterSkillManager.TryStartExternalSkill(SkillGemDefinition, ...)` เรียก
`GetOrCreateExternalEntry` ซึ่งเป็นสิ่งที่ผูก `SkillInstance` เข้ากับ shared charge pool ของตัวละคร
การสร้าง `new SkillInstance` เองจะได้ private pool: ถ้าสร้างใหม่ต่อ cast/query cooldown จะถูก reset
แบบเงียบ ๆ และแม้ cache instance ไว้ก็ยังไม่แชร์ charge กับ entry อื่น ทำให้ single-source-of-truth
พังทั้งข้อ — ดู XML doc บน
`CharacterSkillManager.TryStartExternalSkill`

ข้อจำกัดที่ต้องไม่มองข้าม:

- external entry ที่สร้างจาก `SkillGemDefinition` อย่างเดียวรักษา shared charge ได้ แต่ปัจจุบัน
  `BuildRuntimeSkill(CharacterSkillEntry, ...)` lookup Helper snapshot ด้วย `SkillGemDefinition`
  และ `ApplyHelperExecutionSnapshot` push snapshot เข้า cached external entry ของ definition เดียวกัน
  ดังนั้น Combo ที่ใช้ definition ซ้ำกับ Helper proc อาจรับ Helper upgrade แบบเงียบ ๆ
- MVP ล็อกให้ Combo Skill เป็น fixed character skill ที่ไม่มี upgrade แต่คำว่า fixed ไม่ได้ทำให้
  generic external entry ปลอดภัยจาก Helper snapshot; ต้องเพิ่ม combo-owned runtime entry API ใน
  `CharacterSkillManager` ซึ่งสร้าง `SkillInstance` ด้วย Combo snapshot ที่เป็น `null` อย่าง explicit
  และไม่ถูก `ApplyHelperExecutionSnapshot` แตะ
- HUD และ executor ต้อง resolve combo-owned entry เดียวกัน; ห้ามให้ HUD query definition-only entry
  แต่ executor cast combo-owned entry เพราะ final stats อาจไม่ตรงกันแม้ charge pool เดียวกัน
- shared charge pool ยัง keyed ด้วย `SkillGemDefinition`; การใช้ definition เดียวกันข้าม Battle,
  Helper และ Combo จึงแชร์ charge/cooldown แต่ snapshot ownership ไม่เหมือนกัน สำหรับ MVP ให้
  authoring validator **reject** การ reuse definition ข้าม role เหล่านี้ เว้นแต่ภายหลังจะมี
  explicit shared-role policy พร้อม tests
- การรองรับ Combo skill-tree/loadout variant เป็นงาน post-MVP: API ในอนาคตต้องรับ
  character-owned selection/snapshot และยังคงแยกจาก Helper snapshot

API ขั้นต่ำของ manager ต้องทำให้ caller resolve combo-owned `CharacterSkillEntry`, query
`SkillChargeStatus` และ start cast จาก entry เดิมได้โดยไม่ expose การสร้าง `SkillInstance` ให้
controller/HUD; ชื่อ method ปรับตาม style ใกล้เคียงได้ แต่ ownership ห้ามย้อนกลับไปใช้
definition-only external entry

**Save/load contract ของ MVP:** `CharacterSkillManager` เป็น `IGameSaveAble` แต่ fixed Combo ไม่มี
selection/upgrade จึง **ไม่**เพิ่ม entry ใน `LoadSavedSkillSelections()` หรือ
`CharacterProgressData.selectedSkillOptions` ส่วน charge/cooldown ไม่ persist ข้าม session ตาม
contract ปัจจุบันของ manager: `OnLoad` เรียก `ResetAllChargesToFull()` และ combo-owned entry ที่
resolve หลัง load ต้องเริ่มด้วย full charge เช่นกัน ห้ามเพิ่ม Combo save schema จนกว่าจะมี
loadout/upgrade requirement จริง

### 2.4 จุดที่ถือว่า “ใช้ Combo Skill แล้ว”

เผยแพร่ `ComboSkillCommitted` เมื่อ cast transaction commit ที่ cast point สำเร็จ ไม่ใช่ตอนผู้เล่นกดปุ่ม

เหตุผล:

- การกดที่ถูกปฏิเสธต้องไม่เปิด combo ถัดไป
- animation ที่ถูกยกเลิกก่อน cast point ต้องไม่เปิด combo ถัดไป
- target/placement ที่ล้มเหลวต้องไม่สร้าง chain ปลอม

ถ้าภายหลังต้องการ trigger จาก “Combo Skill hit สำเร็จ” ให้เพิ่ม fact แยกชื่อ `ComboSkillHit` อย่าเปลี่ยนความหมายของ `ComboSkillCommitted`

**ช่องว่างของ API ปัจจุบัน:** ห้ามฟัง `CharacterSkillManager.CastReleased` แล้วถือว่า commit สำเร็จ
เพราะ event นี้ถูกยิงเมื่อถึง cast point **ก่อน** `runtimeSkill.ExecuteReserved(...)` และก่อน
`reservation.Commit()`; payload ยังอาจคืน failure แล้ว reservation ถูก release ได้

implementation ต้องเพิ่ม request-scoped callback ที่ authoritative:

- เพิ่ม `OnCommitted` ใน `SkillCastRequest` และส่งผ่าน public
  `CharacterSkillManager.TryStartExternalSkill` ของ combo-owned entry
- callback รับ `ActiveSkillCastInfo` หรืออย่างน้อย `RequestId` และถูกยิงเพียงครั้งเดียวทันทีหลัง
  `reservation.Commit()` สำเร็จ
- executor ต้องตั้ง offer state เป็น `Starting` และผูก callback **ก่อน** เรียก start API
- immediate path ปัจจุบันทำ release, execute และ commit ภายใน `TryStartCast` ก่อน method คืน
  `SkillCastStartResult`; ห้ามออกแบบให้ executor รอผลลัพธ์แล้วค่อย subscribe/match `RequestId`
- state machine ต้องรองรับ `Starting -> Committed` และ `Starting -> Failed` แบบ synchronous
  นอกเหนือจาก animated `Starting -> Executing -> Committed`
- ถ้าต้องการให้ event order เป็น `Started -> Released -> Committed` เหมือนกันทุก path ต้องเพิ่ม
  `CastStarted` event ใน immediate path ด้วย; `OnStarted` callback ที่มีอยู่ไม่ใช่ event เดียวกัน

global `CastCommitted` event อาจมีเพื่อ diagnostics/presenter ได้ แต่ Combo execution correctness
ต้องไม่พึ่ง global subscription เพียงอย่างเดียว

### 2.5 ไม่มี global player protection ระหว่าง Combo Skill

Chain Attack เดิมป้องกันผู้เล่นตลอด sequence เพราะเป็น cinematic team finisher ระบบ Combo ใหม่ไม่ควรให้ invincibility/untargetable แก่ตัวที่ผู้เล่นควบคุมตลอด offer หรือ execution

อนุญาต actor-level protection เฉพาะสมาชิก AI ที่ถูกดึงออกจาก autonomy ผ่าน `FieldAllyAutonomyScope` ตาม policy เดิม เพื่อป้องกัน execution แตกจาก AI/physics collision

### 2.6 ใช้เวลาแบบ gameplay pause-aware

เป้าหมายคือ pause token หยุด timer แต่ world slow และ hitlag **ไม่** ยืดเวลาตัดสินใจ ซึ่ง clock
สำเร็จรูปที่มีอยู่ไม่มีตัวไหนให้ครบ:

| clock | global pause หยุด | `TimeSlowManager` slow ยืด | global hitlag ยืด |
|---|---|---|---|
| `Time.time` | ใช่ | ไม่ยืด | ยืด |
| `TimeSlowManager.WorldTime` | ไม่หยุด | ยืด | ไม่ยืด |
| `Time.unscaledTime` | ไม่หยุด | ไม่ยืด | ไม่ยืด |

`GlobalTimeScaleManager` เขียน `Time.timeScale` สำหรับ pause และ hitlag ส่วน world slow จริงของ
เกมใช้ `TimeSlowManager.WorldTimeScale` แยกต่างหาก อย่าเรียกสองกลไกนี้รวมกันว่า clock เดียว

ข้อสรุปที่ต้องใช้:

- opportunity expiration ใช้ owner-local accumulator ของ controller เอง —
  `if (!paused) _offerClock += Time.unscaledDeltaTime;` แล้วเทียบ `ExpiresAt` กับ clock ตัวนี้
  ไม่ใช่ `Time.time`
- precedent ของ owner-local accumulator คือ `_statusClock` ของ Status system — ลอกรูปแบบ
  ownership มาได้ แต่ **ห้าม** ลอก delta เพราะ Status ตั้งใจใช้ `WorldDeltaTime`
- Skill charge/cooldown ยังใช้ clock เดิมของ `SkillInstance` ไม่ต้องแตะ
- pause ของ `GlobalTimeScaleManager` เป็น token-based; controller ต้องอ่าน
  `GlobalTimeScaleManager.Instance.IsPaused` โดยตรง ไม่ใช่เดาจาก `Time.timeScale == 0`

### 2.7 ChainReady/Stagger ยังไม่ถูกลบในเฟสแรก

ให้แยก ChainReady ออกจาก Combo Skill migration:

- ChainReady/Stagger ยังคงทำงานเดิมระหว่าง vertical slice
- สามารถเพิ่ม `EnteredBreak`/`EnteredChainReady` เป็นหนึ่ง trigger ของ Combo Skill ได้
- scripted multi-actor Chain เดิมอาจถูกเก็บไว้ชั่วคราวในฐานะ Break Finisher
- อย่าลบ ChainReady prompt, Stagger handoff หรือ chain protection จนกว่า Combo Skill vertical slice ผ่าน regression ทั้งหมดและมี product decision เรื่อง Break Finisher

### 2.8 ย้าย `ChainActorRole` ออกจาก ChainAttack ก่อนเขียนโค้ดใหม่

`ChainActorRole` ประกาศอยู่ใน `Assets/Scripts/AI/ChainAttack/ChainAttackSequenceDef.cs` ซึ่งเป็น
ไฟล์ที่ §15 จัดไว้ในกลุ่ม retire แต่ API ใหม่ทั้งชุด (offer key, HUD slot, executor) ใช้ enum นี้
เป็น primary key และ `PartyRuntime` ก็ใช้อยู่แล้ว

ปล่อยไว้จะกลายเป็นระบบใหม่พึ่งไฟล์ที่มีแผนจะลบ

งานแรกของ Phase 1 คือย้าย enum ไปไฟล์ของตัวเองใต้ `Assets/Scripts/Party/` โดยยังคงชื่อ
`ChainActorRole` ไว้ก่อน (rename เป็น `PartyActorRole` เป็นงานแยกที่ต้องแตะ serialized asset
ของ Chain เดิม จึงไม่ควรรวมกับ migration นี้)

### 2.9 Combo asset ไม่ serialize execution type ของ legacy Chain

สร้าง `PartyComboExecutionProfile` ใต้ `Assets/Scripts/PartyCombo/` ตั้งแต่ Phase 1 เพื่อเป็น
authoring contract ของ placement, enter, exit และ cleanup สำหรับ Combo โดยเฉพาะ

- ห้ามให้ `PartyComboSkillDef` serialize `ChainAttackTeleportProfileDef`,
  `ChainActorEnterMode` หรือ `ChainActorExitMode` โดยตรง
- profile ใหม่สามารถแปลงค่าเป็น request ของ `CharacterPlacementResolver` และเรียก
  `ChainAttackTeleportUtility`/`FieldAllyTransitionController` เป็น implementation adapter ชั่วคราวได้
- enum/data ที่มี semantics ใช้ร่วมจริงให้ย้ายไป module กลางพร้อม migration ชัดเจน ไม่ให้ระบบใหม่
  อ้าง type ที่ประกาศอยู่ในไฟล์ซึ่ง Phase 6 เตรียม retire
- หากต้อง retire utility เดิมในอนาคต ให้เปลี่ยน implementation หลัง profile โดยไม่ต้อง migrate
  `PartyComboSkillDef` asset อีกรอบ

---

## 3. ข้อเท็จจริงของระบบปัจจุบัน

### 3.1 Chain definition ปัจจุบัน

`Assets/Scripts/AI/ChainAttack/SkillChainDef.cs`

- เลือก `PassiveEventType` ได้หนึ่งชนิด
- มี origin filter, proc chance, internal cooldown และ once-per-attack gate
- มี `commandPointCost`
- ชี้ไป `ChainAttackSequenceDef`
- trigger จาก combat event สามารถเริ่ม sequence อัตโนมัติ

ข้อจำกัดต่อ Combo Skill ใหม่:

- ไม่มี Final Strike, Arts Reaction, status threshold หรือ Combo Skill event ใน `PassiveEventType`
- trigger สำเร็จแล้วเริ่ม execution ทันที ไม่มี offer lifecycle
- cost เป็น CP ซึ่งขัดกับกฎ Combo Skill ไม่เสีย SP/CP
- definition เป็นของ whole sequence ไม่ใช่ character-owned single skill

### 3.2 Proc controller ปัจจุบัน

`Assets/Scripts/AI/ChainAttack/ChainAttackProcController.cs`

- subscribe `CombatEventBus` ของ player เพียง bus เดียว
- `OnCombatEventPublished` ประเมินทุก `SkillChainDef`
- เมื่อผ่านจะเรียก `chainAttackCoordinator.TryStartSequence(...)` ทันที
- เก็บ internal cooldown และ attack-id lock เอง
- มี lifecycle พิเศษสำหรับ ChainReady intro cutscene

ส่วนที่ไม่ควรใช้เป็น Combo opportunity controller โดยตรง:

- auto-start behavior
- single-bus subscription
- pending state ที่ผูกกับ `StaggerMeter`
- cooldown ซ้ำกับ skill charge

### 3.3 Sequence/execution ปัจจุบัน

`Assets/Scripts/AI/ChainAttack/ChainAttackSequenceDef.cs`

- sequence มีหลาย `ChainAttackStepDef`
- actor role: Player, PartySlot1, PartySlot2, Helper
- รองรับ target lock, placement, warp, root motion, timing, skip/fail policy และ exit mode

`Assets/Scripts/AI/ChainAttack/ChainAttackCoordinator.cs`

- รัน step ตามลำดับ
- reserve actor และรอ completion/early continue signal
- จัดการ timeout, target death และ player protection

ส่วน implementation ที่ extract/reuse หลัง Combo-owned profile/executor ได้:

- single actor step execution
- target locking และ target anchor
- placement/teleport/root-motion validation
- actor reservation
- completion/cancel cleanup

สิ่งที่ห้ามใช้เป็น public Combo authoring/execution gate:

- สร้าง `ChainAttackSequenceDef` หนึ่งอันต่อ Combo Skill เพื่อห่อ step เดียว
- บังคับให้ Combo หลายตัวอยู่ใน sequence asset เดียว เพราะลำดับใหม่ต้องเกิดจาก runtime trigger
- ใช้ global `ChainAttackCoordinator` running state เป็นตัว block Combo ต่าง actor

cast lifecycle ที่มีอยู่จริงใน `CharacterSkillManager` คือ:

```text
animated: CastStarted
  -> CastReleased (ถึง cast point แต่ payload ยังไม่ยืนยันผล)
  -> ExecuteReserved
     -> failure: reservation.Release + CastExecutionFailed
     -> success: reservation.Commit

immediate: OnStarted callback (ปัจจุบันไม่มี CastStarted event)
  -> CastReleased
  -> ExecuteReserved + settle
  -> method จึงคืน SkillCastStartResult/RequestId
```

ปัจจุบันไม่มี request-scoped committed callback และ immediate path settle transaction ก่อน caller
รู้ `RequestId` จึงต้องเติม contract ตาม §2.4 ก่อนผูก Combo link

### 3.4 Party topology ปัจจุบัน

`Assets/Scripts/Party/PartyRuntime.cs`

runtime มี role:

- `Player`
- `PartySlot1`
- `PartySlot2`
- `Helper`

ข้อควรระวัง: Helper ปัจจุบันมี lifecycle ผ่าน `AllyHelperManager` และอาจถูก hide/deactivate ไม่ได้เป็น field ally ที่โจมตีปกติถาวรเหมือน PartySlot1/2

ดังนั้นประโยค product intent “ตัวละครทุกตัวที่ไม่ได้ควบคุมโจมตีปกติเอง” ยังไม่ตรงกับ topology ปัจจุบันทั้งหมด

ค่าเริ่มต้นของ vertical slice:

- รองรับ PartySlot1 และ PartySlot2 เป็น field Combo actors ก่อน
- รองรับ Helper ผ่าน execution path เฉพาะของ Helper โดยไม่อ้างว่า Helper มี normal-attack autonomy
- หากต้องการสมาชิก 4 คนอยู่สนามและโจมตีปกติพร้อมกัน ต้องเปิด workstream แยกเพื่อเปลี่ยน Helper เป็น persistent field actor หรือเพิ่ม PartySlot3
- live character switching ไม่อยู่ใน Combo Skill migration นี้ หากต้องการสลับตัวควบคุมต้องออกแบบ active-controlled-role และ context rebinding แยกต่างหาก

### 3.5 Event topology ปัจจุบัน

`CombatEventBus` อยู่บน character actor แต่ละตัว ไม่ใช่ global party bus

`PartyRuntimeBinder` bind actors และ UI แต่ยังไม่มี party-level combat event aggregator

ลำดับ spawn ปัจจุบันคือ `PartyRuntimeBinder.TryBind(party)` ตอน runtime root ยัง inactive จากนั้น
จึง activate runtime/UI และยิง `PartySpawnPoint.Spawned` ภายหลัง อีกทั้ง
`IPartySpawnedReceiver` ที่ค้นจาก scene roots จงใจไม่ค้นใต้ runtime root

ผลกระทบ:

- controller ที่ฟังเฉพาะ player bus จะไม่เห็น event ที่เครดิตให้ PartySlot1/2/Helper
- trigger “เพื่อนใช้ Combo Skill” ต้องรวม event จาก bus ของสมาชิกทุกคน
- status lifecycle เกิดบน `StatusEffectController` ของ target จึงต้อง publish fact กลับไปยัง credited source bus หรือมี adapter ที่รักษา attribution ให้ถูกต้อง
- controller ที่อยู่บน Player prefab ต้องถูก bind จาก `PartyRuntimeBinder` โดยตรงก่อน
  `PlayerUIRuntimeBinder.TryBind`; อย่าหวังพึ่ง `IPartySpawnedReceiver` หรือ static `Spawned` สำหรับ
  first bind เพราะตำแหน่งและลำดับ lifecycle ไม่รับประกัน และ static subscription เสี่ยงค้างข้าม scene

### 3.6 ระบบ Status ที่ reuse ได้

`StatusEffectController` มี `EffectLifecycleChanged` พร้อม:

- AppliedNew
- Refreshed
- StackChanged
- Removed
- Ticked

`StatusEffectInstance` เก็บ chain id, depth, origin และ attribution อยู่แล้ว จึงสามารถแปลง status lifecycle เป็น combat fact โดยรักษา provenance ได้

ยืนยันแล้วว่ามีจริงในโค้ด: `StatusEffectController.EffectLifecycleChanged`,
`StatusEffectController.ActiveEffects`, `StatusEffectInstance.Attribution.CreditedEventBus`,
`StatusEffectDef.tags` (`List<string>`) และ `StatusEffectEventType` ครบทั้ง 5 ค่า

### 3.7 Trigger source ที่ **ยังไม่มีอยู่จริง** ในโปรเจกต์

ข้อนี้สำคัญที่สุดของ §3 เพราะ §12 อ่านเหมือนแค่ต้อง "ล็อกนิยาม" ของระบบที่มีอยู่แล้ว
ความจริงคือ trigger 3 ใน 5 ตัวยังไม่มีโค้ดรองรับเลย

| Trigger source | สถานะจริง |
|---|---|
| `ComboSkillCommitted` | ยังไม่มี แต่ระบบนี้เป็นเจ้าของเอง สร้างได้ใน Phase 2 |
| Status Applied / StackChanged | **พร้อม** — lifecycle event มีครบ ขาดแค่ publisher เชื่อมเข้า bus |
| `EnteredBreak` | **พร้อม** — `CombatEventMetadata.EnteredChainReady` มีอยู่และถูก publish จริงจาก melee, skill hitbox และ projectile |
| Final Strike | **ไม่มีเลย** — ไม่มี symbol ใดในโค้ด และ `MeleeComboSO` ไม่มีแนวคิด "step สุดท้าย" ต้องเพิ่มเข้าไปในระบบ basic attack ก่อน |
| Arts Reaction | **ไม่มีเลย** — ไม่มีระบบ reaction อยู่ในโปรเจกต์ เป็น feature workstream แยกทั้งก้อน ไม่ใช่แค่ publisher |

tag `Infliction` ที่ §12 อ้างถึงก็ยังไม่มีใน status asset ใด — tag ที่ใช้จริงตอนนี้เป็นชุดคนละแบบ
(`Damage`, `Armor`, `Ammo`, `Critical` …) ดังนั้นการนับ Infliction ต้องเริ่มจากการ **นิยามและ
ติด tag ให้ status assets ก่อน** ไม่ใช่แค่เลือกสูตรนับ

ผลต่อแผน:

- vertical slice ต้องใช้ trigger ที่พร้อมแล้วเท่านั้น — `ComboSkillCommitted` + Status +
  `EnteredBreak` — ห้ามให้ Phase 2 ไปติดรอ Final Strike
- Final Strike, Arts Reaction และ Infliction ถูก reserve ใน enum แต่ validator ต้องปฏิเสธจนกว่า
  owner system/content จะพร้อม — ดู §6.4 และ §22

---

## 4. Domain Vocabulary

ใช้คำต่อไปนี้สม่ำเสมอในโค้ดและเอกสาร:

| คำ | ความหมาย |
|---|---|
| Party Combo Skill | สกิลเฉพาะตัวละครที่เปิดให้ผู้เล่นกดเมื่อ trigger ผ่าน |
| Combo Trigger | กฎที่ประเมิน combat fact หนึ่งรายการและ target state ปัจจุบัน |
| Combo Opportunity / Offer | หน้าต่าง runtime ที่พร้อมให้ผู้เล่นกด |
| Combo Commit | จุดที่ skill transaction สำเร็จและ cooldown ถูกใช้ |
| Combo Link | การที่ Combo commit/hit ของ actor หนึ่งเปิด opportunity ของอีก actor |
| Combo Chain | ชุด Combo Links ที่เกิดจริงใน runtime ไม่ใช่ authored sequence |
| Fact ID | identity ของ combat fact occurrence หนึ่งรายการ ใช้ same-fact dedupe |
| Trigger Context | fact ต้นทางพร้อม actor, target, fact id, chain id, attack id และ metadata |
| Offer Target | target snapshot ที่ผูกกับ opportunity |
| Break Finisher | ชื่อชั่วคราวของ scripted ChainReady sequence เดิม หากเก็บไว้ |

อย่าใช้คำ `Combo` เดี่ยว ๆ ในชื่อคลาส เพราะโปรเจกต์มี `MeleeComboSO`, `MeleeComboSession` และ animation state ที่ใช้คำนี้อยู่แล้ว

---

## 5. Target Architecture

```text
Weapon / Melee / Skill / Reaction / Status / Combo execution
                         |
                         v
          CombatEventBus ของ credited party actor
                         |
                         v
              PartyCombatEventRouter
            subscribe ทุก actor ใน PartyRuntime
                         |
                         v
            PartyComboOpportunityController
              |        |          |
              |        |          +--> cooldown/availability query
              |        +-------------> offer lifecycle + dedupe
              +----------------------> trigger evaluator
                         |
               OpportunityChanged event
                         |
                         v
                PartyComboHudPresenter
                         |
                    player input
                         |
                         v
              PartyComboSkillExecutor
                         |
              FieldAllyMember / Helper path
                         |
             CharacterSkillManager external cast
                         |
                  cast transaction commit
                         |
                         v
                ComboSkillCommitted fact
```

### Ownership boundaries

- Publisher เจ้าของ gameplay fact เท่านั้นที่ publish fact นั้น
- Router รวม bus แต่ไม่ประเมิน trigger
- Opportunity controller ประเมิน trigger และเป็นเจ้าของ offer state
- HUD แสดง state และส่ง input intent เท่านั้น
- Executor เป็นเจ้าของ reservation, placement, cast และ cleanup
- `CharacterSkillManager` เป็นเจ้าของ skill instance, charge และ cast transaction
- AI ห้ามอ่าน HUD state และห้าม consume opportunity เอง
- executor ห้ามแตะ `locomotionSM` หรือ `CharacterAnimBrain` โดยตรง; animation ต้องเกิดผ่าน
  cast path เดิม `CharacterSkillManager -> StateHub/controller -> CharacterAnimDriver` ตามกฎ
  `Docs/ARCHITECTURE/ANIMATION_COMMAND_FLOW.md`
- `PartyRuntimeBinder` เป็น composition root: bind router/controller/executor กับ `PartyRuntime`
  หลัง configure actors/helper และ **ก่อน** bind HUD; teardown/rebind ต้องมี API ชัดเจนและ idempotent

---

## 6. Data Model ที่แนะนำ

### 6.1 `PartyComboSkillDef`

สร้างไฟล์ใหม่ใน `Assets/Scripts/PartyCombo/PartyComboSkillDef.cs`

แนะนำ fields:

```csharp
[CreateAssetMenu(fileName = "PartyComboSkill", menuName = "Game/Party Combo/Skill")]
public sealed class PartyComboSkillDef : ScriptableObject
{
    public string comboId;
    public string displayName;
    [TextArea] public string description;
    public Sprite iconOverride;

    public SkillGemDefinition executionSkill;
    public PartyComboTriggerRule trigger;

    [Min(0.1f)] public float offerDurationSeconds = 3f;
    public PartyComboTargetPolicy targetPolicy = PartyComboTargetPolicy.EventTarget;
    public PartyComboOfferRefreshPolicy refreshPolicy = PartyComboOfferRefreshPolicy.RefreshSameTarget;
    public bool requireOwnerAlive = true;
    public bool requireTargetAlive = true;
    public PartyComboOwnerBusyPolicy ownerBusyPolicy = PartyComboOwnerBusyPolicy.AllowInterruptibleAiAction;
    public int priority;

    public PartyComboExecutionProfile executionProfile;
}
```

หมายเหตุ:

- `executionSkill` ควรเป็น skill ที่ character owns ผ่าน `CharacterStats`
- icon fallback ใช้ `executionSkill.icon`
- cooldown มาจาก `executionSkill` ไม่เพิ่ม `cooldownSeconds` ใน definition นี้
- MVP กำหนดให้ `executionSkill` ไม่ต้องอยู่ใน `CharacterStats.skillSlots`, ไม่แสดงใน Skill screen
  และไม่มี upgrade node; combo-owned runtime entry สร้างจาก `CharacterStats.partyComboSkill`
  โดยตรง ส่วนการเพิ่ม loadout/upgrade เป็น post-MVP decision ตาม §22
- `ownerBusyPolicy` ต้องแยก AI normal attack/locomotion ที่ Combo สามารถแทรกได้ ออกจาก hard lock
  เช่น down, death, cinematic, interruption reservation หรือ Combo request เดิม; bool เดียวไม่พอ
- `priority` ใช้เฉพาะ deterministic winner selection เมื่อ fact เดียว match หลาย candidates ของ owner
  เดียวกัน: ค่าสูงกว่าชนะและ tie ใช้ stable authoring order; ห้ามใช้ dictionary order
- `executionProfile` เป็น Combo-owned ScriptableObject และต้องไม่ serialize type ที่ประกาศ
  ใต้ legacy Chain; ภายใน executor ยัง reuse utility เดิมผ่าน adapter ได้ตาม §2.9
- validator ต้อง reject `executionSkill` ที่ถูกใช้เป็น Battle Skill หรือ Helper proc ของ character
  เดียวกันใน MVP เพื่อป้องกัน shared charge/snapshot behavior ที่ไม่ได้ author ไว้โดยตั้งใจ

### 6.2 `PartyComboExecutionProfile`

สร้าง `ScriptableObject` ในไฟล์ `Assets/Scripts/PartyCombo/PartyComboExecutionProfile.cs` โดยเก็บเฉพาะ semantics ที่
Combo ต้อง author เช่น placement policy, desired range/offset, obstruction policy, enter presentation,
exit/return policy, timeout และ actor-protection policy

profile ต้องสร้าง `CharacterPlacementRequest` หรือ request DTO กลางให้ executor ใช้ ไม่ expose
`ChainAttackStepDef`/`ChainAttackSequenceDef` ต่อ controller หรือ HUD การแปลงเข้า legacy utility
ให้จำกัดอยู่ใน executor adapter จุดเดียวและมี tests ครอบ cleanup/return behavior

### 6.3 Character ownership

เพิ่ม field บน `CharacterStats` เพราะ prefab ของ party slot เป็น rig ที่สลับตัวละครได้:

```csharp
[FoldoutGroup("Party Combo Skill")]
public PartyComboSkillDef partyComboSkill;
```

อย่า author Combo Skill บน Player prefab หรือ PartySlot prefab เป็นแหล่งหลัก

การย้ายจาก `CharacterStats.chainAttackSkill`:

- เก็บ field เดิมไว้ใน migration phase แรก
- เพิ่ม editor migration tool เพื่อสร้างหรือ assign `PartyComboSkillDef` จากข้อมูลเดิมที่แปลงได้
- อย่า rename/delete serialized field จน assets ถูก migrate และ validation ผ่าน
- `introChainCutscene` ยังเป็นของ Break Finisher เดิม ไม่ควรย้ายเข้า Combo Skill โดยอัตโนมัติ

ข้อควรระวังจาก legacy: Chain skill ปัจจุบันมีสองแหล่งข้อมูล —
`CharacterStats.chainAttackSkill` และ serialized `CharacterSkillManager.chainAttackSkill` โดย
`FieldAllyMember` เลือก CharacterStats ก่อนแล้ว fallback ไป manager entry การ migrate ต้อง audit
และบันทึกแหล่งที่ชนะต่อ character ห้าม copy จาก field ใด field หนึ่งแบบเหมารวม

สำหรับ Combo ใหม่ให้ `CharacterStats.partyComboSkill` เป็น authoring source เดียว ส่วน runtime entry
เป็นของ `CharacterSkillManager` ตาม §2.3 ไม่เพิ่ม serialized Combo entry ซ้ำบน prefab manager

### 6.4 `PartyComboTriggerRule`

MVP ใช้ enum + typed fields ก่อน ไม่สร้าง generic expression tree ตั้งแต่แรก

```csharp
public enum PartyComboTriggerKind
{
    None = 0,

    // MVP — owner systems/event sources exist or are created by this migration.
    ComboSkillCommitted = 1,
    TargetHasStatus = 2,
    EnteredBreak = 3,

    // Reserved post-MVP values. Authoring validator must reject until owner systems exist.
    FinalStrike = 100,
    ArtsReaction = 101,
    InflictionCountReached = 102,
}
```

`PartyComboTriggerKind` ถูก serialize ลง `PartyComboSkillDef` asset จึงใช้กฎเดียวกับ
`PassiveEventType`: **append-only และห้ามเปลี่ยน numeric value** หลังมี asset แล้ว ค่า reserved
ข้างต้นจอง identity ไว้แต่ยัง author ใช้งานไม่ได้ใน MVP; validator ต้องรายงานว่า unsupported แทน
การปล่อยให้ evaluator เงียบ

rule fields ที่อาจใช้:

- required count
- required reaction id/tag
- required status definition หรือ status tag
- source relation: ControlledActor, Self, OtherPartyMember, AnyPartyMember
- target identity/filter
- event source id filter
- origin filter สำหรับ "รับ fact ที่ combo สร้างเองหรือไม่" — reuse `PassiveEventOrigin` และ
  `PassiveOriginFilter` ที่มีอยู่แล้วใน `Passives/PassiveTypes.cs` อย่าเพิ่ม bool ใหม่ซ้ำความหมาย

หลักการ evaluator:

- event เป็นตัวปลุก evaluator; ห้าม poll ศัตรูทุกตัวทุก frame
- trigger ที่ต้องดู target state ให้ query `CharacteContext` ของ `event.Target` หลัง event เกิด
- ใช้ `targetContext.StatusEffects` ไม่ใช้ physics overlap เพื่อค้นหา actor/status
- ถ้ากฎต้อง AND หลายเงื่อนไข ให้ event kind เป็น primary trigger และ filters อ่าน snapshot/state เพิ่ม ไม่ต้องสร้าง nested boolean graph ใน MVP

### 6.5 Runtime `PartyComboOpportunity`

ควรเป็น runtime class/readonly presentation snapshot ไม่ใช่ ScriptableObject

ข้อมูลขั้นต่ำ:

- `int OfferId`
- `PartyComboSkillDef Definition`
- `ChainActorRole OwnerRole`
- `CharacteContext OwnerContext`
- `SkillTargetHandle Target`
- `float OfferedAt` (อ่านจาก offer clock ของ controller ตาม §2.6)
- `float ExpiresAt` (clock เดียวกัน)
- source `PassiveEventContext` หรือ snapshot ที่ไม่ถือ object เกินจำเป็น
- `ulong FactId`
- `ulong ChainId`
- `int Depth`
- state: Offered, Starting, Executing, Committed, Finished, Expired, Cancelled, Failed
- cancellation reason

ห้ามให้ HUD ถือ reference แล้วแก้ runtime offer โดยตรง ให้ controller ส่ง immutable view data

---

## 7. Combat Fact Contracts

### 7.1 Event types ที่ยังไม่มีในโปรเจกต์

ปัจจุบัน `PassiveEventType` มี ShotFired, Hit, Kill, TakeDamage, DamagePrevented, PerfectDodge, Reload, DashStarted, DashEnded และ MovementDistanceReached เท่านั้น

การเพิ่มค่า enum เป็นคนละเรื่องกับการมีระบบที่ publish ค่านั้น — ดู §3.7

MVP ต้อง append fact เพิ่มเพียง:

- `StatusApplied`
- `StatusStackChanged`
- `ComboSkillCommitted`

`FinalStrike` และ `ArtsReaction` ให้ append ใน `PassiveEventType` เมื่อ owner system ของแต่ละ fact
ถูก implement จริงใน post-MVP workstream เท่านั้น อย่าเพิ่ม enum/event traffic ที่ยังไม่มี publisher
หรือ content ใช้งาน

`EnteredBreak` อาจอ่านจาก `Hit` + `CombatEventMetadata.EnteredChainReady` ที่มีอยู่แล้วในระยะแรก โดยยังไม่ต้องเพิ่ม event แยก

`PassiveEventType` เป็น serialized enum ที่ `SkillChainDef` และ passive asset เก็บเป็น int
ดังนั้น **ค่าใหม่ต้อง append ท้าย enum เท่านั้น** ห้ามแทรกกลางหรือเรียงใหม่ ไม่งั้น asset เดิม
จะชี้ event ผิดชนิดแบบไม่มี compile error

ก่อน append ค่าใหม่ต้องทำ serialized-enum hardening PR แยกก่อน Phase 1 โดยตรึงค่าปัจจุบันทั้งหมด:

```csharp
public enum PassiveEventType
{
    // Serialized: append only. Never reorder, renumber, or reuse a retired value.
    None = 0,
    ShotFired = 1,
    Hit = 2,
    Kill = 3,
    TakeDamage = 4,
    DamagePrevented = 5,
    PerfectDodge = 6,
    Reload = 7,
    DashStarted = 8,
    DashEnded = 9,
    MovementDistanceReached = 10,

    StatusApplied = 11,
    StatusStackChanged = 12,
    ComboSkillCommitted = 13,
}
```

PR นี้ต้องเปลี่ยน implicit values เดิมเป็น explicit `0..10` โดยไม่เปลี่ยน serialized meaning แล้วจึง
append `11..13`; หลังจากนั้นทุกค่าใหม่ต้อง append ด้วย explicit number เท่านั้น

### 7.2 Context identity และ metadata ที่ต้องเพิ่ม

เพิ่ม `ulong FactId` บน `PassiveEventContext` โดยตรง ไม่ใส่ใน `CombatEventMetadata` เพราะเป็น
identity ของ event envelope ไม่ใช่ gameplay payload การเปลี่ยน constructor ต้องรักษา call sites เดิม
ด้วย overload/default-compatible parameter หรือ factory path และห้ามให้ publisher สร้าง ID ใหม่ตอน
forward context เดิม

เพิ่ม optional fields ต่อท้าย `CombatEventMetadata` เพื่อไม่ทำลาย named/default call sites เดิม:

- `string StatusEffectId`
- `int StatusStacks`
- `string ReactionId`
- `string ComboSkillId`
- `ChainActorRole ComboOwnerRole` หากจำเป็นต่อ UI/debug; logic ควร resolve จาก party runtime เป็นหลัก

ถ้า metadata เริ่มโตจน constructor อ่านยาก ให้แยก typed factory methods แทนการสร้าง positional constructor เพิ่ม

### 7.3 Publishing ownership

- Final Strike (post-MVP): publish จากระบบ basic attack ที่รู้แน่ชัดว่า hit ที่ apply damage สำเร็จเป็น final authored strike
- Arts Reaction (post-MVP): publish จาก Arts Reaction resolver หลัง reaction สำเร็จจริง
- Status Applied/Stack Changed: hook ใน `StatusEffectController` ต้องกรอง
  `StatusEffectEventType` **ก่อน**สร้าง `PassiveEventContext`/FactId/metadata:
  - `AppliedNew` และ `Refreshed` -> `StatusApplied`
  - `StackChanged` -> `StatusStackChanged`
  - `Ticked` และ `Removed` -> ไม่ publish Combo combat fact โดยเด็ดขาด
  จากนั้นจึง publish ไปยัง `StatusEffectInstance.Attribution.CreditedEventBus`; fallback resolve bus
  จาก source `CharacteContext`
- Combo Skill Committed: executor/controller publish child context จาก trigger context ที่ offer เก็บไว้
  โดยสร้าง FactId ใหม่, รักษา ChainId และเพิ่ม Depth

อย่า infer Final Strike จาก Kill และอย่า infer Arts Reaction จาก status สองชนิดใน Combo controller เพราะ owner system เท่านั้นที่รู้ semantics จริง

### 7.4 Recursion guard

ต้องรักษา:

- `FactId` — identity ของ fact occurrence หนึ่งรายการ
- `ChainId`
- `Depth`
- `AttackId`
- `EventSourceId`
- source Combo ID

policy เริ่มต้น:

- `CombatEventBus.CreateExternalContext` และ `CreateChildContext` ต้อง assign `FactId` ใหม่หนึ่งครั้ง
  ต่อ fact ที่สร้าง; router forward context เดิมโดยไม่สร้าง FactId ใหม่
- same-fact dedupe ใช้ `(FactId, ComboSkillId, OwnerRole)` ห้ามเดาจาก frame, event type หรือ target
  เพราะ fact คนละรายการที่เกิดใน frame เดียวกันอาจถูกต้องทั้งคู่
- offer definition เดิม consume ได้ไม่เกินหนึ่งครั้งต่อ `(ComboSkillId, ChainId, OwnerRole)`
- max combo depth ค่าเริ่มต้น 8
- Combo Skill ห้าม trigger ตัวเองจาก `ComboSkillCommitted` เว้นแต่ definition opt-in อย่างชัดเจน
- cooldown/charge เป็น guard เพิ่ม ไม่ใช่ guard เดียว
- `FactId == 0` เป็น malformed/un-normalized context; router ต้องสร้าง normalized snapshot หนึ่งชุด
  ต่อ publish occurrence ก่อนส่งให้ evaluator และใช้ snapshot เดียวกับทุก definition

precedent ที่ต้องลอกรูปแบบ ownership/cleanup คือ
`PassiveController.RegisterRuleExecution(...)`: ระบบเดิมเก็บ key
`(ChainId, passiveId, ruleId, targetId)` ใน bucket ต่อ chain และให้ `ChainId == 0` ผ่าน dedupe ทันที
พฤติกรรมนี้ยืนยันว่ากับดัก zero-chain มีอยู่จริง; Combo ใช้ key ของตนเองตามด้านบน แต่ควร reuse
แนวคิด bucket lifecycle/last-seen cleanup แทนการสร้าง cache ที่โตไม่สิ้นสุด

**กับดักของ `ChainId == 0`:** `CombatEventBus` ใช้กฎ `parent.ChainId == 0 ? NextChainId() :
parent.ChainId` เมื่อสร้าง child context ถ้า publisher ตัวใดลืม thread parent context เข้าไป
ทุก event จะได้ chain id ใหม่ ทำให้ dedupe key `(ComboSkillId, ChainId, OwnerRole)` ไม่เคย match
recursion guard จะเงียบไปทั้งชุด และ Definition of Done ข้อ 9 จะผ่านแบบหลอก

`CombatEventBus.Publish` ปัจจุบันไม่ได้ normalize context ดังนั้นให้ router เป็น boundary ที่ normalize
`FactId == 0` และ `ChainId == 0` ครั้งเดียวก่อน evaluation; publisher ปกติยังต้องใช้ factory API
แทนการ construct context เอง

ต้องมี test ตรง ๆ ว่า root fact ที่ `FactId/ChainId == 0` ถูก normalize หนึ่งชุดต่อ publish occurrence,
definition หลายตัวเห็น IDs ชุดเดียวกัน และ child `ComboSkillCommitted` ได้ FactId ใหม่แต่รักษา
ChainId เดิม การเรียก `Publish` ซ้ำถือเป็น fact occurrence ใหม่; recursion/offer refresh ใช้ policy
คนละชั้น ไม่ใช่ FactId dedupe

---

## 8. Opportunity Lifecycle

```text
NoOffer
  -> Offered                 trigger ผ่านและ skill พร้อม
  -> Offered(refresh)        trigger เดิม + target เดิมตาม refresh policy
  -> Expired                 offer clock >= ExpiresAt  (clock ตาม §2.6 ไม่ใช่ Time.time)
  -> Cancelled               owner/target/party invalid หรือ scene teardown

Offered
  -> Starting                ผู้เล่นกดและ validation ผ่าน; lock OfferId กันการกดซ้ำ
  -> Failed                  reservation/placement/cast start ล้มเหลว

Starting
  -> Executing               animated external cast ถูก accept และได้ RequestId
  -> Committed               immediate cast commit ภายใน start call ผ่าน request-scoped callback
  -> Failed                  immediate payload fail/reject หรือยกเลิกก่อน commit; ไม่ stamp cooldown

Executing
  -> Committed               ได้ request-scoped post-reservation.Commit callback; publish ครั้งเดียว
  -> Failed                  payload fail/interrupt ก่อน commit; ไม่ publish

Committed
  -> Finished                playback/payload cleanup สำเร็จ
  -> Failed                  interruption/timeout หลัง commit; cooldown ไม่ refund
```

Validation ตอนสร้าง offer:

- evaluator รวบรวม matching candidates ของ owner ตาม stable authoring order แล้วเลือก priority
  สูงสุด/tie-break ตาม §2.2 ก่อนสร้าง offer เพียงรายการเดียว
- definition และ execution skill มีจริง
- owner อยู่ใน active party
- owner alive หาก rule บังคับ
- target resolve เป็น `CharacteContext` เมื่อเป็น character-targeted
- target alive หาก rule บังคับ
- skill charge พร้อมภายใต้ `IgnoreEnergyRespectCharge`
- chain/depth/dedupe policy ผ่าน

อย่าใช้ `CharacterSkillManager.CanStartExternalSkill(...)` ทั้งก้อนเป็น gate ตอนสร้าง offer เพราะ
method นี้รวม `IsSkillStartBlockedByAnimation()` อยู่ด้วย สมาชิกที่กำลังทำ Normal Attack จึงอาจเสีย
trigger ทั้งที่เป็น transient AI action ให้ offer creation query เฉพาะ structural validity + charge
ส่วน arbitration ของ animation/reservation ตรวจตอนกดตาม busy policy

Validation ซ้ำตอนกด:

- offer ยังไม่หมดเวลา
- owner/target ยังเป็น instance เดิมและยัง valid
- owner ไม่ถูก reserve โดยระบบอื่น
- placement ยังหาได้
- skill ยังมี charge
- game state ไม่อยู่ใน death, down, cinematic หรือ transition ที่ห้าม cast
- current action เป็น interruptible AI normal attack/locomotion หรือผ่าน busy policy; hard reservation
  จาก interruption/Combo อื่นต้อง reject

Cooldown ห้ามเริ่มตอน offer ปรากฏหรือหมดอายุ เริ่มผ่าน skill transaction เมื่อ cast commit เท่านั้น
executor ต้องตั้ง state/callback guard ก่อนเรียก start API และตรวจ callback flag หลัง API คืนค่าเพื่อ
ไม่เขียน state `Executing` ทับ `Committed`/`Failed` ที่เกิดแบบ synchronous

---

## 9. Targeting Policy

ค่าเริ่มต้นของ offensive Combo Skill คือ `EventTarget`

- เก็บ `SkillTargetHandle` ตอน offer ถูกสร้าง
- ไม่ re-resolve จาก aim ตอนกด เพราะอาจตีคนละตัวกับตัวที่ทำ trigger
- ถ้า target ตายก่อนกด ให้ cancel offer
- ไม่ auto-retarget ใน MVP
- ใช้ `SkillTargetHandle.TryResolveLiveContext(...)` เมื่อต้องการ actor เดิมสำหรับ placement/
  diagnostics และใช้ `TryResolveEffectTarget(...)` เมื่อต้องตรวจ active/alive eligibility; ห้ามอ่าน
  cached context แล้วเช็ค `context == null` เอง เพราะ handle เป็นเจ้าของ fake-null และ recycled
  instance-id guard
- `AITargetInfo` untargetable และ `HealthSystem` invincible ไม่ใช่เงื่อนไขเดียวกัน และไม่ควรถูก
  Combo controller เหมาเป็น universal reject: ค่าเริ่มต้นตรวจเพียง target active/alive แล้วปล่อยให้
  skill payload/targeting policy ตัดสินผล; หาก content ต้องห้าม boss phase ให้เพิ่ม eligibility policy
  แบบ explicit

เพิ่ม policy เมื่อมี use case จริง:

- `Self`
- `EventActor`
- `CurrentAimTarget`
- `LowestHealthPartyMember`
- `PartyArea`

สำหรับ character target ให้ resolve ผ่าน `CharacteContext`; physics ใช้เฉพาะ placement, obstruction, projectile/melee hit geometry ตามกติกาโปรเจกต์

ข้อจำกัดของ handle: guard ตรวจ destroy/fake-null, inactive eligibility และ instance-id mismatch ได้ แต่
object pool ที่ `SetActive(false)` แล้ว reuse **object เดิม** ภายหลังยังมี instance id เดิม เมื่อ active
อีกครั้ง handle อาจ resolve ผ่าน ดังนั้น summon/spawn/pool owner ต้องส่ง despawn/recycle lifecycle
ให้ opportunity controller cancel offer ทันที ห้ามพึ่ง handle อย่างเดียว และต้องมี regression test
deactivate -> reuse object เดิมแล้ว offer เก่าห้ามกลับมา valid

---

## 10. Execution Strategy

### 10.1 Vertical slice

ใช้ API single-actor execution ตั้งแต่ vertical slice และห้ามสร้าง serialized
`ChainAttackSequenceDef` แบบ one-step เป็น authoring layer ของ Combo:

```csharp
public PartyComboStartResult TryStart(
    PartyRuntimeActor actor,
    PartyComboSkillDef combo,
    SkillTargetHandle target,
    in PassiveEventContext triggerContext);
```

executor ต้อง:

1. reserve `FieldAllyMember`
2. snapshot origin/visibility/autonomy
3. resolve placement จาก `PartyComboExecutionProfile` และ target snapshot
4. suspend AI ผ่าน lifecycle เดิม
5. resolve combo-owned runtime entry และ start ด้วย `IgnoreEnergyRespectCharge`
6. ตั้ง request-scoped commit/failure callbacks และ offer state ก่อนเรียก start API
7. รองรับ callback ที่เกิด synchronous ใน immediate path และผูก RequestId สำหรับ animated path
8. รับ post-commit callback ตาม §2.4 แล้ว publish commit fact ครั้งเดียว
9. สร้าง child fact ด้วย FactId ใหม่, ChainId เดิม และ Depth + 1
10. restore actor/AI/visibility/reservation ทุก exit path

ภายใน executor adapter reuse `CharacterPlacementResolver`, `ChainAttackTeleportUtility` หรือ
transition implementation เดิมได้ แต่ public Combo definition/controller ต้องไม่รู้จัก whole-sequence
semantics และต้องไม่เก็บ serialized reference ไป legacy Chain asset

### 10.2 Field allies

reuse:

- `FieldAllyMember`
- `FieldAllyExecutionState`
- `FieldAllyAutonomyScope`
- `FieldAllyTransitionController`
- `ChainAttackTeleportUtility` หรือ central character placement resolver

อย่า duplicate BT/NavMesh suspend/restore ใน Combo controller

### 10.3 Helper

Helper ต้องผ่าน `AllyHelperManager` เพราะ lifecycle ต่างจาก field ally

- ใช้ metered helper skill path
- `IgnoreEnergyRespectCharge` เป็นค่า default ของ helper skill path อยู่แล้ว — อย่าเพิ่ม override
  ชั้นที่สองเพื่อ "ให้แน่ใจ" เพราะจะเกิดสองแหล่งความจริงของ cost policy
- preserve request-scoped visibility และ deactivation behavior
- อย่าใช้ FieldAllyMember assumptions กับ Helper จนกว่าจะเปลี่ยน topology อย่างเป็นทางการ

ข้อขัดแย้งที่ต้องรู้: `AllyHelperProcController` ยิงสกิลเองอัตโนมัติเมื่อ proc เข้าเงื่อนไข
ซึ่งขัดหลักการข้อ 2 ใน §1 ("AI ไม่ตัดสินใจกดแทนผู้เล่น") ระบบ Combo ไม่ได้ทำให้เรื่องนี้แย่ลง
แต่ต้องมี product decision ว่า helper proc ยังคง auto ต่อไปหรือถูกดึงเข้ามาอยู่ใต้ opportunity
model เดียวกัน — ดู §22

### 10.4 Player actor

MVP ไม่จำเป็นต้องเสนอ Combo Skill ของ actor ที่กำลังถูกควบคุม

ถ้าภายหลังรองรับ live switching:

- eligibility ต้องถาม active controlled actor service ไม่ใช้ `ctx is PlayerContext`
- UI slot ต้องตาม party index ไม่ตาม concrete context subtype
- actor ที่เพิ่งถูกสลับมาควบคุมต้องยกเลิกหรือแปลง pending offer ตาม explicit policy

### 10.5 Execution concurrency

“มี offer พร้อมกันหลายตัว” ไม่ได้แปลว่าใช้ executor ซ้อนกันได้โดยอัตโนมัติ และ
`ChainAttackCoordinator` เดิมรับ whole sequence ได้ครั้งละหนึ่งชุด จึงห้ามใช้ coordinator เป็น
execution gate ของ Combo vertical slice เพราะ link C ที่เปิดตอน B commit จะถูก reject ขณะ B ยัง
cleanup animation ไม่จบ

ค่าเริ่มต้นสำหรับ vertical slice:

- controller เก็บ offer ของคนละ actor พร้อมกันได้
- executor อนุญาต active execution คนละ actorพร้อมกัน แต่ actor เดียวห้าม re-enter
- ใช้ central `CharacterPlacementReservationService` ป้องกันตำแหน่งชนกัน
- hard cinematic/legacy Chain/Guaranteed Interruption reservation ยัง block ตาม arbitration policy
- vertical slice ไม่ใช้ global one-step sequence lock และไม่เลื่อน commit-link emission; หาก
  presentation ต้อง buffer input ในอนาคต buffer ต้องผูก `OfferId`/owner role และยังตรวจ expiry/
  validity ตาม policy ตอนเริ่มจริง

ต้องมี test B commit เปิด C แล้ว C ถูกกดได้ขณะ B ยังอยู่ช่วง recovery ไม่ใช่เพียงหลัง B restore ครบ

### 10.6 Runtime binding และ teardown

แนะนำให้วาง controller/executor บน Player prefab แล้วเพิ่ม reference ผ่าน `PlayerContext` ตามกฎ
context architecture จากนั้นแก้ `PartyRuntimeBinder.TryBind` ให้:

1. configure party actors, formation และ helper ตามเดิม
2. เรียก Combo controller/executor `BindParty(party)`
3. bind Player UI เพื่อให้ presenter เห็น controller ที่พร้อมแล้ว

ทุก `BindParty` ต้อง unbind party เก่าก่อน, dedupe `CombatEventBus` instance และ unsubscribe ครบเมื่อ
disable/scene teardown อย่าผูก first-time initialization กับ static `PartySpawnPoint.Spawned`

ต้องมี rollback/kill switches จาก `PartyComboFeatureFlags` reference เดียวที่ composition root:

- `runtimeEnabled`: เมื่อ false `PartyRuntimeBinder` ไม่ bind router/controller/executor, router ไม่
  subscribe bus, offer ทั้งหมดถูก cancel, executor reject `Disabled` และ Combo HUD ถูก clear/hide
- `phase4aPublishersEnabled`: Phase 4a Status publisher return ที่ source ก่อนสร้าง
  `PassiveEventContext`/FactId/metadata; EnteredBreak adapter ใช้ publisher gate เดียวกัน
- toggle ทั้งสองต้องปิดระบบใหม่ได้ระหว่าง playtest โดยไม่ revert code และไม่กระทบ legacy
  ChainReady/Break Finisher
- config ใหม่ default เป็น disabled จน vertical slice/Phase 4a acceptance ผ่าน แล้ว rollout จึงเปิด
  อย่าง explicit ต่อ prefab/config ที่ต้องการ

การ disable ซ้ำหรือ disable ระหว่าง offer/execution ต้อง idempotent และ restore actor/HUD/subscription
ครบเหมือน scene teardown

---

## 11. UI และ Input

สร้าง HUD แบบ party-slot driven ไม่ใช่ world-space prompt บนศัตรู

แต่ละ slot แสดง:

- portrait หรือ Combo Skill icon
- input glyph
- radial/linear expiry countdown
- ready pulse เมื่อ offer เปิด
- disabled reason เฉพาะกรณี offer ยังแสดงแต่กำลังกดไม่ได้ชั่วคราว
- cooldown stateหลังใช้ โดยอ่านจาก `SkillChargeStatus`

controller API ที่ HUD ต้องใช้ควรเป็น event/snapshot เช่น:

```csharp
public event Action<PartyComboOfferViewData> OfferChanged;
public float ComboClockNow { get; }
public bool TryConsumeOffer(
    ChainActorRole role,
    int expectedOfferId,
    out PartyComboRejectReason reason);
public bool TryGetOffer(ChainActorRole role, out PartyComboOfferViewData view);
```

HUD/input ต้องส่ง `OfferId` จาก immutable snapshot กลับมาพร้อม role; controller consume ได้เฉพาะเมื่อ
`expectedOfferId` ตรง active offer ปัจจุบัน มิฉะนั้น reject ด้วย `StaleOffer` เพื่อกัน callback/frame
เก่ากด offer ใหม่ของ role เดิมโดยไม่ตั้งใจ

`PartyComboOfferViewData.ExpiresAt` อยู่ใน time domain ของ controller ดังนั้น HUD ต้องคำนวณ
`remaining = Mathf.Max(0f, view.ExpiresAt - controller.ComboClockNow)` ห้ามใช้ `Time.time`,
`Time.unscaledTime` หรือนับ delta ของตัวเอง การอ่าน property นี้ต่อ frameเป็น O(1), ไม่มี allocation
และหยุดเองเมื่อ `GlobalTimeScaleManager.Instance.IsPaused` เพราะ controller clock ไม่เดิน

Input:

- ใช้ New Input System callbacks ใน `PlayerInputHandler`
- อย่า hardcode `Keyboard.current`
- อย่าให้ `CharacterSkillManager.Update()` อ่าน input เพราะ manager อยู่บนตัวละครทุกตัว
- ไม่ควร overload `OnPartyCommandSlot1..4` ที่ปัจจุบันใช้เลือก command โดยไม่มี product decision เพราะผู้เล่นอาจกดเพื่อเลือกแล้วเผลอ consume Combo
- ทางที่ปลอดภัยคือเพิ่ม actions/callbacks สำหรับ Combo slot หรือกำหนด interaction layer ที่ชัดเจน แล้ว bind ผ่าน prefab/UI

เมื่อไม่มี offer การกดต้องไม่ทำอะไรและไม่ consume action ของระบบอื่น เว้นแต่ input design กำหนด multiplexing อย่างชัดเจน

`ChainReadyPromptView` ไม่ใช่ Combo HUD และไม่ควรถูกดัดแปลงให้รู้เรื่องสมาชิกทีม ใช้แยกกับ Break Finisher ไปก่อน

---

## 12. Trigger Semantics ที่ต้องล็อกก่อนสร้าง content จำนวนมาก

หัวข้อนี้เป็นการล็อก **นิยาม** เท่านั้น อย่าอ่านเป็นสถานะการมีอยู่ของระบบ — Final Strike,
Arts Reaction และ tag `Infliction` ยังไม่มีในโปรเจกต์ ดู §3.7 ก่อนวางแผนงาน
ทั้งสามค่าเป็น reserved post-MVP trigger kinds และ authoring validator ต้อง reject ใน MVP

### Final Strike

ต้องนิยามว่าเป็น final authored step ของ normal attack sequence ที่สร้าง applied damage จริง ไม่ใช่:

- การฆ่าศัตรูทั่วไป
- hit สุดท้ายก่อน reload
- animation จบแต่ไม่โดนเป้า

ถ้า basic attack มีทั้งยิงและ melee ให้แต่ละ owner system publish fact ตาม definition ของตน

### Arts Reaction

Combo system รับเพียงผล `reactionId`, source actor และ target จาก Arts Reaction system

Combo systemไม่ควรคำนวณเองว่าสถานะสองชนิดรวมกันเป็น reaction ใด

### Infliction count

ต้องตกลงว่า “จำนวน Infliction” หมายถึงอย่างใดอย่างหนึ่ง:

- จำนวน status definitions ที่มี tag `Infliction`
- ผลรวม stack ของ status ที่มี tag `Infliction`
- จำนวน application events ภายในช่วงเวลา

คำแนะนำเริ่มต้น: นับผลรวม current stacks ของ active effects ที่มี tag `Infliction` บน event target หลัง Applied/StackChanged event

### Target has status

- author ด้วย `StatusEffectDef` หรือ stable `effectId`/tag
- query `targetContext.StatusEffects.ActiveEffects`
- trigger เมื่อ status ถูก apply/refresh/stack เท่านั้น ไม่ poll ทุก frame
- ถ้าสถานะมีอยู่ก่อน Combo cooldown พร้อมขึ้นมาใหม่ จะไม่เปิด offer จนเกิด relevant event ใหม่; อย่าธนาคาร trigger ข้าม cooldownใน MVP

### Ally used Combo Skill

- primary fact คือ `ComboSkillCommitted`
- source relation ต้องเป็น `OtherPartyMember`
- target ของ offer ถัดไปใช้ target เดิมจาก committed Combo เป็นค่าเริ่มต้น
- ถ้า skill ก่อนหน้าไม่มี hostile target definition ถัดไปต้องมี target policy ที่รองรับ ไม่ fallback แบบเงียบ ๆ

---

## 13. Failure และ Cancellation Policy

| เหตุการณ์ | ผลที่ต้องการ |
|---|---|
| Offer หมดเวลา | ปิด HUD; ไม่ใช้ cooldown |
| Owner down/dead ก่อนกด | cancel offer |
| Controlled actor/Player role down หรือ dead ขณะมี B/C offer | cancel ทุก offer, cleanup active Combo reservations และ push empty snapshot ให้ HUD clear/hide; ไม่รอ expiry |
| Target ตายก่อนกด | cancel offer; ไม่ retarget |
| Owner กำลังทำ interruptible AI normal attack ตอน trigger | สร้าง offerได้; ยังไม่แทรก AI จนกด |
| Owner ติด hard lock ตอน trigger | ไม่สร้าง offerเฉพาะ policy ที่ระบุ; death/down/party invalid ต้อง reject เสมอ |
| Owner busy หลัง offer เปิด | offer อยู่ได้; ตอนกดให้ arbitration แทรก transient AI action หรือรายงาน Busy; timer เดินต่อ |
| Owner ถูก Guaranteed Interruption reserve หลัง offer เปิด | คง offer แต่ mark temporarily Busy และ timer เดินต่อ; กดระหว่าง reservation ให้ reject Busy โดยไม่ consume, หลัง restore กดได้ถ้ายังไม่หมดเวลา; ถ้า owner down/dead ให้ใช้กฎ cancel |
| Placement หาไม่ได้ | fail attempt; ปิด offerหรือคงไว้ตาม retry policy; MVP ปิดและไม่ใช้ cooldown |
| Cast rejected ก่อนเริ่ม | ไม่ใช้ cooldown; cleanup reservation |
| Interrupted ก่อน cast commit | ไม่ใช้ cooldown; ไม่ publish ComboSkillCommitted |
| Interrupted หลัง commit | cooldown คงอยู่; cleanup actor; chain ถัดไปที่เปิดตอน commit ยังคงถูกต้อง |
| Scene/room transition | cancel offers และ restore reservationsทั้งหมด |
| Party member ถูกเปลี่ยน | cancel offer ของ role นั้น; rebuild definitions/subscriptions |
| Controller disabled | unsubscribeทุก bus; cancel offers; executor cleanup |
| กดตอนมี `GlobalTimeScaleManager` pause token (cutscene/menu) | reject ด้วยเหตุผล Paused; ไม่ consume offer; timer ไม่เดินระหว่าง pause ตาม §2.6 |
| มีสอง offer แล้วกดทั้งคู่ใน frame เดียว | executor ต้องมี guard ของตัวเอง; event-level dedupe ไม่ครอบกรณีนี้ |
| UI ส่ง role ถูกแต่ `expectedOfferId` เก่า | reject `StaleOffer`; ห้าม consume offer ใหม่ของ role เดิม |
| Target ยังไม่ตายแต่ untargetable/invincible | ใช้ target eligibility policy; ค่าเริ่มต้นไม่ universal-reject และให้ payload ตัดสินผล ถ้า payload ไม่เกิดผล transaction ต้องไม่ commit |
| Owner กำลัง execute combo ก่อนหน้าอยู่ | re-entrancy guard ที่ executor; ไม่ซ้อน reservation (รูปแบบเดียวกับ summon re-entrancy guard ที่มีอยู่) |
| Actor B commit แล้ว C ถูกกดระหว่าง B recovery | อนุญาตเมื่อเป็นคนละ actor; placement/reservation ต้องไม่ชนกันตาม §10.5 |
| Combo offer กับ ChainReady prompt live พร้อมกัน | ทั้งสอง prompt อยู่ร่วมกันได้ แต่ input ต้องแยกคนละ action ตาม §11; ห้ามให้ปุ่มเดียว consume ทั้งสองระบบ |

ทุก cleanup path ต้อง idempotent

---

## 14. Migration Plan

### Phase 0 — Lock product decisions

ต้องตอบก่อน merge implementation เต็มระบบ:

1. Helper จะคงเป็น summon/hide หรือเป็น field member คนที่ 3?
2. ต้องการ live character switching หรือยังใช้ Player role คงที่?
3. Break Finisher เดิมจะเก็บ, rename หรือถอดหลัง migration?
4. หลัง MVP ต้องการเพิ่ม Combo loadout/upgrade variants หรือคง fixed character identity?
5. input layout บน keyboard/gamepad/controller เป็นแบบใด?
6. offer พร้อมกันหลายตัวให้กดอิสระหรือมี queue/priority และ execution ต่าง actor ซ้อนช่วง recovery ได้หรือไม่?

คำตอบเริ่มต้นที่แนะนำสำหรับ vertical slice:

- topology เดิม
- ไม่มี live switching
- เก็บ Break Finisher เดิม
- หนึ่ง Combo Skill ต่อ character
- Combo เป็น fixed skill ไม่มี upgrade tree และไม่ต้องอยู่ใน `CharacterStats.skillSlots`
- ใช้ combo-owned runtime entry ที่ snapshot เป็น `null`; validator reject definition ที่ซ้ำกับ
  Battle Skill หรือ Helper proc ใน MVP
- offers พร้อมกันหลาย actor ได้
- actor ละหนึ่ง offer
- execution ต่าง actor ซ้อนช่วง recovery ได้; actor เดียวห้าม re-enter
- input แยกจาก party command selection

### Phase 0.5 — Serialized enum hardening

ทำ PR เล็กแยกจาก Combo runtime ก่อน Phase 1:

- ใส่ explicit numeric values `0..10` ให้ `PassiveEventType` เดิมโดยห้ามเปลี่ยนความหมาย
- เพิ่ม comment `Serialized: append only` และ regression test ที่ assert ชื่อ/ตัวเลขเดิมทุกค่า
- append `StatusApplied = 11`, `StatusStackChanged = 12`, `ComboSkillCommitted = 13`
- audit serialized passive/chain assets แบบ focused smoke check ก่อนเริ่มสร้าง Combo assets

ห้ามรวมการ reorder/rename enum หรือ content migration อื่นใน PR นี้

### Phase 1 — Core data ownership, trigger and opportunity

ก่อนอื่น: ย้าย `ChainActorRole` ไป `Assets/Scripts/Party/ChainActorRole.cs` ตาม §2.8 เพื่อไม่ให้
โค้ดใหม่พึ่งไฟล์ในกลุ่ม retire

เพิ่ม `CharacterStats.partyComboSkill` ใน Phase นี้พร้อม pure definitions เพื่อให้ controller และ
Phase 2 vertical slice resolve Combo definition ของ B/C จาก authoring source จริงตาม §6.3;
ยังไม่ต้อง migrate asset เดิมหรือสร้าง production content จำนวนมาก

เพิ่มไฟล์ภายใต้ `Assets/Scripts/PartyCombo/`:

- `PartyComboSkillDef.cs`
- `PartyComboExecutionProfile.cs`
- `PartyComboFeatureFlags.cs`
- `PartyComboTriggerRule.cs`
- `PartyComboOpportunity.cs`
- `PartyComboTriggerEvaluator.cs`
- `PartyCombatEventRouter.cs`
- `PartyComboOpportunityController.cs`

เพิ่ม EditMode smoke tests:

- trigger match/non-match
- FactId/ChainId normalization และ same-fact dedupe
- target/source relation
- offer expiry
- same-chain recursion guard
- same-target refresh
- one offer per actor
- simultaneous offers across actors
- max depth
- owner/target invalidation
- target despawn/recycle cancels offer; reactivating pooled object เดิมไม่ทำให้ stale offer valid อีก

ยังไม่ต้องมี animation/UIจริง สามารถ consume ผ่าน test API

Exit criteria:

- event จาก Player และ PartySlot1/2 มาถึง router
- ทุก fact มี non-zero FactId/ChainId ก่อน evaluator และ definition ทุกตัวเห็น IDs ชุดเดียวกัน
- controller resolve `partyComboSkill` ของแต่ละ actor จาก `CharacterStats` ได้
- trigger สร้าง offer แต่ไม่ start skill
- no per-frame actor scan
- disable/rebind ไม่เกิด duplicate subscription
- `runtimeEnabled == false` ไม่ subscribe router และไม่สร้าง offer

### Phase 2 — Single field-ally execution vertical slice

เพิ่ม:

- `PartyComboSkillExecutor.cs`
- adapter เข้ากับ `FieldAllyMember`/placement/external skill cast
- `ComboSkillCommitted` fact

สร้าง content ทดสอบ:

- B: trigger จาก `EnteredBreak` test fact หรือ `TargetHasStatus` ซึ่งมี source system จริงใน MVP
- C: trigger จาก ComboSkillCommitted ของ B
- skill ทั้งคู่มีหนึ่ง charge และ recharge time ชัดเจน

ก่อนผูก `ComboSkillCommitted` ให้เพิ่ม request-scoped post-commit callback ใน
`CharacterSkillManager` ตาม §2.4; ห้ามใช้ `CastReleased` หรือ subscribe หลัง start API คืนค่า

Exit criteria:

```text
publish EnteredBreak on target X
  -> B offer appears in runtime state
  -> no cast before input
  -> consume B
  -> B executes against X without Energy cost
  -> B cooldown starts on commit
  -> B commit creates child FactId ใหม่และ opens C offer for X ด้วย ChainId เดิม
  -> consume C ได้แม้ B ยังอยู่ recovery
  -> C executes
  -> every actor returns to AI/formation
```

ตรวจทั้ง animated และ immediate path รวม target ตาย, actor down, placement fail, payload failure
และ interrupt ก่อน/หลัง commit

### Phase 3 — HUD and Input

เพิ่ม:

- `PartyComboHudPresenter.cs`
- `PartyComboSlotView.cs`
- UI fields บน Player UI prefab/context
- Input Actions และ callbacks
- binding ใน `PlayerUIRuntimeBinder`

Exit criteria:

- UI ถูก bind กับ spawned party runtime ไม่อ้าง scene actor โดยตรง
- portrait/icon และ role ถูกต้องหลังเปลี่ยน party
- countdown ตรงกับ controller
- mouse/keyboard และ gamepad glyph ตาม input design
- ไม่มี offer แล้วกดไม่ไปเรียก skill
- HUD teardown/reload scene ไม่มี stale subscription
- runtime kill switch clear/hide HUD และเปิด legacy Chain prompt ได้ตามเดิม

### Phase 4 — Real gameplay publishers

แยกเป็นสองกลุ่มตาม §3.7 เพราะไม่ใช่งานขนาดเดียวกัน

**4a — publisher ล้วน (owner system มีอยู่แล้ว):**

- Status Applied/StackChanged — เชื่อม `EffectLifecycleChanged` เข้า credited bus
  โดย early-filter lifecycle type ที่ source; ห้ามสร้าง context สำหรับ `Ticked`/`Removed`
- Entered Break adapter — อ่าน `CombatEventMetadata.EnteredChainReady` ที่ publish อยู่แล้ว
- ทั้งสอง publisher return ก่อนสร้าง context เมื่อ `phase4aPublishersEnabled == false`

**4b — ต้องสร้าง owner system ก่อน (ไม่ใช่แค่ publisher):**

- Final Strike — ต้องเพิ่มแนวคิด "step สุดท้ายของ attack sequence" เข้า basic attack/`MeleeComboSO` ก่อน
- Arts Reaction — ยังไม่มีระบบ reaction เลย; เป็น workstream แยก ไม่ควรอยู่ใน migration นี้
- tag `Infliction` — ต้องนิยาม tag และติดให้ status assets ก่อนจะนับได้

4a ทำได้ทันทีหลัง Phase 3 ส่วน 4b ต้องมี product decision และแผนของตัวเอง Combo Skill ต้อง
ส่งมอบได้โดยไม่รอ 4b

Exit criteria (ของ 4a):

- publisher ปล่อย event หลัง gameplay result สำเร็จจริง
- preserve attribution, attack id, chain id และ depth
- summon/helper credit ถูก actor ที่ถูกต้อง
- failed/no-op status mutation ไม่ publish Applied/StackChanged และ EnteredBreak adapter ทำงานเฉพาะ
  เมื่อ `EnteredChainReady` เป็นจริง
- `AppliedNew`/`Refreshed` map เป็น `StatusApplied`, `StackChanged` map เป็น
  `StatusStackChanged`; `Ticked`/`Removed` ไม่สร้าง FactId/context และไม่แตะ bus subscriber
- profile burst ของ Status Applied/StackChanged ก่อนเปิด publisher จริง และบันทึก event count/
  subscriber cost เทียบ baseline ตาม §17

### Phase 5 — Character authoring and migration

- editor validation สำหรับ duplicate/empty combo id
- validation ว่า execution skill/trigger/offer duration ครบ
- validation ว่า `PartyComboExecutionProfile` ครบและไม่มี serialized legacy Chain type
- validation ว่า execution definition ไม่ซ้ำ Battle Skill/Helper proc/Combo role อื่นใน MVP
- migration tool สำหรับ asset เดิมที่แปลงได้
- สร้าง PartyComboSkillDef จริงของตัวละคร
- อัปเดต prefab/runtime binder ถ้าต้องมี references ใหม่

อย่าแก้ `.csproj` เพื่อให้ไฟล์ใหม่ติด build; ใช้ Unity refresh และ `CheckAssemblyBuild.ps1`

### Phase 6 — Retire or reframe legacy Chain

ทำเมื่อ Combo vertical slice, UI และ content ผ่านแล้วเท่านั้น:

- ตัด auto-proc path จาก `ChainAttackProcController`
- ลบ CP cost ของ Combo path
- เปลี่ยนชื่อ user-facing ChainReady sequence เป็น Break Finisher หากเก็บ
- migrate/remove `SkillChainDef` assets อย่างมีเครื่องมือ
- ถอด `OpenChainSkill`/manual Chain input เฉพาะเมื่อ replacement พร้อม
- ลบ legacy fields หลัง asset audit และ serialization migration

---

## 15. File Touch Map

### ไฟล์ใหม่ที่คาดว่าจะมี

- `Assets/Scripts/Party/ChainActorRole.cs` (ย้ายมาจาก `AI/ChainAttack/ChainAttackSequenceDef.cs` ตาม §2.8)
- `Assets/Scripts/PartyCombo/PartyComboSkillDef.cs`
- `Assets/Scripts/PartyCombo/PartyComboExecutionProfile.cs`
- `Assets/Scripts/PartyCombo/PartyComboFeatureFlags.cs`
- `Assets/Scripts/PartyCombo/PartyComboTriggerRule.cs`
- `Assets/Scripts/PartyCombo/PartyComboOpportunity.cs`
- `Assets/Scripts/PartyCombo/PartyComboTriggerEvaluator.cs`
- `Assets/Scripts/PartyCombo/PartyCombatEventRouter.cs`
- `Assets/Scripts/PartyCombo/PartyComboOpportunityController.cs`
- `Assets/Scripts/PartyCombo/PartyComboSkillExecutor.cs`
- `Assets/Scripts/UIScripts/PartyCombo/PartyComboHudPresenter.cs`
- `Assets/Scripts/UIScripts/PartyCombo/PartyComboSlotView.cs`
- `Assets/Scripts/Editor/PartyCombo/PartyComboCoreSmokeTests.cs`
- `Assets/Scripts/Editor/PartyCombo/PartyComboExecutionSmokeTests.cs`
- `Assets/Scripts/Editor/PartyCombo/PartyComboAuthoringValidator.cs`

### ไฟล์เดิมที่น่าจะต้องแก้

- `Assets/Scripts/CharacterStats/Scripts/CharacterStats.cs`
- `Assets/Scripts/Passives/PassiveTypes.cs`
- `Assets/Scripts/Passives/PassiveEventContext.cs` สำหรับ `FactId`
- `Assets/Scripts/Passives/CombatEventMetadata.cs`
- `Assets/Scripts/Passives/CombatEventBus.cs` สำหรับ FactId factory/child propagation
- `Assets/Scripts/StatusEffects/StatusEffectController.cs`
- `Assets/Scripts/Party/PartyRuntime.cs`
- `Assets/Scripts/Party/PartyRuntimeBinder.cs`
- `Assets/Scripts/Party/PlayerUIRuntimeBinder.cs`
- `Assets/Scripts/Player/PlayerContext.cs`
- `Assets/Scripts/Player/PlayerInputHandler.cs`
- `Assets/Scripts/Player/Skill/CharacterSkillManager.cs` สำหรับ request-scoped post-commit callback,
  immediate `CastStarted` parity และ combo-owned fixed runtime entry

### Post-MVP workstream ที่ไม่อยู่ใน migration file touch map

- owner system/publisher ของ Final Strike — ยังไม่มี ดู §3.7
- Arts Reaction system/publisher — ยังไม่มีทั้งระบบ ดู §3.7
- status asset/tag migration สำหรับ Infliction

### Legacy implementation ที่ Combo adapter reuse ได้ก่อนและ retire ทีหลัง

- `Assets/Scripts/AI/ChainAttack/FieldAllyMember.cs`
- `Assets/Scripts/AI/ChainAttack/FieldAllyAutonomyScope.cs`
- `Assets/Scripts/AI/ChainAttack/FieldAllyTransitionController.cs`
- `Assets/Scripts/AI/ChainAttack/ChainAttackTeleportUtility.cs`

Combo asset ห้าม serialize type จากไฟล์เหล่านี้; adapter ต้องอยู่หลัง
`PartyComboExecutionProfile`/executor เท่านั้น

### Legacy Chain ที่คงไว้เพื่อ regression แต่ไม่ใช้เป็น Combo execution gate

- `Assets/Scripts/AI/ChainAttack/ChainAttackTeleportProfileDef.cs`
- `Assets/Scripts/AI/ChainAttack/ChainAttackCoordinator.cs`
- `Assets/Scripts/AI/ChainAttack/ChainAttackProcController.cs`
- `Assets/Scripts/AI/ChainAttack/SkillChainDef.cs`
- `Assets/Scripts/AI/ChainAttack/ChainAttackSequenceDef.cs`

---

## 16. Testing Matrix

### Pure/EditMode

- `PassiveEventType` serialized numeric contract คงค่าเดิม `0..10` และ Combo additions `11..13`
- ทุก trigger kind match และ reject ถูกต้อง
- authoring validator reject reserved `FinalStrike`/`ArtsReaction`/`InflictionCountReached` ใน MVP
- source relation แยก controlled/self/other-party ได้
- event target ถูก snapshot และไม่ retarget
- status tag/definition และ stack threshold ถูกต้อง
- status publisher early-filter: AppliedNew/Refreshed/StackChanged map ถูกต้อง และ Ticked/Removed
  ไม่สร้างหรือ publish combat fact
- same-fact dedupe ด้วย `(FactId, ComboSkillId, OwnerRole)`
- same-chain recursion guard ด้วย `(ComboSkillId, ChainId, OwnerRole)`
- recursion depth
- refresh/replacement policy
- expiry clock ไม่ถูก world slow ยืด และหยุดเดินระหว่าง pause
- simultaneous offers
- same-owner multi-match เลือก priority สูงสุด และ tie-break ตาม stable authoring order
- party rebind/unsubscribe
- runtime kill switch prevents subscription/HUD bind and disabling mid-offer cancels/cleans up idempotently
- publisher kill switch returns before FactId/context creation
- root fact `FactId/ChainId == 0` ถูก normalize ครั้งเดียว; evaluator หลาย definition เห็น IDs
  ชุดเดียวกัน; child commit ได้ FactId ใหม่และรักษา ChainId (§7.4)
- router ไม่ subscribe bus ของ actor ตัวอื่นข้าม party slot — เป็น shape เดียวกับบั๊ก context
  cross-bind ที่เคยเกิด และ `includeInactive` audit ทั้งโปรเจกต์ยังไม่ปิด
- สลับตัวละครใน party slot กลางรันแล้ว definitions/subscriptions ถูก rebuild

### Execution/EditMode และ manual/focused Play Mode

**ข้อจำกัดของ assembly:** test ปัจจุบันใต้ `Assets/Scripts/Editor` compile เป็น
`Assembly-CSharp-Editor` และ `CheckAssemblyBuild.ps1` ไม่ครอบ `Editor/` code จึงต้องให้ Unity
compile และรัน EditMode suite เพิ่มด้วย Test Runner อย่างไรก็ตาม Editor test สามารถ instantiate
GameObject/ScriptableObject และ `AddComponent` runtime type จาก `Assembly-CSharp` ได้ เหมือน
`SkillCastTransactionSmokeTests` และ `CharacterPlacementBaselineTests`; ไม่ควรโยนทุก actor case
ไปเป็น manual โดยไม่ลองทำ deterministic EditMode fixture ก่อน

animated path ที่ต้องใช้ live Animancer/timeline, NavMesh scene หรือ actual input loop จึงค่อยเป็น
focused/manual Play Mode check จนกว่าจะย้าย runtime ที่เกี่ยวข้องและ tests เข้า asmdef ที่อ้างอิงกัน
ได้อย่างถูกต้อง; อย่าสร้าง test asmdef แล้วคาดว่าจะ reference `Assembly-CSharp` ได้โดยตรง

- free Energy but respects charge
- cooldown starts only on commit
- event order ต้องเป็น Started -> Released -> Committed สำหรับ success
- payload failure อาจมี Released -> ExecutionFailed แต่ต้องไม่มี Committed
- immediate path ยิง request-scoped Committed/Failed ก่อน start API คืนค่าได้โดย state ไม่ถูกเขียนทับ
- immediate และ animated path publish Committed เพียงครั้งเดียวต่อ RequestId/offer
- Combo fixed runtime entry มี snapshot เป็น `null` แม้ character มี Helper proc ที่ใช้ระบบ external cast
- validator reject SkillGemDefinition ที่ reuse ข้าม Battle/Helper/Combo ใน MVP
- save/load ไม่เขียน Combo ลง `selectedSkillOptions`; `OnLoad` และ late-resolved combo entry เริ่มด้วย
  full charge ตาม non-persistent charge contract
- pre-commit interruption refunds reservation/charge ตาม skill transaction
- post-commit interruptionไม่ refund cooldown
- owner AI ถูก suspend และ restore
- NavMesh/BT/visibility restore ครบทุก failure path
- target death before input
- target death during execution
- placement blocked
- owner down/dead
- controlled actor down/dead cancels all offers and clears HUD state immediately
- Guaranteed Interruption reservation keeps offer, rejects press as Busy without consume, and allows
  press after restore if unexpired
- helper hidden/deactivated cleanup
- room transition/disable cleanup
- B commit เปิด C และ C start ได้ระหว่าง B recovery โดยไม่ชน placement/reservation

### UI/Focused Play Mode

- correct party slot/icon/glyph
- countdown and expiry
- countdown derives from `ExpiresAt - ComboClockNow`, stops on pause token และไม่ถูก hitlag/world slow ยืด
- offer appearance does not cast
- each press consumes only intended role
- multiple offers can be selected deterministically
- controller rejects `TryConsumeOffer(role, oldOfferId, ...)` หลัง role เดิมมี offer ใหม่
- UI bind after party spawn
- UI unbind on teardown

### Regression

- ChainReady -> F -> legacy Break Finisher ยังทำงานระหว่าง migration
- Stagger state completes after legacy sequence success/fail/cancel
- Guaranteed Interruption ยัง reserve/restore ally ได้
- Helper command และ helper proc cooldown ไม่ถูก reset
- ordinary Battle Skill ยังใช้ Energy/charge policy เดิม
- passive Hit/Kill rules ไม่ trigger ซ้ำจาก router
- Character placement baseline tests ยังผ่าน

---

## 17. Performance Requirements

- ห้ามใช้ `FindObjectsByType` หรือ physics overlap ทุก frame เพื่อหา party/targets
- `CombatEventBus` เป็น multicast hot path: `PassiveController` และ
  `WeaponAffixRuntimeController` subscribe bus เดียวกัน โดยตัวหลัง dispatch ต่อให้
  `ConfiguredWeaponAffixBehavior.OnCombatEvent`; การเพิ่ม `StatusApplied`, `StatusStackChanged`
  และ `ComboSkillCommitted` ทำให้ subscriber เดิมถูกเรียกและจ่าย traversal/filter cost แม้ enum
  ไม่ match rule เดิม
- ก่อนเปิด Phase 4a publisher ให้ profile กรณี status apply/refresh/stack burst ด้วยจำนวน actor/
  effect สูงสุดที่คาดจริง วัด event count ต่อ frame, GC allocation และ CPU ของ
  `PassiveController.HandlePassiveEvent`, `WeaponAffixRuntimeController.OnCombatEvent` และ router;
  ถ้า cost สูงให้เพิ่ม cheap type gate/batched adapter ที่ owner boundary โดยห้ามเปลี่ยน semantics
- router subscribe จาก `PartyRuntime.Actors` ตอน bind/rebind
- opportunity controller ทำงานเมื่อมี event, input, expiry หรือ lifecycle change
- actor count สูงสุดเล็ก แต่ยังควรใช้ dictionary ตาม `ChainActorRole`
- หลีกเลี่ยง LINQ/allocation ใน hot combat event path
- HUD รับ snapshot เมื่อ state เปลี่ยนและ extrapolate countdown เอง แทน controller broadcast ทุก frame
  โดยอ่าน `controller.ComboClockNow` ตาม §11 ไม่ใช่ `Time.time` หรือ local delta ของตัวเอง
- cleanup dead Unity object references เมื่อ party/scene เปลี่ยน
- debug logging ต้อง opt-in และระบุ combo id, offer id, fact id, role, target, chain id, depth และ reject reason

---

## 18. Documentation ที่ต้องอัปเดตเมื่อ implement

- สร้าง `Docs/SYSTEMS/PARTY_COMBO.md` เป็น canonical subsystem entry point สำหรับ runtime flow,
  data contracts, authoring, feature flags, save/load, failure policy และ debugging; handoff ฉบับนี้
  คงเป็น migration/history note ไม่ใช่เอกสารใช้งานหลักหลังส่งมอบ
- `Docs/GAMEPLAY_OVERVIEW.md`
  - เปลี่ยนคำอธิบาย Chain Attack เป็น Combo Skill flow
  - อธิบายความสัมพันธ์กับ Break/ChainReady
- `Docs/SYSTEMS/SKILL_SYSTEM.md`
  - character-owned Combo Skill
  - `IgnoreEnergyRespectCharge`
  - cast commit และ cooldown semantics
  - fixed Combo save/load exclusion และ charge reset-on-load
- `Docs/SYSTEMS/AI_AND_TARGETING.md`
  - AI normal attack vs player-commanded Combo
  - autonomy suspension/restore
  - event-target snapshot
- `Docs/ARCHITECTURE/COMBAT_EVENT_BUS.md`
  - event types/metadata/publishers ใหม่
  - party router และ provenance
- `Docs/PREFABS_AND_AUTHORING.md`
  - Combo HUD, PlayerContext/binder, CharacterStats และ feature-flag setup/default-off rollout
- `Docs/VALIDATION.md`
  - รายการ test suites ใหม่และ manual validation

ถ้ามีการเปลี่ยน build/validation tooling เท่านั้นจึงแก้ `Docs/VALIDATION.md` ส่วน workflow

---

## 19. Build and Validation Rules

หลังแก้ C# ให้ validate ด้วยคำสั่งที่โปรเจกต์กำหนดเท่านั้น:

```powershell
powershell -ExecutionPolicy Bypass -File 'P:\Game_RB_Project\RB_Project\Assets\Scripts\CheckAssemblyBuild.ps1'
```

**Repository-rule conflict ณ 2026-09-04:** root `CLAUDE.md` ระบุให้ใช้ `powershell -File ...`
เพราะ command classifier แต่ root `AGENTS.md` ซึ่ง `CLAUDE.md` ระบุเองว่าเป็น binding rulebook
กลับบังคับคำสั่งด้านบนพร้อม `-ExecutionPolicy Bypass` เอกสารนี้ยึด `AGENTS.md` จนกว่าเจ้าของโปรเจกต์
จะแก้สองไฟล์ให้ตรงกัน; ผู้รับช่วงห้ามสลับคำสั่งเองแบบเงียบ ๆ หาก classifier บล็อกให้รายงาน
instruction conflict แทนการใช้ validation path อื่น

ห้าม:

- `dotnet build` ตรงกับ Unity `.csproj`
- build ที่เขียน `_buildbin` หรือ `_buildobj` ใต้ `Assets/Scripts`
- ย้ายคลาสใหม่ไปไว้ไฟล์อื่นเพื่อแก้ `.csproj` ยังไม่ refresh
- แก้ generated `.csproj`/`.sln`

เมื่อเพิ่ม C# class ให้เก็บหนึ่ง intended class ต่อหนึ่ง `.cs` file และให้ Unity refresh/regenerate project files ตามปกติ

---

## 20. Definition of Done

ระบบ Combo Skill ถือว่าส่งมอบได้เมื่อ:

1. สมาชิก AI ทำ normal combat ต่อได้โดยไม่ auto-cast Combo Skill
2. combat fact ที่ถูกต้องสร้าง offer ให้ actor ที่ถูกต้องเท่านั้น
3. offer แสดงบน HUD พร้อม target และ countdown ที่ถูกต้อง
4. ไม่มีการ execute ก่อนผู้เล่นกด
5. กดแล้ว actor ใช้สกิลกับ target snapshot เดิม
6. Combo Skill ไม่เสีย Energy แต่ใช้ charge/cooldown จริง
7. cooldown เริ่มเมื่อ cast commit ไม่ใช่ตอน offer เปิด
8. request-scoped post-commit callback แยกจาก `CastReleased`, รองรับ immediate commit ก่อน start
   API คืนค่า และ `ComboSkillCommitted` เปิด offer ถัดไปเพียงครั้งเดียว
9. ทุก fact มี FactId/ChainId ถูกต้อง, same-fact dedupe ไม่กลืน fact คนละรายการ และไม่มี infinite loop
10. owner/target death, interrupt, placement failure, timeout และ scene teardown cleanup ครบ
11. AI, NavMesh, visibility, collision และ reservation ถูก restore ทุก exit path
12. Helper ใช้ lifecycle ถูกต้องตาม topology ที่ตกลง
13. legacy Break/ChainReady behavior เป็นไปตาม migration decision
14. C# validation ผ่านด้วย `CheckAssemblyBuild.ps1`
15. EditMode/PlayMode/manual regression ตาม testing matrix ผ่าน
16. มี `Docs/SYSTEMS/PARTY_COMBO.md` เป็น canonical entry point และเอกสาร gameplay, skill,
    AI/targeting, event bus, prefab authoring และ validation ถูกอัปเดต
17. B -> C chain ยังกด C ได้ระหว่าง B recovery ตาม concurrency policy
18. Combo ใช้ combo-owned fixed runtime entry ที่ไม่มี Helper snapshot รั่วเข้ามา
19. Combo assets อ้าง `PartyComboExecutionProfile` และไม่ serialize type จาก legacy Chain
20. consume API ตรวจทั้ง role และ expected OfferId; stale UI callback ใช้ offer ใหม่ไม่ได้
21. fixed Combo ไม่สร้าง save selection และ charge เริ่มเต็มหลัง load ตาม contract ปัจจุบัน
22. MVP author ได้เฉพาะ `ComboSkillCommitted`, `TargetHasStatus` และ `EnteredBreak`; reserved trigger
    kinds ถูก validator ปฏิเสธ
23. runtime/publisher kill switches ปิด Combo ได้โดยไม่แตะ legacy Chain และ cleanup state ครบ
24. `PassiveEventType` เดิมมี explicit `0..10` และ Combo facts ใช้ `11..13` โดยไม่เปลี่ยน asset meaning
25. Status publisher early-filter `Ticked`/`Removed` ก่อนสร้าง context และไม่ fan-out event สองชนิดนี้
26. same-owner multi-match เลือก priority/tie-break ได้ deterministic โดยไม่พึ่ง dictionary order
27. HUD countdown อ่าน `ComboClockNow` และตรงกับ pause/hitlag/world-slow contract
28. target despawn/recycle cancel stale offer แม้ pooled object เดิมถูก activate กลับมา

---

## 21. งานแรกที่แนะนำให้ผู้รับช่วงทำ

อย่าเริ่มจากลบ Chain เดิมหรือทำ HUD

เริ่ม vertical slice ที่พิสูจน์ contract นี้ก่อน:

```text
PartyRuntime มี Player + PartySlot1 + PartySlot2
  -> router subscribe bus ทั้งสาม
  -> test publishes EnteredBreak(target X, FactId F1, ChainId C1) จาก Player
  -> PartySlot1 definition match
  -> controller creates offer แต่ยังไม่ cast
  -> test consumes PartySlot1 offer
  -> combo-owned fixed runtime entry starts with IgnoreEnergyRespectCharge
  -> request-scoped commit publishes ComboSkillCommitted(target X, FactId F2, ChainId C1)
  -> PartySlot2 definition match และสร้าง offer ถัดไป
  -> test consumes PartySlot2 ขณะ PartySlot1 ยัง recovery
```

ลำดับไฟล์สำหรับ PR/ชุดเปลี่ยนแรก:

1. request-scoped commit callback + immediate event-order tests
2. combo-owned fixed runtime entry + Helper snapshot isolation tests
3. `PassiveEventType` explicit-number hardening PR ตาม Phase 0.5
4. `FactId`/ChainId factory-normalization contract
5. `CharacterStats.partyComboSkill` + pure definitions + `PartyComboExecutionProfile` + evaluator
6. party event router + opportunity controller + pure smoke tests
7. single field-ally executor adapter + concurrency tests
8. docs ของ contracts ที่ implement จริง

เมื่อ vertical slice นี้ผ่าน จึงต่อ HUD และ Phase 4a Status/EnteredBreak publishers ส่วน Final Strike,
Arts Reaction และ Infliction ทำเฉพาะเมื่อมี post-MVP workstream ที่เจ้าของโปรเจกต์อนุมัติ

---

## 22. Open Product Decisions ที่ไม่ block MVP

รายการนี้ต้องไม่ถูกตัดสินแทนแบบเงียบ ๆ ระหว่าง implementation:

- Helper เป็นสมาชิกสนามถาวรหรือ summoned helper?
- ผู้เล่นสลับตัวที่ควบคุมได้หรือไม่?
- Break Finisher เดิมยังอยู่หรือถูกแทนทั้งหมด?
- Combo ของตัวที่กำลังควบคุมสามารถเปิดได้หรือไม่?
- input layout, simultaneous offer UX และ press buffering presentation; correctness ของ MVP ยังต้องให้ actor
  คนละตัว start ได้ระหว่าง recovery ตาม §10.5
- Infliction count semantics
- offer retry หลัง placement fail
- owner-busy timing UX: MVP ปัจจุบันให้ timer เดินต่อระหว่าง normal attack/Guaranteed Interruption
  จึงอาจเหลือเวลาตอบสนองจริงสั้นมาก ต้อง playtest แล้วตัดสินว่าจะคงเดิม, freeze ระหว่าง hard busy
  หรือให้ one-time grace extension พร้อม disabled reason ที่ HUD
- cinematic/world-slow presentation ของ Combo
- Combo trigger จาก commit หรือ hit สำหรับ content แต่ละตัว
- post-MVP จะเพิ่ม Combo skill-tree/loadout variants หรือไม่; MVP ล็อกเป็น fixed identity,
  ไม่อยู่ใน `CharacterStats.skillSlots` และใช้ null snapshot
- post-MVP จะอนุญาต shared `SkillGemDefinition` ข้าม Battle/Helper/Combo หรือไม่; MVP validator reject
- `AllyHelperProcController` ยังคง auto-cast ต่อไป หรือย้ายเข้า opportunity model เดียวกัน
  (ตาม §10.3)
- Arts Reaction จะถูกสร้างเป็น workstream แยกเมื่อไร หรือถอดออกจาก trigger set ถาวร (§3.7)
- Final Strike นิยามจาก `MeleeComboSO` step สุดท้าย หรือจากระบบ basic attack ระดับบนกว่านั้น
  และครอบคลุมทั้ง ranged/melee หรือไม่ (§3.7)
- tag `Infliction` จะถูกนิยามและติดให้ status asset ชุดไหน (§3.7)

หากยังไม่ได้คำตอบ ให้ใช้ vertical-slice defaults ใน Phase 0 โดยไม่ขยาย scope และรักษา architecture
ให้เปลี่ยน policy ภายหลังได้โดยไม่แก้ execution core
