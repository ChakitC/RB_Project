# แผนส่งต่อ: Player Targeting กลางและลูกศรเหนือหัว

สถานะ: Implemented 2026-09-11 — เอกสารนี้คงไว้เป็น acceptance/migration record; runtime contract
ปัจจุบันอยู่ใน `Docs/SYSTEMS/AI_AND_TARGETING.md`, `CAMERA.md`, `SKILL_SYSTEM.md`,
`PARTY_COMBO.md`, `Docs/ARCHITECTURE/CHARACTER_CONTEXT.md` และ
`Docs/PREFABS_AND_AUTHORING.md`

## คำสั่งสำหรับเซสชันผู้รับ

Implement แผนนี้ให้ครบใน P:\Game_RB_Project\RB_Project โดยอ่าน AGENTS.md และสกิล unity-developer ก่อนเริ่ม ตรวจสถานะงานเดิมและคำสั่งในโฟลเดอร์ที่จะแก้ก่อน ห้าม revert งานอื่น ทำระบบเลือกเป้าผู้เล่นกลาง แสดงลูกศร และแทนที่ระบบเลือกเป้าผู้เล่นเดิมทั้งหมดในขอบเขตนี้ ไม่เปิด legacy fallback กลับมาเมื่อหาเป้าไม่ได้ ตรวจ prefab/scene references ก่อนลบ serialized fields หรือ component และทดสอบพร้อมอัปเดตเอกสารตามรายการท้ายแผน

## 1. ข้อสรุปพฤติกรรม

- Target เป็น soft lock อัตโนมัติใกล้ Aim บนจอ ขยับ Aim แล้วเปลี่ยนตัวได้ ไม่เพิ่มปุ่ม hard lock หรือบังคับกล้องตามตัวในงานนี้
- มี CurrentTarget เดียวต่อผู้เล่น ชนิด CharacteContext; UI และคำสั่งที่อาศัย Aim อ่านค่าจากแหล่งเดียวกัน
- เลือกศัตรู active ที่มี HealthSystem และยังมีชีวิต กรองด้วย TargetIdentity/CharacterFactionUtility ตามกติกาฝ่ายของโปรเจกต์ ตัดตัวเองและพวกเดียวกันออก
- ตรวจระยะโลกจากผู้เล่น ตรวจว่าอยู่หน้ากล้องและภายในจอ ตรวจรัศมีรอบ reticle และ line of sight
- คะแนนหลักคือระยะบนจอจาก reticle ไปยัง TargetInfo.AimPoint; ใช้ระยะโลกจากผู้เล่นตัดสินกรณีเท่ากันเท่านั้น ไม่บวกระยะโลกเข้าคะแนนจนเป้าที่ห่าง reticle กว่าชนะได้
- วัดระยะจอโดยคำนึงถึง aspect ratio เช่น screen distance / screen height; อย่าใช้ viewport x/y เป็นวงกลมโดยไม่แก้อัตราส่วน
- Aim ปัจจุบันอยู่กลางจอ ใช้กล้องเดียวกับ ThirdPersonAimController; หากไม่มี camera/owner ให้ล้างเป้า
- ค่าเริ่มต้นสำหรับทดลอง: ระยะ 30 เมตร, acquire radius 0.15 ของความสูงจอ, release radius 0.18, switch advantage 0.015 ของความสูงจอ, switch delay 0.10 วินาที ทั้งหมดปรับ Inspector ได้และต้อง playtest
- ตอนเลือกครั้งแรกเลือกตัวใกล้ reticle ที่สุด; ขณะมีเป้าให้คงเป้าจนตัวใหม่ดีกว่าเกณฑ์ต่อเนื่องตาม switch delay ใช้ unscaled time สำหรับความรู้สึก UI ระหว่าง world slow แต่ไม่เลือกเป้าใหม่ระหว่าง pause/menu ที่หยุด gameplay
- เป้าตาย/despawn/disable/ออกจอ/เกิน release radius/เกินระยะ/ถูกบัง ให้ invalidate และเลือกใหม่ ไม่ค้างเป้าที่ใช้ไม่ได้
- การยิงธรรมดายังคงใช้ AimPoint/ResolveShotDirection เดิม การมี Target ไม่เปลี่ยนวิถีกระสุนโดยอัตโนมัติ
- คำสั่งที่ใช้เป้าปัจจุบันต้องตรวจเงื่อนไขตัวเอง หากใช้ไม่ได้ให้ fail พร้อมเหตุผลและไม่เสีย resource ก่อนยืนยันสำเร็จ ห้ามเลือกตัวอื่นแทนโดยเงียบ
- Snapshot target เมื่อรับ request ที่ต้องใช้เป้า ก่อนรอ animation/queue; ตอนยืนยันเริ่มทำงานตรวจเป้า snapshot เดิมซ้ำตามกติกา command ไม่ capture CurrentTarget ใหม่ UI เปลี่ยนได้โดยไม่เปลี่ยนเป้าที่ล็อกไว้กลางท่า และ execution ยังตรวจการตาย/ถูกทำลายตาม lifecycle ของตน

## 2. โครงสร้างใหม่

### PlayerTargetingController.cs

วางใน Assets/Scripts/ThirdPerson เป็นเจ้าของการเลือกและการเปลี่ยนเป้า อ้าง PlayerContext เป็น owner และอ่าน peer references ผ่าน context

API ที่ตั้งใจเพิ่ม:

```csharp
public CharacteContext CurrentTarget { get; }
public bool TryGetTarget(out CharacteContext target);
public event Action<CharacteContext, CharacteContext> TargetChanged; // previous, current
```

TryGetTarget เป็น read/validation ของ target ที่ commit แล้ว ห้าม rescan หรือเปลี่ยนเป้าตามจำนวนครั้งที่ consumer/UI เรียก Event เกิดเมื่อ identity เปลี่ยนจริงและส่ง null เมื่อ clear Invalid target ต้องคืน false ทันที ส่วนการ commit/event ให้ controller จัดการโดยไม่ยิง event แบบ reentrant จาก getter

CurrentTarget ต้องเป็น validated read ตาม validity พื้นฐานเดียวกับ TryGetTarget (owner/input gate, active/alive, life token) และคืน null เมื่อใช้ไม่ได้ ห้ามเผย raw backing field ที่อาจค้างให้ consumer อ่านข้าม TryGetTarget ตรวจ LOS/ระยะเฉพาะเป้าอีกครั้ง ณ เริ่มคำสั่งได้ แต่ไม่ต้อง raycast ทุกครั้งที่ UI อ่าน property Event ของ selector บอกการเปลี่ยนเป้า ไม่ใช่ readiness event; ChainReady/reservation/resource ที่เปลี่ยนบนตัวเดิมยังต้องอัปเดต readiness ผ่าน events/poll เดิมของระบบคำสั่ง

โค้ดจริงรับคำสั่งผ่าน InputAction callbacks ขณะที่ ThirdPersonAimController ใช้ Update และ GameplayCameraController/CinemachineBrain ใช้ LateUpdate จึงห้ามอาศัย DefaultExecutionOrder อย่างเดียวเพื่ออ้างว่า selector ทำงานก่อน input ให้ใช้สัญญา: commit selection หนึ่งครั้งหลังกล้องอัปเดตเสร็จ แล้ววาง UI จาก selection เดียวกัน; input ใช้ selection ที่ commit จากภาพล่าสุดที่ผู้เล่นเห็น พร้อมตรวจ validity ซ้ำ ห้าม rescan ไปอีกตัว ณ เวลากด ก่อนมี committed selection ให้คืนไม่มีเป้า เซสชัน implement ต้องตรวจ hook/ลำดับ post-camera ของ Cinemachine เวอร์ชันในโปรเจกต์และทดสอบจริง ไม่เดาเพียงว่า LateUpdate ของ UI จะตามหลัง Brain เสมอ

เพิ่ม reference Targeting ใน PlayerContext.ResolveReferences() โดยเรียก base ตามเดิม ไม่เพิ่ม field ผู้เล่นนี้ลง CharacteContext และไม่สร้าง singleton ใหม่

### Character registry

สำรวจ registry ที่มีอยู่จริงก่อนเพิ่ม หากไม่มี registry ตัวละคร ให้สร้าง CharacterContextRegistry.cs สำหรับ active CharacteContext และเชื่อม registration/unregistration กับ lifecycle ที่เหมาะสม ตรวจ OnEnable/OnDisable ของทุก subtype ก่อนเปลี่ยน base class เพื่อไม่ให้ Unity message ถูกบัง

รองรับ spawn, pooling, disable, destroy, scene change และ Enter Play Mode ที่ปิด domain reload ต้องไม่มีรายการค้างหรือซ้ำ ไม่ใช้ FindObjectsByType หรือ physics overlap หา actor ทุกเฟรม ไม่ย้ายระบบ aura/heal อื่นมาใช้ registry ในงานนี้เว้นแต่จำเป็นต่อความถูกต้อง

### PlayerTargetIndicatorView.cs

- uGUI Screen Space Overlay ให้สอดคล้องกับ ThirdPersonReticleView ลูกศรหนึ่ง instance ต่อ local player ปิด raycastTarget
- รับ context ตามวงจร UI ของโปรเจกต์ subscribe/unsubscribe TargetChanged และรองรับเปลี่ยนผู้เล่น/scene
- ย้ายตำแหน่งหลัง post-camera selection commit ใน frame เดียวกัน ตามกล้องและ CanvasScaler ใช้ TargetInfo สำหรับจุดเล็ง แต่ใช้จุดเหนือหัวแยกต่างหากสำหรับ UI ไม่ให้ LateUpdate อิสระของ UI รันก่อนจุด commit แล้วแสดงเป้าเก่า
- ต่อจาก CharacterTargetHeightUtility.ResolveOverheadPoint ได้ แต่ไม่เรียก hierarchy scan/สร้าง array ทุกเฟรม Cache references ที่ใช้วัด แล้วคำนวณ bounds/position สด; หากเพิ่ม authored marker anchor ให้ใช้ local Transform และ fallback ที่มีอยู่
- ซ่อนเมื่อ target ไม่ valid, อยู่นอกจอ/หลังกล้อง หรือ gameplay UI ถูกซ่อน รวมถึง clear เมื่อ owner disable

## 3. แทนที่ระบบเดิมจริง

จุดต่อไปนี้พบจากการตรวจโค้ดวันที่ 2026-09-11 ต้องอ่าน callers และ assets อีกครั้งก่อนแก้:

| จุดเดิม | งานเปลี่ยน |
| --- | --- |
| ThirdPersonTargetingUtility.FacePlayerTowardSoftTarget | เอาการค้นด้วย OverlapCapsule และเลือกคะแนนออก อ่าน CurrentTarget แล้วหันตัว; ไม่มีเป้าใช้ planar camera forward ตามเดิม |
| PlayerInputHandler และ PrefabHitboxSkillPayloadDef | ตรวจเส้นทางการเรียกหันตัวให้ใช้ target กลาง ไม่สร้าง selector อีกชุด |
| ChainAttackTargetingUtility.TryResolveTargetFromAim | แทนการ scan/ranking ด้วย target กลาง เอา off-screen fallback และการเลือก ChainReady ตัวอื่นเหนือเป้า Aim ออก |
| ChainAttackCoordinator และ PartyCommandController | เส้นทาง manual/Aim ใช้ snapshot เป้ากลาง ตรวจ ChainReady, reservation, range และเงื่อนไข sequence บนตัวนั้น |
| AllyHelperManager.TryResolveChainAttackTarget และ callers | ย้าย OverlapSphereNonAlloc รอบ playerContext.aimTarget และการจัดอันดับระยะโลกไปอ่าน committed target กลาง ตรวจเส้นทาง CanStart/TryStart ของ helper chain ทั้งหมด ลบ _chainTargetBuffer/_chainTargetIds และ helper คัด candidate เฉพาะเมื่อไม่มีผู้ใช้เหลือ ตรวจ HelperChainAttackSequenceDef settings/authoring ควบคู่ |
| InterruptionCommandController.TryFindTarget | เอา OverlapCapsuleNonAlloc/loop เลือกตัวอื่นออก สร้าง InterruptionTargetContext จากเป้ากลาง ตรวจ block window, reservation, health และ knockback ของเป้านั้น รักษา result/diagnostics ที่ caller ใช้ |
| ChainReadyPromptView และ PartyCommandController.TryGetChainReadyPromptBlockReason | ปรับ prompt ให้ทราบ owner context เทียบกับ CurrentTarget ปุ่ม [F]/สถานะพร้อมกดแสดงเฉพาะตัวที่เลือกและผ่านเงื่อนไขจริง ตัวอื่นยังแสดงสถานะ/timer ChainReady ได้แต่ห้ามสื่อว่ากดแล้วคำสั่งจะลงตัวนั้น |
| CharacterSkillManager.TryBeginCast, SkillCastRequest และ PrefabHitboxSkillPayloadDef | เชื่อม snapshot สำหรับการหันตามเป้าของ skill ฝั่งผู้เล่นที่เดิมค้นเป้าเอง ให้ request เก็บ snapshot ก่อนรอ animation และ payload อ่าน snapshot นั้น ห้ามเติม enemy target เป็น PrimaryTarget ให้ทุก skill |
| ThirdPersonTargetingUtility.TryGetReticleScore/HasLineOfSight | รวมการคำนวณที่ยังใช้ไว้เป็น helper ของ selector กลาง ปรับ score ให้ตรงข้อ 1 และลด allocation ใน LOS; ไม่ให้ consumer ใช้ helper เพื่อเลือกเป้าเอง |

ค้นเพิ่มเติมทั้ง Assets/Scripts ด้วยชื่อ helper และการค้นใกล้ aimTarget/CameraRay ก่อนประกาศจบ เส้นทาง manual/Aim ที่พบเพิ่มต้องย้ายด้วย พร้อมบันทึก migration checklist ในผลส่งมอบ

การลบหมายถึงลบ logic ค้นหา/จัดอันดับเป้าผู้เล่นเก่าและ dead code จริง ไม่เพิ่ม switch ใช้ old/new ควบคู่ ไม่ลบไฟล์ทั้งไฟล์หากยังมี anchor/alive/explicit target helpers ถูกใช้

รักษา explicit target จาก event/proc/execution ที่มีความหมายของตัวเอง และ AI sensor สำหรับการต่อสู้อัตโนมัติ ทั้งสองอย่างไม่ใช่การเลือกเป้าจาก Aim ผู้เล่น ห้าม redirect explicit target กลับไปหา CurrentTarget

จัดประเภทตามแหล่งข้อมูลเป้าจริง ไม่ใช่ชื่อ Proc/Helper: AllyHelperProcController เรียก AllyHelperManager.TryStartChainAttackHelperProc ซึ่งไม่มี explicit target parameter และปัจจุบันเรียก TryResolveChainAttackTarget จาก Aim เส้นทางนี้ต้องย้ายไป selector กลางด้วย ส่วน TryStartChainAttackHelperToTarget และ event ที่ส่ง actor มาจริงคง explicit target เดิมไว้ คำว่า "คง proc" ในแผนหมายถึงคงกติกา proc/resource/trigger และ target ที่ระบุมาแล้ว ไม่ใช่ยกเว้น Aim selector ที่อยู่ในเมธอดชื่อ Proc

คง AITargetSensor, AIAimTargetDriver, AITargetInfo, IAITargetable, TargetIdentity, ChainAttackPoint, SkillTargetHandle และ physics hit detection ตามหน้าที่เดิม ไม่ลบเพียงเพราะชื่อมี Target หากพบตัวเลือกเป้าซ้ำในนั้นให้แก้เฉพาะเส้นทาง Aim ผู้เล่น

Serialized search radius/mask/preferChainReady ที่เลิกใช้ต้องตรวจ scene, prefab และ asset references แล้ว migrate ค่าที่จำเป็นไป controller กลางก่อนลบ ห้ามเปลี่ยนความหมาย enum ของ asset เดิมโดยเงียบ และไม่เปลี่ยนชื่อ public API/class โดยไม่จำเป็น ให้ wrapper เดิมอ่าน controller ใหม่ได้แต่ห้ามมี legacy selector ข้างใน

### Migration และสัญญา consumer

1. แยก acquisition settings ออกจาก action eligibility: radius รอบ reticle อยู่ที่ selector แต่ระยะ melee, layer/ประเภทเป้าที่ skill รับ และข้อจำกัดการใช้คำสั่งยังอยู่ที่ consumer ห้ามย้ายค่าทั้งหมดมารวมจนท่าประชิดใช้ระยะ 30 เมตร ค่าเดิมของ PlayerInputHandler คือ searchDistance 4 เมตร และ PrefabHitboxSkillPayloadDef ส่ง 6 เมตร; ทั้งคู่เคยใช้ capsule จึงต้องนิยามระยะ action จากผู้เล่นให้ชัดและ playtest ไม่อ้างว่าเทียบ geometry เดิมแบบหนึ่งต่อหนึ่ง เมื่อ target กลางไม่ผ่านเงื่อนไขหันตัวของ melee ให้ใช้ planar camera forward และยังโจมตีได้ ห้ามค้นตัวที่สอง
2. รัศมีโลก aimSearchRadius/chainReadySearchRadius ของ ChainAttackSequenceDef ไม่ใช่รัศมีจอ แปลงตรง ๆ ไม่ได้ จัด migration report แยกค่าที่เลิกใช้ ค่าที่เป็น action constraint และค่า controller ใหม่ ระยะ 30 เมตร/รัศมี 0.15 เป็นค่าทดลอง ไม่ใช่ค่าทดแทนอัตโนมัติของทุก asset ระบุชัดว่า manual/Aim acquisition ใหม่บังคับ visible+LOS แม้ asset เดิมไม่ requireAimLineOfSight; explicit/proc path รักษานโยบายเดิม
3. ChainReady input ต้องรักษาสัญญา NoReadyTarget/Consumed ที่ PlayerInputHandler ใช้แยกระหว่าง chain และ Interactor: เป้ากลางไม่ ChainReady ให้ NoReadyTarget ตามเดิม; เมื่อรับเป็นคำสั่ง chain แล้วแต่ resource/actor ไม่พร้อม ให้ Consumed พร้อมเหตุผลตาม flow เดิม ห้ามทำให้กดครั้งเดียวเริ่ม chain และ interact พร้อมกัน ทดสอบการยกเลิก/ปล่อยปุ่มด้วย
4. สัญญา ChainTargetSource เดิมใช้ ณ การรับคำสั่งใหม่เท่านั้น: ExplicitTargetOnly ใช้เฉพาะ explicit; ExplicitTargetOrAimTarget ใช้ explicit ตามเดิมและใช้ committed player target เมื่อไม่มี explicit; AimTargetOnly ใช้ committed player target อย่าเปลี่ยน enum values หรือ precedence ของ entry request ถ้ามี explicit แต่ invalid ให้รักษาพฤติกรรม fail เดิม ไม่เพิ่ม fallback ใหม่ เมื่อ request รับและล็อก snapshot แล้ว การ resume หลัง intro/queue หรือส่งต่อ step ต้องใช้ internal continuation path ที่รับ snapshot เดิมโดยไม่อ่าน targetSource เพื่อเลือกใหม่
5. Snapshot ต้องบันทึกอายุการ spawn ของ actor ด้วย: Transform/CharacteContext เดิมอาจถูก disable แล้ว pooled กลับมาในขณะ execution ยังอยู่ ใช้ lifecycle generation/token ผ่าน handle เดิมหากรองรับหรือเพิ่มกลไกที่จำเป็น ตรวจ token ก่อนใช้เป้าซ้ำ การมี IsAlive เป็น true อีกครั้งไม่อนุญาตให้ execution เก่าโจมตีชีวิตใหม่
6. Skill ที่รับ explicit/event target อยู่แล้วต้องคงเส้นทางนั้น ไม่บังคับ skill ทุกชนิดให้ต้องมี player target ใช้ snapshot ณ จุดรับคำสั่ง/ยืนยัน transaction ของ flow เดิม และส่ง handle ไปถึง delayed payload แทนการอ่าน CurrentTarget ใหม่ตอนปล่อยท่า; ตรวจ PrefabHitboxSkillPayloadDef ซึ่งหันตัวตอน payload execute โดยเฉพาะ

### รายละเอียด selector ที่ต้องปิดก่อน implement

- หาก TargetInfo/AimPoint ไม่มี ให้ fallback เป็นจุดกึ่งกลางลำตัวจาก reference/bounds ที่ cache ผ่าน context และสุดท้าย root + authored fallback offset ไม่ตัด actor ที่ไม่มี TargetInfo/collider ออกเงียบ ๆ แยก marker overhead จาก scoring point
- ถ้าคะแนนและระยะเท่ากันให้รักษา current target; ตอนยังไม่มี current ให้ใช้ stable per-spawn key เป็น tie-break ไม่อิงลำดับ HashSet เพิ่ม/ลบ และ reset switch timer ทุกครั้งที่ challenger เปลี่ยน/หลุด validity
- LOS เริ่มจาก gameplay camera ไปยัง scoring point ระบุ obstacle layer จริงจาก project และ ignore owner geometry โดยไม่ให้วง trigger/ลูกศร/อุปกรณ์ที่ถือกลายเป็นกำแพง หากต้อง ignore friendly geometry ให้ใช้กติกาที่ตรวจจาก Aim ปัจจุบัน ไม่เดา ignore ทุก actor
- LOS ต้องลอง candidate ตามคะแนนจนพบตัวที่มองเห็น ไม่หยุดที่ตัวคะแนนดีที่สุดแต่ถูกบัง หากใช้ NonAlloc ต้องรองรับ buffer เต็มโดยไม่สรุปว่าโล่งจาก hit ที่ไม่ครบ ขยาย/retry หรือ fail closed โดยไม่เลือกผ่านกำแพง
- Pause/gameplay input gate ต้องอิงระบบ pause/menu จริง ไม่ตรวจ Time.timeScale อย่างเดียว ระหว่าง pause ห้ามเริ่มคำสั่งและซ่อน marker ตาม UI policy; death/despawn ต้อง invalid แม้ pause และเมื่อ resume ให้ commit จากกล้องใหม่ก่อนรับคำสั่งเป้า
- Registry ต้องรองรับปิดทั้ง domain reload และ scene reload: reset static อย่างเดียวอาจล้างรายการโดยไม่มี OnEnable ลงทะเบียนใหม่ ให้มี bootstrap/reconcile หนึ่งครั้งต่อ play session ได้ ห้ามกลายเป็น full scene scan ต่อเฟรม EnemyContext.OnDisable มี RestoreWorldSlowValues อยู่แล้ว ต้องรักษา callback นี้เมื่อเชื่อม registry

### Skill contract และขอบเขตการย้าย

- CharacterSkillManager.TryBeginCast(SkillSlot) ปัจจุบันสร้าง SkillCastRequest โดยไม่ส่ง primaryTarget ขณะที่ SkillCastContext มี PrimaryTarget อยู่แล้ว จึงต้องเชื่อม request จริงด้วย ไม่ใช่เพียงแก้ payload ให้ไปอ่าน field ที่ยังเป็น None การหันตัวของ PrefabHitboxSkillPayloadDef ใช้ snapshot จาก player targeting ณ รับคำสั่ง และเมื่อไม่มี/เสียเป้าใช้ fallback direction ที่ snapshot ตอนเดียวกัน ห้ามอ่าน CurrentTarget/Aim ใหม่ตอน delayed payload execute หากทำให้เปลี่ยนทิศกลางคำสั่ง
- แยก optional facing snapshot ออกจาก explicit effect recipient: PrimaryTarget เดิมมีความหมายเป็นผู้รับผลของ cast โดย TargetedDeliverySkillPayloadDef ระบุ recipient ฝ่ายเดียวกัน ห้ามใช้ enemy selector เติมช่องนี้แบบ global default ให้ทุก request หรือใช้การมี target เป็นข้อบังคับของ heal/self/ground/projectile skill การส่ง target ของสกิลโจมตีที่ต้องการ recipient ต้องทำเฉพาะเส้นทางที่ประกาศรับ enemy target; งานนี้ไม่เพิ่มระบบ configuration สำหรับ target mode ทุกชนิดหรือเปลี่ยนระบบเลือกเพื่อน
- รักษา SkillTargetHandle.WasAssigned, lastKnownDeliveryPoint และความต่างระหว่าง TryResolveLiveContext (ยังมี actor) กับ TryResolveEffectTarget (รับผลได้) อย่าเพิ่ม IsAlive gate ลง live-resolution ทั้งระบบ เพราะ TargetedDeliveryRuntime ใช้มันกำหนดการเดินทาง/การจบภาพ เป้าที่เสียไประหว่างท่ารวมถึง lifecycle token ไม่ตรงต้องยังมี WasAssigned=true และใช้นโยบาย target-lost เดิม ไม่เปลี่ยนเป็น None จน refund resource ผิด
- อายุ actor ต้องเป็น token แยกจาก selection revision: การเปลี่ยน Aim, ถูกบัง, หลุดระยะ หรือถอดออกจาก candidate list ไม่ใช่การ spawn ใหม่ อย่าใช้ registry membership version แทน life token ให้ตรวจวงจร pooling/respawn จริงและระบุจุดเริ่ม/สิ้นอายุ; หากระบบ pooling จบอายุด้วย disable ให้เชื่อมตรงนั้นโดยไม่เหมารวมการซ่อน visual เป็น despawn เมื่อ spawn ใหม่ใน object เดิม committed selection เก่าต้องอ่านเป็น invalid จน commit ใหม่ด้วย ไม่ป้องกันเฉพาะ execution handle
- Player controller ต้อง invalidate เป้าเมื่อ owner ตาย/disable/เปลี่ยน context/scene หรือ camera session เปลี่ยน และไม่คืน target ระหว่าง gameplay input gate ปิด การ clear ภายใน lifecycle ทำได้ แต่ getter ไม่ยิง event หรือ rescan หาก actor ถูก pool กลับมาก่อนรอบ commit ให้ clear/publish ตาม life identity แม้ CharacteContext reference เท่าเดิม UI ต้อง refresh binding ของชีวิตใหม่
- ChainAttackTestTarget เป็น AITargetInfo + IDamageable และมีระบบ health ของตัวเอง ไม่ใช่ CharacteContext เป้าทดสอบนี้จึงไม่เข้า selector ใหม่ที่เลือก character-only โดยตั้งใจ รักษา explicit test/proc support เดิม และใช้ enemy prefab ที่มี context สำหรับทดสอบ manual Aim แทน อย่าใส่ EnemyContext/HealthSystem ทับ dummy อัตโนมัติจนมี health สองเจ้าของ ระบุ scene/dummy ใดที่เลิกใช้ manual Aim ใน migration report
- UI/action validation ใช้ target-specific eligibility เดียวกัน แต่ readiness query ต้องไม่มีการ reserve/spend/เริ่ม animation การแสดง prompt ไม่มีสิทธิ์เปลี่ยน selection หรือสร้าง transaction; กรณีหายไปจาก LOS/ระยะระหว่างภาพล่าสุดกับ input ให้ reject เป้าเดิมและไม่ค้นตัวอื่น การตรวจ LOS/ระยะซ้ำเป็น validation ไม่ใช่ selection
- Post-camera selection ต้องมีผู้เรียกเพียงหนึ่งครั้งต่อ gameplay render frame ของกล้องที่ใช้งาน ระบุจุด bootstrap controller และ UI ให้ครบ (prefab binding หรือ runtime creation ตาม pattern เดิม) ResolveReferences อย่างเดียวไม่สร้าง component ที่ไม่มีอยู่ ห้าม bootstrap ซ้ำจนมีสอง selector/สอง marker

### จุดเชื่อมที่ต้องตรวจครบ

- AllyHelperManager มีทั้ง player-Aim helper chain selector และ autonomous helper skill aiming ในไฟล์เดียวกัน ย้าย TryResolveChainAttackTarget ตามตาราง แต่คง TryResolveHelperSkillAimPoint ที่อ่าน AITargetSensor/LastSeenPosition ตามหน้าที่ AI อย่าแทนทั้งไฟล์ด้วย player target เส้นทาง explicit ของ helper chain ต้องคง precedence เดิม
- InterruptionCommandController.TryExecuteInterruptionCommand ยังมี early gate ว่า playerContext.aimTarget ต้องมี และ log ใช้ targetSearchRadius/mask ก่อนเข้า TryFindTarget ต้องย้าย gate/diagnostics เหล่านี้ไปแยก MissingConfiguration (ไม่มี controller/owner) กับ NoValidTarget (มีระบบแต่ไม่มีเป้า) ไม่ทิ้ง dependency ของ scan เก่าไว้หลังลบ loop
- Skill ที่ใช้ PrefabHitboxSkillPayloadDef ไม่ได้เข้าผ่าน TryBeginCast(SkillSlot) เท่านั้น ตรวจ TryBeginEntryCast และทุก SkillCastRequest constructor call ใน manual/party/combo/explicit routes ให้การส่ง facing snapshot ครบ เส้นทางที่มี explicit locked target ใช้เป้านั้นเพื่อหันตัวก่อน player-Aim fallback และห้ามเติม snapshot ใหม่จากผู้เล่นใน delayed phase ห้ามแก้เฉพาะ slot path แล้วปล่อย entry path หันตาม Target คนละตัว
- รูปแบบ facing snapshot ต้องแยกสามสถานะ: ไม่ได้ขอ assist, ขอ assist แต่ไม่มี eligible target (ใช้ captured planar fallback), มี locked target (life-aware handle) ค่า default ของ request เก่าหมายถึงไม่ได้ขอ assist เพื่อไม่เปิด behavior ใหม่ใน AI/explicit route โดยไม่ตั้งใจ ทำเป็นข้อมูลขนาดเล็กตาม convention เดิม ไม่สร้าง targeting framework ใหม่
- สำหรับ facing assist ของ skill ให้ตรวจ action range/type ของเป้า ณ รับ request ก่อน snapshot ถ้าไม่ผ่านให้ snapshot fallback direction จากเวลานั้น หากเป้าผ่านแต่ตาย/หลุด action range ก่อน execute ให้ใช้ fallback เดิมตามพฤติกรรม melee ไม่ค้นเป้าใหม่ การ snapshot ตัว actor ไม่ได้ freeze ตำแหน่งโลก: เมื่อยัง valid ให้หันหาตำแหน่งปัจจุบันของ actor เดิมได้
- หลักฐานการลบ selector เดิมต้องครอบคลุมชื่อ methods/fields และ callers ในสองตระกูล sequence: ChainAttackSequenceDef และ HelperChainAttackSequenceDef รวม Inspector/editor tools ที่ใช้ fields เหล่านั้น ห้ามใช้เพียงผลค้น TryGetReticleScore เป็นหลักฐานว่า migration ครบ

### ส่งต่อ target ตลอดอายุ execution

- ChainAttackProcController ถือ _pendingIntroTargetTransform ระหว่าง ChainReady intro และ StartChainAfterIntro เรียก coordinator.TryStartSequence อีกครั้ง ต้องเปลี่ยนไปส่ง snapshot เดิมเข้า continuation path ตามข้อ 3.4 แม้ sequence เป็น AimTargetOnly ห้ามไหลกลับ acquisition ที่จะอ่าน CurrentTarget ขณะ cinematic/input gate ปิดหรือหลังผู้เล่นเปลี่ยน Aim เก็บ life token ตั้งแต่กดและตรวจซ้ำก่อนเริ่ม sequence หลัง intro
- แยกการรับคำสั่งใหม่กับ committed continuation: pause/cinematic/camera gate ปิดการเลือกเป้าและคำสั่งใหม่ แต่ไม่ทำให้ committed chain intro ของระบบเองถูกยกเลิกเพียงเพราะ Targeting.TryGetTarget คืน false หลังกล้องเปลี่ยน continuation ตรวจ snapshot, owner/target lifecycle, reservation และ cancellation policy เดิมแทน
- ChainReady intro จ่าย command points และ stamp cooldown ก่อน sequence เริ่ม หาก target หาย/intro ถูกยกเลิกก่อน sequence เริ่ม ให้ผ่าน AbortPendingChainReady/refund path เดิมหนึ่งครั้ง ไม่ยกนโยบาย skill target-lost ที่ไม่ refund มาใช้กับ transaction นี้ คงการยกเลิก meter/reservation แบบมีเจ้าของและอย่าแก้ meter ชีวิตใหม่ที่ถูก pool กลับมา
- ChainAttackCoordinator.ActiveChainRuntime, AllyHelperManager.PendingChainAttackSequence และขั้นตอนของ FieldAllySequenceRunner/FieldAllyTransitionController/ChainSkillCastBridge มีการถือและส่ง Transform/anchor อยู่ ตรวจทุกขอบเขตส่งต่อของ execution ที่รับ target กลาง และส่ง life-aware snapshot เดิมต่อไปด้วย ไม่สร้าง snapshot ใหม่จาก Transform เมื่อถึง step ถัดไป เพราะเป้าอาจถูก pool กลับมาแล้วและจะกลายเป็นการรับรองชีวิตใหม่โดยผิดพลาด
- ก่อน warp, face, เริ่ม step, ส่ง PrimaryTarget ให้ skill หรือใช้ cached anchor ให้ validate life token เดิม ไม่ใช่เพียง IsAlive/Transform != null หากไม่ valid ให้ใช้ cancel/skip/cleanup policy ของ sequence เดิมและคืน reservation/protection/aim override ตาม flow เดิม ไม่ถือ anchor ของชีวิตใหม่ต่อเพื่อให้จบท่า
- ถ้าต้องคง public signature ที่รับ Transform ให้คงเป็น entry wrapper สำหรับคำสั่งใหม่ และเพิ่ม internal path ที่รับ snapshot สำหรับการส่งต่อ execution เดิม ไม่แก้ public APIs ทั้งระบบโดยไม่จำเป็น ไม่บังคับ explicit non-character target ให้กลายเป็น CharacteContext
- ทดสอบ pooling แบบ disable-enable ระหว่างสอง step โดยไม่มี Update คั่น รวมถึงระหว่างรอ warp event และก่อนส่ง skill request: actor ชีวิตใหม่ต้องไม่ถูก warp เข้าหา/รับคำสั่งจาก execution เก่า แม้ Unity instance id และ anchor Transform เท่าเดิม

- Interruption มี delayed execution ของตัวเองนอก Chain: ส่ง snapshot ผ่าน InterruptionTargetContext ไป PlayerInterruptionController และ AllyInterruptionController ด้วย ตรวจ life token ก่อน timeline effect/knockback/complete block และใช้ rollback ของ reservation เดิมเมื่อ token ไม่ตรง อย่าถือเพียง Health/Transform ที่กลับมา alive ได้ภายหลัง เปลี่ยน executor จาก player เป็น ally ตาม fallback placement เดิมได้โดยใช้ target snapshot เดิม นี่ไม่ใช่การเปลี่ยนเป้าซึ่งแผนห้าม

## 4. กติกา context และ prefab

- target เป็น CharacteContext ตลอด common logic อ่าน HealthSystem/TargetInfo/KnockbackMotor ผ่าน context
- ถ้าต้อง resolve PreCastBlockController หรือ StaggerMeter ซ้ำ ให้ตรวจเจ้าของ reference และเพิ่มใน context ที่เหมาะสม โดย shared reference อยู่ใน base และ subtype-only อยู่ใน subtype override
- ห้ามให้ parent-only lookup เป็น gameplay gate ต้องรองรับ component บน root และ child ที่ต่างกันใน prefab
- ตั้ง controller และ UI ด้วย workflow prefab/scene ของโปรเจกต์ ใช้ Unity CLI/MCP เมื่อแก้ Unity assets ไม่แก้ generated project files เพื่อให้ compiler พบคลาสใหม่
- C# class ใหม่แยกไฟล์ของตน ตรวจ .meta และ serialized bindings ไม่เกิด missing script

## 5. ลำดับ implementation

1. ตรวจ AGENTS, git status, consumer callers, lifecycle ของ context, UI binding, prefab/scene และ tests ที่ครอบคลุม ChainReady/Interruption/explicit target
2. ทำ registry, controller, PlayerContext reference และกติกา selection ให้ทดสอบได้โดยแยก pure score/filter เท่าที่จำเป็น
3. ทำลูกศรและผูก prefab/runtime setup ตาม convention ที่พบ
4. ย้ายทุก manual/Aim consumer ตามตาราง พร้อม snapshot และ validation ก่อนใช้ resource
5. ลบ scan/ranking เดิม, dead buffers/helpers และ migrate serialized settings ที่เลิกใช้ อัปเดต tests ที่ตั้งใจเปลี่ยนพฤติกรรม
6. อัปเดตเอกสาร รัน validation และ playtest ตรวจ checklist ด้านล่าง

## 6. Acceptance และ validation

- ตัวใกล้ reticle ชนะตัวใกล้ผู้เล่น; ทำงานตรงกันบน 16:9 และ ultrawide; ไม่เลือกหลังกล้อง/นอกจอ/ผ่านกำแพง
- สองตัวใกล้กันไม่กระพริบสลับรัว; ตาย/disable/despawn/swap scene แล้วไม่มี stale target หรือ orphan arrow
- Pool disable-enable และ play mode/domain reload configuration ไม่ทำให้ registry หายหรือซ้ำ
- ลูกศรอยู่เหนือหัวของ prefab ความสูงต่างกัน และปรับตำแหน่งถูกกับ resolution/CanvasScaler
- health/target info บน root และ nested child ให้ผลเหมือนกัน; ไม่ต้องมี collider เพื่อค้นพบ actor
- Manual chain และ interruption ใช้ตัวที่ลูกศรชี้ เมื่อไม่มี ChainReady/open window ให้ fail โดยไม่ย้ายไปตัวอื่นและไม่เสีย resource
- Explicit/proc target และ AI sensor ยังเลือก/ใช้เป้าตามเหตุการณ์ของตนได้
- เปลี่ยน Aim ระหว่าง execution ไม่เปลี่ยน snapshot; target ถูกทำลายระหว่าง execution ไม่ throw
- การยิงธรรมดา, muzzle obstruction, melee fallback direction และ world slow/pause ยังทำงานตามกติกา
- UI และ action snapshot ใช้ committed selection ของภาพล่าสุดเดียวกัน; หมุนกล้องและกดใน input callback ไม่ rescan ไปตัวที่ยังไม่เคยแสดง การอ่าน getter ซ้ำไม่เปลี่ยนผลหรือยิง event ซ้อน
- ChainReady หลายตัวพร้อมกัน: มีเพียงตัวที่เลือกแสดงว่า [F] ใช้กับตน, resource เปลี่ยนแล้ว prompt ตรงกับการกดจริง และ chain/interact ไม่เกิดพร้อมกัน
- เป้า 30 เมตรไม่ทำให้ melee ที่จำกัดระยะหันไปใช้ target ไกล; fallback โจมตีตามกล้องยังทำงาน
- Pool เป้ากลับมาใน object เดิมระหว่าง delayed execution ต้องทำให้ snapshot ชีวิตเก่า invalid; LOS buffer เต็มไม่เลือกผ่านกำแพง; registry ยังครบเมื่อปิดทั้ง scene/domain reload
- SkillSlot -> SkillCastRequest -> delayed PrefabHitbox payload ได้ facing snapshot จริง; เปลี่ยน Aim ระหว่างรอ animation ไม่เปลี่ยน target/fallback direction ของคำสั่งนั้น
- Friendly TargetedDelivery, self/ground skill และ explicit non-character dummy ยังใช้ recipient เดิม ไม่รับ enemy target โดยอัตโนมัติ และไม่มี target ไม่ทำให้ skill ที่ไม่จำเป็นต้องมี target ถูกปิดใช้งาน
- TargetedDelivery สูญเสีย target หลังเริ่มท่ายังรักษา WasAssigned/target-lost presentation และ resource policy เดิม; actor ที่ยังมีอยู่แต่ตายไม่ถูกสับสนกับ actor ที่ถูกทำลาย
- Owner ตาย/disable และ target pool กลับมาภายในเฟรมเดียวไม่ทำให้ API คืน selection อายุเก่า; มี selector/marker เพียงหนึ่งชุดหลัง reload/เปลี่ยนผู้เล่น
- Helper chain แบบ manual/Aim ใช้เป้าลูกศร ทั้ง CanStart และ TryStart; helper skill แบบ autonomous ยังใช้ sensor ของตน และ explicit helper chain ไม่ถูกแทนเป้าจากผู้เล่น
- Slot/entry/party/combo ที่ใช้ delayed PrefabHitbox ผ่านการทดสอบ snapshot ตามที่มาของคำสั่ง; ไม่มี controller กับไม่มี candidate ให้ diagnostics ต่างกัน และไม่เหลือ early gate ที่บังคับ aimTarget เพียงเพื่อเลือก actor
- Helper proc ที่ไม่มี explicit recipient ใช้ target กลาง ส่วน proc ที่ส่ง explicit recipient คงตัวเดิม; snapshot life token ไม่ถูกสร้างใหม่ระหว่าง chain step/warp/skill handoff และ cleanup หลัง target-lost ไม่ค้าง reservation, protection หรือ aim override
- AimTargetOnly + ChainReady intro: หลังจ่ายแต้มและกล้องเข้าคัตซีน เปลี่ยน Aim/ไม่มี CurrentTarget แล้วยังเริ่มบน snapshot เดิมได้ หาก target หาย/ถูก pool ระหว่าง intro ให้ abort และคืนแต้ม/cooldown ตามนโยบายเดิมเพียงครั้งเดียว ไม่แตะ meter ชีวิตใหม่
- Player/Ally Interruption ที่รอ timeline event ไม่กระทำต่อชีวิตใหม่ของ target หลัง pooling; fallback เปลี่ยนผู้ลงมือได้แต่ไม่เปลี่ยน target และไม่ค้าง reservation หลัง rollback
- ค้น reference ยืนยันไม่มี legacy player aim scan/ranking เหลือ และตรวจ Profiler ว่า steady-state selection/UI ไม่สร้าง GC allocation จาก scene scan, LINQ, bounds lookup หรือ RaycastAll ทุกเฟรม

รัน meaningful EditMode/PlayMode tests สำหรับ selection, lifecycle, integration และ manual playtest ตัวแทน prefab เท่าที่สภาพแวดล้อมรองรับ C# build ใช้คำสั่งนี้เท่านั้น:

```powershell
powershell -ExecutionPolicy Bypass -File 'P:\Game_RB_Project\RB_Project\Assets\Scripts\CheckAssemblyBuild.ps1'
```

ห้าม dotnet build Unity .csproj โดยตรง และห้ามสร้าง artifacts ใต้ Assets ระบุสิ่งที่ไม่ได้รันทดสอบและเหตุผลตามจริง

## 7. เอกสารและผลส่งมอบ

อัปเดต Docs/SYSTEMS/AI_AND_TARGETING.md เป็นหลัก พร้อม Docs/SYSTEMS/CAMERA.md, Docs/SYSTEMS/SKILL_SYSTEM.md, Docs/SYSTEMS/PARTY_COMBO.md และ Docs/ARCHITECTURE/CHARACTER_CONTEXT.md เฉพาะส่วนที่พฤติกรรม/API เปลี่ยน อัปเดต Docs/PREFABS_AND_AUTHORING.md สำหรับ controller, marker และ migration settings

สรุปไฟล์ที่แก้ selector เดิมที่ถูกเอาออก กติกา ChainReady/Interruption ใหม่ prefab ที่ตั้งค่าแล้ว ผล build/tests/playtest และข้อจำกัดที่ยังตรวจไม่ได้ งานเสร็จเมื่อทุก manual/Aim consumer ใช้ target กลางและลูกศรใช้งานได้จริง ไม่จบที่สร้าง API อย่างเดียว

## 8. ผล audit ตัวเลือกเป้าเดิมจาก source และ serialized assets

ตรวจเมื่อ 2026-09-11: ค้น runtime C# ทั้ง Assets/Scripts ด้วยพฤติกรรม physics overlap/cast, scene/tag discovery, screen projection, Aim references, nearest/best scoring, targeting method names และตาม callers ของผลที่เกี่ยวข้อง ตรวจ C# นอก Scripts เพิ่มด้วยรูปแบบ Aim/Target และ physics/angle/projection; ผลนอก Scripts ที่พบเป็น shader/occlusion, formation, ragdoll, editor/plugin utility ไม่พบ player combat Aim selector เพิ่มจากการค้นนี้ ตรวจ serialized field names และ GUID ของ owner scripts ใน .prefab/.unity/.asset ด้วย การตรวจนี้เป็น static audit ไม่ใช่การยืนยัน runtime graph/DLL/plugin code ที่ไม่มี source

### ตัวเลือกศัตรูจาก Aim ที่ต้องแทนที่: 4 จุด / 5 เส้นทางค้น

| Source method | พฤติกรรมเดิม | Callers ที่ยืนยัน |
| --- | --- | --- |
| ThirdPerson/ThirdPersonTargetingUtility.cs:79 FacePlayerTowardSoftTarget | OverlapCapsule -> reticle score -> หันตัว | PlayerInputHandler.OnMelee และ PrefabHitboxSkillPayloadDef.Execute |
| AI/ChainAttack/ChainAttackTargetingUtility.cs:168 TryResolveTargetFromAim | OverlapCapsule + OverlapSphere สำรองสำหรับ ChainReady -> readiness/reticle score | TryResolveLockedTarget -> ChainAttackCoordinator.CanStartSequence/TryStartSequence และ PartyCommandController.TryExecuteChainReadyChainAttack |
| Player/InterruptionCommandController.cs:225 TryFindTarget | OverlapCapsuleNonAlloc -> block-window filter -> reticle score | TryExecuteInterruptionCommand <- PlayerInputHandler |
| AI/AllyHelperManager.cs:2351 TryResolveChainAttackTarget | OverlapSphereNonAlloc รอบ world aimTarget -> ระยะใกล้ AimPoint | HasChainAttackTarget, TryStartChainAttackHelper, TryStartChainAttackHelperProc <- PartyCommandController และ AllyHelperProcController |

ทั้ง 4 จุดอยู่ใน migration plan แล้ว ไม่พบตัวเลือกศัตรูจาก Aim เพิ่มจากการค้นรอบนี้ จำนวน 5 เส้นทางนับ capsule และ ChainReady sphere ใน utility เป็นคนละ sweep หมายเลขบรรทัดเป็นหลักฐานก่อน implementation และอาจเปลี่ยนหลังแก้

### พบและตั้งใจคงไว้ตามหน้าที่

- Interactable/Interractor.cs.FindBestFocus: เลือก IInteractable จาก Aim ray และระยะเอื้อม ไม่ใช่ combat target; อย่าลบเพียงเพราะมันเลือก focus จาก Aim เช่นกัน
- AI/Helper Proc/AllyHelperProcController.cs.TrySelectLowestHealthTarget: เลือกสมาชิกทีมตามสัดส่วน HP และระยะ ไม่ใช่ enemy Aim selector ต่างจาก helper chain proc ที่ต้องย้าย
- PartyCombo/PartyComboOpportunityController.cs.ResolveTarget และ Passives/PassiveController.cs.ResolveTargetObject: เลือก Self/EventActor/EventTarget ที่มากับ event ไม่ scan หา enemy ใกล้ Aim
- AI/Ai Taget And Sensor/AITargetSensor.cs และ AIAimTargetDriver: autonomous AI sensing/aim override; คง helper skill sensor path ใน AllyHelperManager.TryResolveHelperSkillAimPoint
- ThirdPersonAimController และ Projectile/Modules/LobToAimTargetModule: คำนวณจุดเล็ง/วิถีกระสุน module หลังอ่าน AimTransform หรือชื่อ Aim Target และรองรับ lockTargetOnSpawn; ไม่จัดอันดับ actor ห้าม redirect ไป CurrentTarget หรือเปลี่ยน trajectory ในงานนี้
- ThirdPersonTargetingUtility.TryGetReticleScore/HasLineOfSight เป็น helper ไม่ใช่ selector เพิ่ม; ChainAttackTargetingUtility.TryResolveExplicitTarget/TryResolveTargetAnchor และ ChainSkillCastBridge เป็น resolve/handoff ของ actor ที่ระบุแล้ว
- HealAreaSkillPayloadDef/TauntSkillRuntime และ area-damage/hitbox/projectile overlaps: ค้นผู้รับผลตามพื้นที่/ตรวจ physical hits; camera occlusion, cover, movement separation, placement/teleport sweeps และ SpecialShootPoint UI มีหน้าที่ของตน คงไว้ในงานนี้

### Serialized authoring ที่พบ

- Assets/Prefab/Player/Player.prefab มี script GUID ของ PlayerInputHandler, InterruptionCommandController, AllyHelperManager และ ChainAttackCoordinator และมีค่า melee search ที่ต้อง migrate
- Assets/Scripts/AI/ChainAttack/Example.ChainAttack.Sequence.Main.asset มี legacy sequence search fields
- Assets/Scripts/AI/Helper Proc/HelperChainAttackSequence.asset มี legacy helper sequence search fields

ผล field-name search นี้ไม่แทนการตรวจ inheritance ของ prefab variants และ default values ที่ไม่ serialized ทุก field ก่อนลบ field ให้ตรวจ meta GUID/callers/editor authoring อีกครั้งบน working tree ตอน implement และเปิด representative scenes ใน Unity เพื่อยืนยันการ bind จริง
