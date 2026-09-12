# แผนส่งต่อ: Player / Ally ใช้ 1 Active + 1 Ultimate

สถานะ: เริ่ม implementation เมื่อ 2026-09-12 แล้ว ระบบรองรับ semantic slot, save migration, run snapshot/lock, validator และย้าย Feno สำเร็จในข้อมูล source ส่วน Aires/Roma ยังรอ mapping ที่ระบุท้ายเอกสาร

ทบทวนกับโค้ดและ serialized assets เมื่อ 2026-09-12: แนวทางใช้ระบบเดิมถูกต้อง แต่ยังไม่พร้อมย้ายข้อมูลทุกตัวละครโดยอัตโนมัติ เพราะ Ultimate บางตัวไม่ผูก asset และรายการ skillSlots มี Passive จริง อ่านส่วนผลตรวจละเอียดท้ายแผนก่อน implementation การตรวจครั้งนี้เป็น static inspection ไม่ใช่ผลทดสอบ Unity Play Mode

## เป้าหมายและข้อตกลงที่ยืนยันแล้ว

- Player และ Ally ที่ลงสนามมีสกิลติดตั้ง 1 Active + 1 Ultimate ต่อหนึ่งตัวละคร
- ตัวละครเดียวกันใช้ชุดเดียวกันเมื่อเปลี่ยนระหว่าง Player กับ Ally
- เลือกผ่าน UI ใน BaseMent เท่านั้น และล็อกตัวเลือกตลอดการลงสนามครั้งนั้น
- รวมสกิลและตัวเลือก Active จากสามช่องเดิมเป็น options ในช่อง Active เดียว เลือกพกหนึ่งตัวเลือก คงต้นไม้อัปเกรดของแต่ละสกิล
- Ultimate มีช่องและรายการตัวเลือกแยก ใช้ระบบร่ายเดิม งานนี้ไม่เพิ่มระบบเกจใหม่
- คงวิธีสั่งใช้ กฎ AI และค่าใช้จ่ายเดิมของ Ally ปรับการอ้างอิงให้ตรงกับสกิลที่ติดตั้ง
- รักษาความคืบหน้า แต้มอัปเกรด และสกิลที่ปลดล็อกในเซฟเก่า ค่าเริ่มต้น Active ใช้ตัวเลือกเดิมของช่อง Active แรก ถ้าใช้ไม่ได้ให้ใช้ค่าเริ่มต้นที่กำหนดไว้
- Ultimate ใช้ค่าเริ่มต้นที่กำหนดให้ตัวละคร หากยังระบุไม่ได้ ให้รวบรวมรายชื่อและตัวเลือกเพื่อให้ผู้ใช้ตัดสินใจก่อนย้ายข้อมูลตัวละครนั้น ห้ามเลือกแทนโดยเดา
- Helper คงระบบ Command / Proc เดิม อยู่นอกขอบเขตการลดช่อง
- ไม่เปลี่ยนจำนวนสกิลของ Enemy / Summon โดยอัตโนมัติ ไม่ปรับสมดุล cooldown, resource cost, damage หรือรูปแบบการต่อสู้โดยพลการ

## สิ่งที่ตรวจพบจริง

พาธด้านล่างอ้างอิงจาก `P:\Game_RB_Project\RB_Project` ต้องตรวจโค้ดล่าสุดอีกครั้งก่อนแก้ เพราะเซสชันอื่นอาจมีการเปลี่ยนแปลง

| ไฟล์ | บทบาท / ข้อค้นพบ |
|---|---|
| `Assets/Scripts/CharacterStats/Scripts/CharacterStats.cs` | มี `skillSlots` และข้อมูลบทบาท Helper พร้อม `helperCommandSlot` / `helperProcSlots` |
| `Assets/Scripts/Player/Skill/CharacterSkillLoadoutSlot.cs` | มี slotId, displayName, defaultOptionIndex และ options รองรับ passive slot ด้วย จึงห้ามนับทุกช่องเป็น Active โดยไม่ตรวจ |
| `Assets/Scripts/Player/Skill/CharacterSkillLoadoutOption.cs` | ตัวเลือกสกิลและความสัมพันธ์กับ upgrade tree ที่ต้องรักษาระหว่างย้าย |
| `Assets/Scripts/Player/Skill/CharacterSkillManager.cs` | มี TrySelectSkillOption, TrySelectLoadoutOption และ RefreshCharacterOwnedLoadout สร้าง Runtime จากข้อมูลและเซฟเดิม |
| `Assets/Scripts/UIScripts/ActiveSkill/ActiveSkillLoadoutSession.cs` | มี CreateLobby / CreateRuntime และ SelectOption บันทึกตาม characterId ผ่าน SaveManager หรือเปลี่ยน Runtime ผ่าน manager |
| `Assets/Scripts/UIScripts/ActiveSkill/ActiveSkillScreenController.cs` | หน้าช่องสกิลและตัวเลือกเดิม ใช้ต่อได้ |
| `Assets/Scripts/UIScripts/ActiveSkill/SkillLoadoutDescriptorFactory.cs` | สร้างรายการช่องจาก skillSlots หรือข้อมูล Helper ใช้ displayName ที่กำหนด ก่อน fallback เป็น SKILL I / II / III |
| `Assets/Scripts/SelectCharactor/UILoadLaval.cs` | OpenActiveSkillTree เปิดหน้าสกิลด้วย BindLobby(selectedCharacter) |
| `Assets/Scripts/Player/Skill/Upgrades/CharacterSkillLoadoutKeys.cs` | Stryker ใช้ raw slotId; Helper มี namespace แยก fallback ID อาจอิง index ต้องระวังการย้าย |
| `Assets/Scripts/Player/Skill/Upgrades/CharacterSkillSelectionStore.cs` | จุดตรวจการอ่านและเขียนตัวเลือกในเซฟ |
| `Assets/Scripts/Player/Skill/Upgrades/ActiveSkillProgressModel.cs` | จุดตรวจ schema และความคืบหน้าต้นไม้อัปเกรดก่อนออกแบบ migration |
| `Assets/Scripts/Player/PartyCommandController.cs` | จุดตรวจ command, CP และการเรียกสกิล ห้ามเหมารวมว่าเป็นเส้นทาง Ultimate |

ข้อควรระวังที่ยืนยันจากโค้ด: `RebuildResolvedCommandSlots()` ใช้ `Mathf.Max(statsSlotCount, serializedSlotCount)` จึงยังเติมช่องจาก `autonomousSlots` บน prefab หากข้อมูล CharacterStats ไม่ครอบคลุม index นั้น ลดรายการบน CharacterStats อย่างเดียวไม่พอ

ยังไม่พบระบบที่ใช้ชื่อ Ultimate โดยตรงจากการค้นสคริปต์ แต่ตรวจ asset เพิ่มแล้วพบ `Test.Aires.Ultimate` ใน `Assets/Scripts/CharacterStats/Asosiation/ChaDef.Aires.asset` ซึ่ง skillAsset ยังเป็น null จึงเป็นเพียงตัวเลือกที่ยังใช้ไม่ได้ ห้ามถือว่า Chain Attack คือ Ultimate ต้องตาม asset / prefab / input / UI จริงให้พบก่อน mapping

## ลำดับดำเนินงาน

### 1. สำรวจและทำตารางย้ายข้อมูล

- อ่าน AGENTS.md ที่ใช้กับไฟล์เป้าหมายและใช้ unity-developer ก่อนลงมือ ตรวจ working tree และรักษางานที่ไม่เกี่ยวข้อง
- ไล่ Player/Ally prefab จริงไปยัง CharacterStats และ manager รวม prefab variants, scene overrides และสกิลที่ยังอยู่บน prefab
- ทำตารางต่อหนึ่งตัวละคร: characterId, asset/prefab, บทบาท, slotId/optionId เดิม, skill asset, upgrade tree, ค่าเริ่มต้น และปลายทางใหม่
- แยก active, passive, helper และสกิลภายนอกตามพฤติกรรมจริง ห้ามรวม passive หรือ helper proc เข้า Active โดยชื่อหรือตำแหน่งช่อง
- ตามจุดเรียกใช้ทั้งหมด: input Player, AI, party command, HUD, skill selection, spawn/respawn และการสลับตัวละคร ตรวจเลข index ที่เขียนตายตัว
- ยืนยัน Ultimate จากข้อมูลจริง ถ้าขาด ให้ส่งรายการที่ต้องตัดสินใจแก่ผู้ใช้ แล้วทำส่วนอิสระต่อได้ ห้ามย้ายตัวละครนั้นด้วยสกิลที่เดา

### 2. ปรับข้อมูลช่องบนระบบเดิม

- ใช้ CharacterSkillLoadoutSlot / Option และ CharacterSkillManager เดิม ไม่สร้างระบบ loadout/save/cast ซ้ำ
- กำหนดช่อง Active และ Ultimate ด้วย stable ID ที่ไม่อิงลำดับ ตั้งชื่อแสดงผลให้ชัดเจน
- หากต้องเพิ่ม metadata ประเภทช่อง ให้เพิ่มเท่าที่จำเป็นและมีค่าเข้ากันได้กับข้อมูลเดิม ห้ามเปลี่ยน default ของ Enemy / Helper / Summon ไปเป็นกฎสองช่อง
- รวมตัวเลือก Active โดยรักษา skillAsset, optionId, displayName และ upgradeTreeOverride ตาม schema ปัจจุบัน ตรวจ ID ซ้ำข้ามช่องก่อนรวม อย่าลบทิ้งด้วยการ deduplicate จาก skill asset อย่างเดียว ปัจจุบัน CharacterSkillLoadoutOption ไม่มี level/supports อย่าเพิ่ม field จากคำอธิบายในเอกสารเก่าที่ไม่ตรงโค้ด
- จัดการ fallback prefab ของตัวละครที่ย้ายแล้ว เพื่อไม่ให้ช่องเก่ากลับมา รักษา fallback ของตัวละครนอกขอบเขต
- ไม่ลบ serialized fields หรือ public API เพียงเพื่อทำให้ inspector เหลือสองช่อง ตรวจผู้ใช้งานและ prefab ก่อนเสมอ

### 3. ย้ายเซฟและข้อมูลอัปเกรด

- อ่าน schema จริงก่อนเขียน migration ใช้ mapping ต่อ characterId และ old slot/option ไป new slot/option รวม tree/node keys หาก schema ใช้
- รักษา unlocked nodes, spent/available points และความคืบหน้าของทุกตัวเลือก ไม่ใช่เฉพาะสกิลที่ติดตั้ง
- กำหนด migration version ให้รันซ้ำได้โดยไม่เพิ่มแต้ม ย้ายซ้ำ หรือลบความคืบหน้าใหม่
- รองรับกรณี ID ซ้ำ เดิมใช้ fallback index และข้อมูลหาย โดยห้ามทิ้งข้อมูลที่จับคู่ไม่ได้อย่างเงียบ ๆ เก็บไว้เพื่อกู้คืนและรายงาน
- รักษาตัวเลือก Active เดิมของช่องแรกเมื่อ mapping ได้ ส่วน Ultimate ใช้ตัวเลือกเดิมที่ยืนยันได้ หรือค่าเริ่มต้นที่กำหนดให้ตัวละคร
- เซฟใหม่สร้างสองช่องตามข้อมูลที่ผ่านการตรวจแล้ว เซฟเดิมที่ยังขาด Ultimate mapping ต้องไม่ถูกทำเครื่องหมายว่าย้ายสำเร็จทั้งตัวละคร

### 4. เชื่อม UI BaseMent และล็อก Runtime

- ใช้ OpenActiveSkillTree → BindLobby → ActiveSkillLoadoutSession เดิม ให้ช่องร่ายแสดง Active / Ultimate และตัวเลือกของแต่ละช่อง คงการเข้าถึง Passive/tree เดิม ไม่ตีความว่าทุก tab ต้องเหลือสอง tab
- คงการดูต้นไม้อัปเกรดเดิม งานนี้ล็อกการเปลี่ยนตัวเลือกสกิล ไม่ได้อนุมัติให้ล็อกระบบอัปเกรดทั้งหมด
- ปิดการเปลี่ยน loadout ในสนามที่ขอบเขตระบบจริงด้วย ไม่ใช่ซ่อนปุ่มเพียงอย่างเดียว แยกการ restore/init ภายในออกจากคำขอเปลี่ยนของผู้เล่น
- ตรวจ SelectOption และ API manager: เมื่อ Runtime ปฏิเสธ ต้องไม่ไหลไปเขียนเซฟเป็น fallback และต้องไม่แสดงว่าสำเร็จ
- ใช้ชุดที่เลือกไว้ตอนเริ่มรอบอย่างสม่ำเสมอหลังเปลี่ยนฉากในรอบ, respawn และสลับ Player/Ally ห้าม refresh เปิดทางเปลี่ยนชุดระหว่างรอบ
- เปลี่ยน HUD/input/AI ที่อ้างช่องเดิมให้ชี้สองช่องใหม่ รักษาต้นทุนและเงื่อนไขการร่ายเดิม ไม่เปิด auto-cast ให้ Ultimate โดยเดา

### 5. ตรวจรับและปรับเอกสาร

- เพิ่มการทดสอบที่ตรวจพฤติกรรมจริงในรูปแบบที่โปรเจกต์ใช้อยู่ เน้น migration, การล็อก Runtime และ fallback slots
- ตรวจ prefab อย่างน้อย Player และ Ally ที่มี layout ต่างกัน ใช้ ctx เป็นศูนย์กลางอ้างอิงตาม AGENTS.md
- อัปเดต `Docs/SYSTEMS/SKILL_SYSTEM.md` และ `Docs/PREFABS_AND_AUTHORING.md` สำหรับกฎใหม่และขั้นตอน authoring
- อัปเดตเอกสาร AI/party/save ที่เฉพาะเจาะจงเมื่อพบว่าพฤติกรรมหรือสัญญาข้อมูลเปลี่ยนจริง ไม่แก้เอกสาร third-party
- อัปเดตสถานะในแผนนี้เมื่อดำเนินการเสร็จ พร้อมรายการที่ยังรอ Ultimate mapping หากมี

## เกณฑ์ตรวจรับ

1. ตัวละครในขอบเขตที่ย้ายสำเร็จมีเพียงสองช่องร่ายที่ติดตั้ง: Active หนึ่งและ Ultimate หนึ่ง ไม่มีช่องร่ายเก่าจาก prefab กลับมา Passive ยังคงได้และไม่นับเป็นหนึ่งในสองช่อง ห้าม assert จำนวน skillSlots/CommandSlots รวมเป็นสองหากยังเก็บ Passive ในรายการเดียวกัน
2. BaseMent เลือกได้หนึ่ง option ต่อช่อง บันทึกแล้วออกเข้าเกมใหม่และเริ่มสนามได้ตรงตัวเลือก
3. ตัวละครเดียวกันเปลี่ยนบทบาท Player/Ally แล้วยังใช้ชุดเดียวกัน และสถานะ cooldown ไม่แชร์ข้าม runtime actor โดยไม่ตั้งใจ
4. คำขอเปลี่ยนตัวเลือกในสนามถูกปฏิเสธทั้ง UI/API ไม่เปลี่ยนเซฟ ไม่รีเซ็ต cooldown และไม่กระทบการ restore ตอนเริ่มรอบ
5. Active ทุกตัวเลือกยังอ้างต้นไม้อัปเกรดเดิม ความคืบหน้าและแต้มจากเซฟเก่าไม่หายหรือเพิ่ม และ migration ซ้ำไม่เปลี่ยนผล
6. AI/input/party command ใช้ช่องใหม่ได้ ไม่มีการเรียกช่อง 3 เดิม ไม่มีการเปลี่ยนค่าใช้จ่ายหรือเพิ่มกฎเกจใหม่
7. Helper Command/Proc, passive และตัวละครนอกขอบเขตไม่เสียพฤติกรรมเดิม
8. ข้อมูลที่ขาด Ultimate mapping ถูกระบุชัดเจนและไม่ถูกเลือกให้เอง

## การตรวจ C# ตามกฎโปรเจกต์

รันจาก PowerShell เมื่อมีการแก้ C#:

```powershell
powershell -ExecutionPolicy Bypass -File 'P:\Game_RB_Project\RB_Project\Assets\Scripts\CheckAssemblyBuild.ps1'
```

ห้าม `dotnet build` กับ Unity csproj โดยตรง ห้ามใช้ build command ที่มี `Assets\Scripts\_buildbin` หรือ `Assets\Scripts\_buildobj` และห้ามแก้ generated csproj/sln เพื่อบังคับรวมไฟล์ใหม่ ตรวจ runtime ใน Unity เพิ่มเพราะ compilation อย่างเดียวไม่ยืนยัน prefab/UI/AI/save flow

## ผลตรวจละเอียดกับโค้ดจริง และข้อกำหนดเพิ่มเติม

อ้างอิงเลขบรรทัด ณ วันที่ตรวจ อาจเปลี่ยนหลังแก้ไฟล์

### A. ข้อมูลจริงไม่ใช่สาม Active ที่พร้อมใช้งานทุกตัว

| แหล่งข้อมูล | สิ่งที่พบ | ผลต่อแผน |
|---|---|---|
| `Assets/Scripts/CharacterStats/Asosiation/ChaDef.Aires.asset:53` | slotId เป็นสตริง `0`, `1`, `2`; ช่อง `0` มี Aires_Skill_1 และ option ว่าง; ช่อง `1` มีชื่อ Test.Aires.Ultimate แต่ไม่มี skillAsset; ช่อง `2` ผูก skill asset | เก็บ raw ID เดิมใน mapping ไม่แปลง `0` เป็น `slot:0`; Ultimate นี้ยังไม่ผ่าน IsConfigured |
| `Assets/Scripts/CharacterStats/Asosiation/ChaDef.Feno.asset:53` | ช่อง `1` ว่าง ช่อง `2` มี feno.skill.minigunterret และมีช่อง passive.feno.bag | Active เริ่มต้นต้องรองรับช่องแรกว่าง; คง Passive แยกจากสองช่องร่าย |
| `Assets/Character/{Roger,Dorothy,Noemi,Abbygail,Milano}` | CharacterStats ที่ตรวจในโฟลเดอร์เหล่านี้มี skillSlots ว่าง; Abbygail/Milano เป็น Helper | ห้ามสร้าง mapping สาม Active ให้ทุกตัวจากสมมติฐาน ต้องตรวจ asset ใน Asosiation และ CharacterDatabase ด้วย |
| `Assets/Prefab/Player/Player.prefab:1066` | autonomousSlots มีสามรายการ แต่ skillAsset ทุกช่องว่าง | มีช่องส่วนเกินจริง แต่ยังไม่ใช่หลักฐานว่าสามช่องนี้ร่ายได้ |
| `Assets/Prefab/Player/Ally_Stryker.prefab:1314` | autonomousSlots ว่าง และมี chainAttackSkill ผูกกับ Test_skill_MilanoChainAttack_01 | อย่ารายงานว่าตัว prefab นี้มีสาม Active อยู่แล้ว และห้ามย้าย chainAttackSkill ไป Ultimate โดยเดา |

ข้อมูลนี้เป็นหลักฐานจากไฟล์บนดิสก์ ไม่ครอบคลุม unsaved scene overrides ใน Unity Editor ต้องตรวจ live scene ก่อนสรุป loadout ที่ผู้ใช้เห็นขณะเล่น

### B. ข้อจำกัดสองช่องต้องเป็นกติกาของช่องร่าย

`CharacterSkillLoadoutSlot.IsPassiveSlot` และ `CharacterSkillManager.AppendConfiguredPassiveDefinitions()` ใช้รายการช่องเดียวกันกับ Active จริง ส่วน `TryBeginCast` ปฏิเสธ Passive อยู่แล้ว ดังนั้นวิธีที่เปลี่ยนน้อยคือคงข้อมูล Passive และกำหนดชนิด/การ resolve ช่องร่าย Active/Ultimate อย่างชัดเจน ให้ input และ AI resolve ช่องร่ายโดยความหมาย ไม่อาศัยว่า index 0/1 เป็นช่องที่ถูกต้องเสมอ หากเลือกเรียง Active/Ultimate ก่อน Passive ต้องตรวจและย้ายผู้ใช้งาน index ทุกแห่ง

ไม่จำเป็นต้องแยกระบบ Passive ใหม่ทั้งระบบในงานนี้ แต่ validator ต้องแยกจำนวนช่องร่ายออกจากจำนวนช่องทั้งหมด ตรวจว่า Active/Ultimate ไม่มี Passive ปะปน และไม่ตัด tab Passive ที่เปิด tree ได้อยู่แล้ว

### C. Migration ต้องเสร็จก่อนการอ่านที่มีผลข้างเคียง

- `ActiveSkillProgressModel.GetTreeProgress():265` ค้นด้วย slotId + optionId; รายการซ้ำจะถูกลบและคืน paidCost, treeId เปลี่ยนจะล้าง unlockedNodes และคืนแต้ม, node ที่หาไม่พบก็ถูกลบและคืนแต้ม การ BuildSnapshot หรือดูสถานะ tree จึงไม่ใช่ read-only ล้วน
- Migration ต้อง remap selection และ tree progress ให้เสร็จก่อน `ActiveSkillLoadoutSession` สร้าง model/เปิด tree และก่อน `CharacterActiveSkillProgress.LoadState():153` / manager สร้าง upgrade snapshot
- Schema จริงคือ `CharacterProgressData` ใน `Assets/Scripts/System/SaveSystem/SavaData.cs:44`: selectedSkillOptions, skillPoints, skillProgressInitialized, activeSkillTrees ขณะที่ tree entry มี slotId, optionId, treeId และ nodeId/paidCost ไม่ต้องเพิ่ม treeVersion โดยไม่มีเหตุผล
- `SaveDataMigration.LoadAndMigrateCharacterProgressSaveFile():66` มี envelope migration อยู่แล้ว แต่ไม่มี character asset parameter และ `SaveSystem.LoadCharacterProgressFile():452` เขียนไฟล์ทันทีเมื่อ migrated เป็น true ต้องออกแบบการเข้าถึง mapping ให้พร้อม ไม่อาศัยลำดับ asset โหลดโดยบังเอิญ
- envelope version อย่างเดียวระบุไม่ได้ว่าตัวละครใดยังขาด Ultimate mapping ให้มีสถานะย้ายต่อ character หรือกลไกเทียบเท่า และต้องคัดลอก field ใหม่นี้ผ่าน `CharacterProgressData.DeepClone()` เพราะ SaveManager clone ทั้งตอนอ่านและเขียน
- เส้นทางจริงของ SaveManager คือไฟล์ `Assets/Scripts/System/SaveSystem/SaveMenager.cs:457` และ `:463` ไม่ใช่ SaveManager.cs; repository เขียนทั้ง progress entry จึงต้องป้องกัน snapshot เก่าเขียนทับข้อมูลหลัง migration
- `CharacterActiveSkillProgress.PersistState():177` merge เฉพาะ skillPoints, skillProgressInitialized และ activeSkillTrees ลง snapshot ล่าสุดอยู่แล้ว ต้องรักษาพฤติกรรมนี้ และตรวจการ refresh model ที่ยังถือ old tree keys
- ห้ามสร้างสอง entry ที่ปลายทาง slotId/optionId เดียวกัน ถ้า option เดิมคนละช่องมีความคืบหน้าต่างกัน ให้คง identity แยกด้วย mapping ที่ไม่ชนก่อน ห้าม union node แบบเดาสุ่ม โดยเฉพาะ mutually exclusive nodes
- ทดสอบทั้ง JSON เก่า/ใหม่ การ clone การ save/reload และ migration รอบสอง ตรวจ paidCost และ available points โดยตรง ไม่ใช่ตรวจเพียงจำนวน node

### D. จุดล็อก Runtime ที่ต้องครอบคลุม

1. `CharacterSkillManager.TrySelectSkillOption` ทั้ง overload index และ string (`:323`, `:345`)
2. `TrySelectLoadoutOption():427` ซึ่ง fallback ไป Helper ต้องปฏิเสธตามชนิดช่องที่ถูกล็อก ไม่ปิด Helper ทั้งหมด
3. `AssignSkillToSlot():997` และ `ClearSlot():982` เป็น public mutation อีกทาง ต้องระบุการอนุญาต initialization/internal ให้ชัด แม้การค้นครั้งนี้ยังไม่พบ caller ภายนอกของสอง API นี้
4. `ActiveSkillLoadoutSession.SelectOption():127` คืน true และเขียนข้อมูลผ่าน fallback เมื่อ manager เลือกไม่สำเร็จ: ถ้ามี runtime progress จะเปลี่ยน model ในหน่วยความจำ แต่ SaveLobbyState ไม่เขียนทันที; ถ้าไม่มี runtime progress อาจไปเขียน repository ได้ ต้องแยก failure ออกจาก Lobby route ให้ชัด ไม่กล่าวเหมารวมว่าทุก failure เขียนดิสก์ทันที
5. `CreateLobby()` ไม่มีการตรวจว่าอยู่ BaseMent จริงและ `IsRuntime` อิง context เท่านั้น จึงใช้ IsRuntime เป็นแหล่งตัดสินสิทธิ์เพียงอย่างเดียวไม่ได้ ต้องผูกกับสถานะการเล่นที่เจ้าของ flow ยืนยันได้
6. `RebuildResolvedCommandSlots():1597` อ่านเซฟใหม่ผ่าน LoadSavedSkillSelections; `HandleActiveSkillTreeChanged():1816` สามารถ rebuild ได้ จึงต้อง resolve ตัวเลือกจากชุดที่ล็อกไว้ในรอบ ไม่ใช่เซฟที่อ่านใหม่ทุกครั้ง

`MapRunSession` ปัจจุบันเก็บ Graph/CurrentNode/CurrentEntry/IsTransitioning ยังไม่มี loadout snapshot ส่วน `MapRunController.StartRun():170` และจุด ClearRun เป็นจุดตรวจ lifecycle เพิ่ม แต่ HasActiveRoom ไม่เท่ากับสิทธิ์เปลี่ยนสกิล: ระหว่างโหลดหรือเข้า room ไม่สำเร็จอาจไม่มี room ทั้งที่ยังไม่ได้กลับ BaseMent ส่วน `BasementManager.cs` ประกาศ class ชื่อ BasementContext และเป็น reference hub ไม่ใช่ service จัดการ run

ข้อเสนอ implementation: เพิ่มข้อมูลตัวเลือกที่ล็อกต่อ characterId ในเจ้าของ lifecycle ของรอบที่ตรวจยืนยันแล้ว เก็บเพียง ID ของตัวเลือก ไม่แชร์ SkillInstance ข้าม actor และไม่ freeze upgrade tree progress โดยพลการ กำหนดการเริ่ม/จบ/เริ่มรอบใหม่และกรณีเข้า scene ทดสอบตรงให้ชัดก่อน wiring ไม่มีเหตุผลให้สร้างระบบ save รอบค้างใหม่หากเกมยังไม่รองรับ

### E. เส้นทางใช้สกิลและการทดสอบที่ต้องระบุชื่อไฟล์

| จุดเรียก | สิ่งที่ต้องตรวจ/แก้ |
|---|---|
| `Assets/Scripts/Player/PlayerInputHandler.cs:211` | OnSkillSlot1/2/3 ส่ง index 0/1/2 ตรง ห้ามปล่อยปุ่มสามไปโดน Passive หรือช่องอื่นหลังจัดใหม่ รักษา callback API เดิมได้ แต่ route เฉพาะช่องที่มีความหมายถูกต้อง |
| `Assets/Input/Inputmaneger.inputactions` | ตรวจ binding และ PlayerInput serialized callbacks รวม HUD ก่อนเปลี่ยนปุ่ม ไม่เปลี่ยน public callbacks โดยไม่ตรวจ scene/prefab |
| `Assets/Scripts/AI/Ally TEST Scripts/TryCastSkillFromSlot.cs:7` | มี serialized SlotIndex และเรียก manager จริง ชื่อโฟลเดอร์ TEST ไม่ใช่เหตุผลให้ข้าม ต้องตาม Behavior Designer assets ที่ใช้ task นี้ด้วย |
| `Assets/Scripts/Player/PartyCommandController.cs:880` และ `:919` | ตรวจทั้ง block reason และ execute; เลือก PlayerCommandSkill ก่อน slot fallback; PlayerCommandSkill ใน manager หมายถึง Helper manual command ไม่ใช่ Ultimate |
| `Assets/Scripts/UIScripts/ActiveSkill/ActiveSkillChargePresenter.cs:26` | HUD อิง index ของ CommandSlots ต้องให้ชื่อ/icon/charge/readiness ตรง slot mapping เดียวกัน |
| `Assets/Scripts/Player/Skill/CharacterSkillManager.cs` | มี external skill, chain attack, helper และ party combo paths แยกอยู่ ห้ามใส่ global cast whitelist สอง asset แล้วทำ scripted/combo/helper ใช้ไม่ได้ คุมเฉพาะเส้นทาง loadout ที่อยู่ในขอบเขต |

เรื่อง cooldown: `CreateRuntimeSkill():1172` ผูก shared charge ผ่าน manager และ `SkillInstance` ใช้ pool เดียวกันสำหรับ definition เดียวใน actor เดียวอยู่แล้ว การสร้าง SkillInstance ใหม่จึงไม่ได้แปลว่า reset cooldown เสมอ แผนต้องรักษาการแชร์นี้ และทดสอบว่าเปลี่ยน selection ที่ถูกปฏิเสธ/refresh ไม่ reset pool; ไม่เพิ่มข้อกำหนดให้ cooldown ต่อเนื่องข้าม actor ที่ถูกทำลายแล้วสร้างใหม่โดยไม่ได้ตรวจพฤติกรรมเดิม

### F. ชุดทดสอบเพิ่มจากเกณฑ์ทั่วไป

- Feno: Passive ยังทำงาน/เปิด tree ได้ หลังจัดสองช่องร่าย และ Active เริ่มต้นได้แม้ช่องเดิมแรกว่าง
- Aires: Test.Aires.Ultimate ที่ไม่มี skillAsset ต้องถูกแจ้งว่า incomplete ไม่ผ่าน validator เพราะมีชื่ออย่างเดียว
- Player prefab: สาม fallback slots ว่างไม่กลายเป็นช่องร่ายที่สามหลัง migration; Ally_Stryker ไม่ได้รับ skill จากชื่อ chain attack บน rig โดยอัตโนมัติ
- Runtime: ปฏิเสธทั้งสอง TrySelect overload, Assign/Clear และ Session fallback ตรวจ memory + disk + selected IDs หลัง refresh/tree change
- Migration: duplicate destination keys, old raw ID `0` เทียบกับ fallback `slot:0`, optionId ว่างที่ resolve จาก SkillDefinitionId/asset name, treeId เดิม, paidCost เดิม, missing Ultimate และ DeepClone version marker
- Helper regression: ใช้ `Assets/Scripts/Editor/ActiveSkill/HelperSkillLoadoutSmokeTests.cs` และ `HelperRuntimeExecutionSmokeTests.cs` เป็นฐาน ตรวจไม่ถูก global lock
- Save regression: ต่อจาก `Assets/Scripts/Editor/ActiveSkill/CharacterProgressMigrationTests.cs` และยืนยัน legacy activeSkillPoints migration เดิมยังทำงาน
- Player input / Ally task / PartyCommand readiness+execute / HUD ใช้ mapping เดียวกัน และสล็อตที่สามเดิมไม่ร่ายสกิลที่ไม่ได้ติดตั้ง

## ข้อความเริ่มงานสำหรับเซสชันถัดไป

> อ่าน AGENTS.md และ `P:\Game_RB_Project\RB_Project\Docs\PLAYER_ALLY_SKILL_LOADOUT_HANDOFF.md` แล้วดำเนินการปรับระบบเดิมตามแผนที่ผู้ใช้ยืนยันแล้ว ให้ Player/Ally ใช้ 1 Active + 1 Ultimate เลือกเฉพาะ BaseMent รักษาเซฟและ tree progress รวมถึงคง Helper เดิม เริ่มจากตรวจข้อมูลจริงและตาราง migration ไม่ต้องถามยืนยันข้อตกลงเดิมซ้ำ หากระบุ Ultimate ของตัวละครไม่ได้ให้ถามเฉพาะรายชื่อที่ต้องตัดสินใจและทำงานส่วนที่ไม่ติดขัดต่อ ห้ามเดา mapping รักษางานอื่นใน working tree ตรวจ build ตามสคริปต์ที่กำหนด และรายงานผลทดสอบพร้อมเอกสารที่อัปเดต

## สถานะ implementation ล่าสุด

- Feno: ย้ายเป็น `active` = `Feno.Skill_MinigunTerret` และ `ultimate` =
  `Feno.Skill_Ulatimate`; Passive Forgotten Bullet Bag ยังคงเป็นช่องแยก
- Save migration ของ Feno ย้าย key เดิม `2` ไป `active`, เติม default
  `ultimate`, รักษา tree/node/paidCost และมี per-character version marker
- runtime ของรอบ snapshot selection ต่อ `characterId`; API เปลี่ยนช่องของ
  Stryker ถูกปฏิเสธระหว่าง run โดย Helper Command/Proc ไม่ถูกล็อก
- input/AI/party command/HUD ที่ใช้ index ผ่าน manager resolver เดียวกัน:
  input 0 = Active, 1 = Ultimate, 2 ถูกปฏิเสธสำหรับ loadout ที่ migrate แล้ว
- เพิ่ม validator และ EditMode tests สำหรับ migration กับ run snapshot
- รอผู้ใช้ยืนยันสอง mapping โดยไม่เดา:
  - `ID.Aires`: Ultimate จะใช้ asset ใด (`Aires_Active` เป็น candidate ที่พบ แต่ชื่อไม่ยืนยันบทบาท)
  - `ID.Roma`: Active จะใช้ asset ใด (พบเฉพาะ `Skill.Def.Roma_Ultimate` ที่ยืนยันเป็น Ultimate)
