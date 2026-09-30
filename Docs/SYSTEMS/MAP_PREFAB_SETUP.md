# วิธี setup Map Room Prefab

คู่มือนี้อ้างอิงระบบ Map ที่อยู่ในโปรเจกต์ ณ วันที่ 25 กันยายน 2026 สำหรับทำ prefab ห้องใหม่ เช่น ห้อง Sewer แล้วนำเข้าระบบสุ่มแผนที่

ระบบใช้ **1 prefab ต่อ 1 ห้อง** แต่ละ node จะสร้างห้องเมื่อเข้าไปครั้งแรก และเก็บ instance ไว้จนจบ run เมื่อเปลี่ยนห้อง ระบบปิดห้องเดิมแล้วเปิดห้องปลายทางที่ `roomSpawnAnchor` เดียวกัน จึงไม่ต้องนำ prefab ทุกห้องมาต่อกันใน scene

ลำดับการเชื่อม asset คือ:

```text
Prefab ห้องที่ตกแต่งและ bake NavMesh แล้ว
    -> RoomDefinitionSO.Room Prefab
    -> MapContentPoolSO.Room Definitions
    -> MapRunConfigSO.Content Pool
    -> MapRunController / ด่านที่เลือกจาก Basement
```

## 1. เลือก prefab ฐานให้ตรงรูปทรงประตู

ฐานอยู่ที่ `Assets/Prefab/MAP/Base_Map/` เลือกตามจำนวนและตำแหน่งประตูจริงของห้อง:

| รูปแบบ | ตัวอย่าง prefab ภายใน Base_Map | Exit Mask | Max Exit Count |
| --- | --- | --- | ---: |
| ห้องเริ่มต้น | `Start/RoomPrefab.Base.Start.Up.prefab` | Up | 1 |
| ทางตัน | `DeadEnd/RoomPrefab.Base.DeadEnd.Up.prefab` | Up | 1 |
| ทางตรง | `Straight/RoomPrefab.Base.Straight.UpDown.prefab` | Up, Down | 2 |
| ทางเลี้ยว | `Turn/RoomPrefab.Base.Turn.UpRight.prefab` | Up, Right | 2 |
| สามแยก | `T/RoomPrefab.Base.T.UpRightDown.prefab` | Up, Right, Down | 3 |
| สี่แยก | `Cross/RoomPrefab.Base.Cross.UpRightDownLeft.prefab` | Up, Right, Down, Left | 4 |

1. คลิกขวา prefab ฐานใน Project แล้วเลือก **Create > Prefab Variant**
2. ตั้งชื่อให้ระบุธีม ชนิดห้อง และรูปทรง เช่น `RoomPrefab.Sewer.Combat.StraightUpDown`
3. ย้าย Variant ไปโฟลเดอร์ห้องที่ใช้งานจริง เช่น `Assets/Prefab/MAP/Sewer/PrefabMapaGameSetting/`
4. เปิด Variant ใน Prefab Mode แล้วตกแต่งห้องนั้น

Start base เป็น Variant ของ `DeadEnd.Up` อีกชั้นหนึ่ง ให้สร้างห้อง Start จาก Start base และคงประตูเดียวไว้ ระบบหมุนห้องให้เข้ากับกราฟได้เอง

**Base_Map เป็นโครงสำหรับ authoring และยังไม่มี Navigation/NavMesh ที่ bake แล้ว** ต้องทำห้องสำเร็จรูปก่อนนำไปผูกกับ Room Definition อย่าใช้ base เปล่าเป็นห้องพร้อมเล่น

## 2. ใส่โมเดล พื้น และ collider

วางโมเดลห้องและของตกแต่งใต้ `Visual` ของ Variant รักษา root ให้ scale `(1, 1, 1)` และ rotation `(0, 0, 0)` เป็นค่าเริ่มต้นที่ตรวจทิศได้ง่าย หากต้องปรับขนาดโมเดล ให้ปรับ child ภายใน `Visual` แล้วตรวจ collider และจุดเกิดตามขนาดใหม่

ตัวอย่างโครงสร้างสำหรับห้องทางตรง โดยชื่อ root และกลุ่มของตกแต่งเป็นตัวอย่าง ส่วนชื่อ socket ด้านล่างอิงฐานที่มีอยู่:

```text
RoomPrefab.Sewer.Combat.StraightUpDown
    [RoomController]
    [NavMeshSurface]                 <- เพิ่มบน root ของ Variant ในคู่มือนี้
    Visual
        SewerModel
        Floor / Walls / Props       <- มี collider ตามการเดินและชนจริง
    PlayerSpawnPoint
    Exit.North                      <- RoomExitInteractable.Direction = Up
        EntranceSpawnPoint
    Exit.South                      <- RoomExitInteractable.Direction = Down
        EntranceSpawnPoint
    EnemySpawnPoint.North
    EnemySpawnPoint.South
    EnemySpawnPoint.East
    EnemySpawnPoint.West
    LootSpawnPoint
    StageExitSpawnPoint             <- เพิ่มสำหรับห้อง Boss
```

- พื้นต้องมี collider ที่เดินได้ และไม่เป็น Trigger
- ผนัง/ขอบห้องต้องกันผู้เล่นออกนอกพื้นที่ โดยเฉพาะช่องประตูที่กราฟอาจไม่ได้ใช้งาน
- เมื่อย้ายทางเข้าออกของโมเดล ให้ย้าย `Exit.*` และจุดเกิดให้ตรงด้วย ตำแหน่งจากฐานเป็นเพียงตำแหน่งเริ่มต้น
- คง `RoomController`, ประตู และ reference ที่สืบทอดจากฐานไว้ ไม่ต้องเพิ่ม controller ซ้ำ
- ระบบสร้าง `RuntimeContent/Persistent`, `Encounter`, `Temporary` เอง ไม่ต้องสร้างหรือผูก reference ไว้ใน prefab

## 3. ตั้งประตูและทิศทาง

ทิศทางอ้างอิงแกน local ของห้องก่อน runtime หมุน ไม่ใช่ตำแหน่งกล้องหรือทิศที่เห็นบนหน้าจอ:

| Direction | ตำแหน่งด้านห้อง | จุดเกิดหันเข้าห้องไปทาง |
| --- | --- | --- |
| Up | +Z | -Z |
| Right | +X | -X |
| Down | -Z | +Z |
| Left | -X | +X |

แต่ละประตูต้องมี:

1. `RoomExitInteractable` ตั้ง `Direction` ให้ตรงด้านจริง และไม่ซ้ำกับประตูอื่นในห้อง
2. `BoxCollider` เปิด `Is Trigger` วางให้ผู้เล่นเล็ง/โฟกัสได้จากด้านในห้อง
3. GameObject ที่มี collider อยู่ Layer **Interactable**
4. `InteractableLink` มี `Targets` อ้างถึง `RoomExitInteractable` ของประตูนั้น
5. `EntranceSpawnPoint` อยู่ด้านในประตูและหันแกน forward เข้าห้อง

Base prefabs ผูกสิ่งเหล่านี้ไว้แล้ว แต่หลังเปลี่ยนตำแหน่งหรือเพิ่มโมเดลต้องตรวจว่า trigger ไม่ถูกผนังบัง และจุดเกิดไม่อยู่ใน collider

ที่ `RoomController` ให้ `Exits` อ้างถึงประตูทั้งหมดที่ใช้จริง หาก array มีสมาชิก ระบบจะใช้รายการนั้นโดยไม่เติมประตูที่ตกหล่นให้ ส่วน array ว่างทั้งชุดจึงจะค้นหาประตูใน children อัตโนมัติ แนะนำผูกให้ครบอย่างชัดเจน

เปิด `Hide Unused Exits` เพื่อซ่อนประตูที่ไม่มีปลายทางในกราฟ `Visual Root` ของประตูควรเป็นเฉพาะ object ที่ต้องซ่อน ห้ามชี้ไปที่ root ห้องหรือพื้นทั้งห้อง การซ่อนประตูไม่ได้สร้างผนังปิดช่องให้อัตโนมัติ จึงต้องออกแบบขอบห้องให้เดินตกไม่ได้ด้วย

ไม่ต้องระบุ node ปลายทางเองใน prefab: `MapRunController` และ `RoomController` ผูกปลายทาง รวมถึงประตูย้อนกลับ ให้ตามกราฟของ run

## 4. ผูกจุดเกิดใน RoomController

| ช่องใน Inspector | วิธีตั้งค่า |
| --- | --- |
| `Player Spawn Point` | จุดเกิดกลางห้องหรือจุดเริ่มที่ปลอดภัย ใช้เมื่อไม่มี entrance direction เช่นเข้า Start ครั้งแรก |
| `Player Spawn Points By Direction` | เพิ่มหนึ่งรายการต่อด้านประตู กำหนด `Direction` และลาก `EntranceSpawnPoint` ของด้านนั้นใส่ `Spawn Point` |
| `Enemy Spawn Points` | ลาก Transform จุดเกิดศัตรูทั้งหมด วางบนพื้นที่เดินได้และไม่ชน props |
| `Loot Spawn Points` | จุดวางรางวัลที่เข้าถึงได้ ไม่อยู่ใต้พื้นหรือชิดผนัง |
| `Stage Exit Spawn Point` | จุดเกิด portal หลังชนะ Boss แยกจากจุดเกิด Boss และกอง loot |
| `Lockdown Objects` | barrier หรือเอฟเฟกต์ที่เปิดเฉพาะช่วงล็อกห้อง อย่าใส่พื้นหรือ root ห้อง |

ระบบเลือกจุดเกิดตามลำดับ: จุดใน `Player Spawn Points By Direction` → child ของประตูที่ชื่อมี `SpawnPoint` → `Player Spawn Point` → Transform ของ `RoomController` ดังนั้นควรผูกค่าจริงแทนการพึ่ง fallback

เว้นพื้นที่ว่างด้านหน้าจุดเกิดให้ player และ companions ยืนเป็น formation ได้ ไม่ใช่มีพื้นที่พอดีแค่คนเดียว ถ้า companion ที่ต้องย้ายไม่สามารถลง NavMesh ได้ การย้ายห้องจะล้มเหลวและย้อนกลับห้องเดิม

สำหรับชุด Test Stage ปัจจุบัน ใช้จุดเกิดศัตรู 4 จุดในห้อง Combat และอย่างน้อย 1 จุดสำหรับ Boss จำนวนศัตรูจริงมาจาก Encounter Definition; ถ้า wave มี spawn มากกว่าจำนวนจุด ระบบจะวนใช้จุดเดิม จึงต้องตรวจการซ้อนกันด้วย

## 5. Bake NavMesh ให้ห้องสำเร็จรูป

วิธีที่ตั้ง scope ได้ง่ายสำหรับ Variant ใหม่:

1. เพิ่ม **NavMesh Surface** บน root ของห้อง Variant
2. ตั้ง `Agent Type` ให้ตรงกับ `NavMeshAgent` ของ companion/enemy ที่จะใช้ห้องนี้
3. ตั้ง `Collect Objects` เป็น **Current Object Hierarchy** เพื่อเก็บ geometry เฉพาะห้องนี้
4. ตั้ง `Use Geometry` เป็น **Physics Colliders** หากต้องการให้เส้นทางอิงพื้นและผนังที่ตัวละครชนจริง
5. ตั้ง `Include Layers` ให้รวมพื้นและผนังที่ต้องใช้ โดยตรวจ layer ของ collider จริง
6. กด **Bake** แล้วตรวจว่า `NavMesh Data` มี asset ไม่เป็น `None` หรือ `Missing`
7. ตรวจพื้นที่ NavMesh รอบทุก entrance, จุดเกิดศัตรู และพื้นที่เดินภายในห้อง แล้วบันทึก prefab
8. ปิดแล้วเปิด prefab อีกครั้งเพื่อยืนยันว่า reference ของ NavMesh Data ถูกเก็บใน asset แล้ว

หากห้องเดิมใช้ child ชื่อ `Navigation` ที่มี `NavMeshSurface` อยู่แล้ว สามารถใช้โครงนั้นต่อได้ แต่ต้องตรวจ scope ให้ครอบคลุมพื้นจริง: **Current Object Hierarchy บน Navigation จะไม่เก็บ Visual ที่เป็น sibling** ต้องปรับโครงหรือวิธี collect ให้ถูกต้อง และไม่เพิ่ม surface ซ้อนโดยไม่จำเป็น

หลังเปลี่ยนพื้น ผนัง ทางแคบ หรือ props ที่ขวางทาง ให้ bake ใหม่และเก็บ NavMesh asset พร้อม `.meta` ของมัน ระบบ Map เปิด/ปิดข้อมูลที่ bake ไว้ตอนเปลี่ยนห้อง **ไม่ได้ Build NavMesh ใหม่ตอน runtime**

## 6. สร้าง Room Definition

ใน Project เลือก **Create > Game > Map > Room Definition** เก็บ asset ใน `Assets/Data/Map/` หรือโฟลเดอร์ย่อยของธีม

ตัวอย่างสำหรับ `RoomPrefab.Sewer.Combat.StraightUpDown`:

| ช่อง | ค่า |
| --- | --- |
| `Display Name` | Sewer Combat Straight |
| `Node Type` | Combat |
| `Weight` | 1 หรือมากกว่า 0 เพื่อให้ถูกเลือกได้ |
| `Room Prefab` | Variant ห้อง Sewer ที่ bake เรียบร้อย |
| `Max Exit Count` | 2 |
| `Exit Mask` | Up และ Down |
| `Allow Superset Exit Mask` | ปิด สำหรับห้องที่ต้องการใช้รูปทรงประตูตรงกับกราฟ |
| `Lock Exits Until Clear` | เปิด |
| `Start Encounter On Enter` | เปิด |
| `Requires Clear Before Exit` | เปิด |

`Exit Mask` ต้องตรงกับ Direction ที่มี socket จริง `Max Exit Count` เป็นความจุด้านประตูของห้อง ไม่ใช่จำนวนศัตรูหรือจำนวนห้องใน run ให้คิดประตูย้อนกลับรวมด้วยเมื่อกราฟต้องใช้

เมื่อเปิด `Allow Superset Exit Mask` ห้องที่มีประตูมากกว่าความต้องการของ node ก็เป็นตัวเลือกได้ โดยประตูส่วนเกินจะไม่มีปลายทาง ระบบให้ความสำคัญกับรูปทรงที่ตรงพอดีก่อน จึงไม่ควรคาดว่าเพิ่ม Weight แล้วห้องที่เป็น superset จะปรากฏแทน exact match เสมอ

ระบบลองหมุนห้องครั้งละ 90 องศาเอง จึงไม่จำเป็นต้องทำ asset ซ้ำทุกทิศสำหรับโมเดลเดียวกัน

ค่าตั้งต้นสำหรับ flow ของแต่ละห้อง:

| ประเภทห้อง | Lock Exits Until Clear | Start Encounter On Enter | Requires Clear Before Exit |
| --- | --- | --- | --- |
| Combat / Elite / Ambush / Trap / Boss ที่ใช้ encounter | เปิด | เปิด | เปิด |
| Start / Shop / Heal / Upgrade / Event ที่ไม่มี encounter | ปิด | ปิด | ปิด |

การตั้ง Node Type อย่างเดียวไม่ได้สร้างร้านค้า จุดฮีล หรือกิจกรรมให้ ต้องมี component ของระบบนั้นใน prefab ด้วย สำหรับ **Heal ใน Test Stage** ให้เพิ่ม `TestStageRecoveryStations` ใกล้ `RoomController` และ author `HealInteractable` กับ `AmmoRefillInteractable` พร้อม link/collider/layer ของแต่ละจุด

ถ้าต้องการรางวัลหลังเคลียร์ ให้ใส่ `Clear Reward Table` และ `Clear Reward Rolls` มากกว่า 0 ส่วน `Map Hint` และ `Map Icon` ใช้กับการแสดงผลแผนที่

## 7. ใส่ห้องเข้า Content Pool ของด่าน

1. เปิด `MapRunConfigSO` ของด่านที่จะใช้ เช่น `Assets/Data/Map/TestStages/Test Stage 01 Map Run Config.asset`
2. ตาม reference `Content Pool` ไปเปิด `MapContentPoolSO` ตัวที่ด่านใช้จริง
3. เพิ่ม Room Definition ใหม่ใน `Room Definitions` ไม่ใช่ลาก GameObject prefab ใส่ array นี้
4. ถ้าห้องเป็นการต่อสู้ ให้ตรวจ `Encounter Definitions` ว่ามี encounter ที่ `Node Type` ตรงกันและ `Weight > 0`
5. เปิด encounter ตรวจ `Waves`, `Enemy Prefabs`, `Spawn Count` และ `Wait For Wave Clear`; Boss ต้องตั้ง `Node Type = Boss` และเปิด `Boss Encounter`
6. ตรวจ `Generation Profile` ว่าเปิดโอกาสให้เกิด node ชนิดและรูปทรงที่ห้องนี้รองรับ

ห้อง Combat ใหม่สามารถใช้ Encounter Definition เดิมของด่านได้ ถ้าต้องการศัตรูชุดเดิม; Room Definition และ Encounter Definition ถูกเลือกตาม node type แยกกัน ไม่ได้ผูกกันแบบหนึ่งต่อหนึ่ง

ถ้าต้องสร้าง pool ใหม่ ใช้ **Create > Game > Map > Profiles > Map Content Pool** แล้วผูกกลับใน config แต่ต้องคงชนิดห้องที่ generator ต้องใช้ให้ครบ ไม่ใช่ใส่เพียง Combat ห้องเดียว Pool ที่หลายด่านแชร์กันจะมีผลกับทุกด่านที่อ้างถึงมัน

Config ต้องมี `Generation Profile` และ `Content Pool`; ด่านที่มี `Stage Id` ต้องมี `Progression Profile` ด้วย ข้อมูลเก่าที่อาจยังเห็นใน YAML ของ config ไม่ใช่รายการที่ควรแก้แทน profile

## 8. ตรวจและทดสอบในเกม

1. บันทึก Variant, NavMesh Data, Room Definition และ Content Pool
2. รัน **Tools > RB Project > Map > Validate Map Content** แล้วแก้ errors ที่เกี่ยวกับห้อง/ด่านนี้ และตรวจ warnings เรื่อง entrance กับ stage exit
3. เมนู **Validate Base Room Prefabs** และ **Validate Start Base Prefab** ใช้ตรวจฐานเท่านั้น ไม่ได้แทนการตรวจห้องใหม่ที่ลง pool แล้ว
4. เข้าเกมผ่าน Basement แล้วเลือกด่านที่แก้ หรือใช้ scene `Assets/Scenes/MapPlayMode GR_01 Prototy/MapRun.unity` โดยตรวจ `MapRunController.Run Config` สำหรับการทดสอบเปิด scene ตรง
5. เริ่ม run ใหม่ให้กราฟสุ่มข้อมูลล่าสุด หากมี config ที่ส่งมาจาก Basement จะใช้ config ที่เลือกนั้น; การแก้ fallback ใน scene อย่างเดียวจึงอาจไม่เปลี่ยนด่านที่เล่น
6. ทดสอบเข้าแต่ละด้านของห้องและรูปแบบที่ถูกหมุน ตรวจ player/companions ไม่ติดผนังหรือเกิดนอก NavMesh
7. ห้องต่อสู้ต้องเกิดศัตรู ล็อกทางออก เคลียร์ได้ และปลดล็อกหลังจบ encounter
8. เดินไปห้องถัดไปแล้วย้อนกลับ ตรวจว่าห้องที่เคลียร์แล้วไม่เริ่ม encounter ซ้ำ และของที่ยังไม่เก็บกลับมาอยู่เหมือนเดิม
9. ห้อง Boss ต้องมี Stage Exit ที่เข้าถึงและใช้กลับ Basement ได้หลังเคลียร์

การเปิด prefab ดูอย่างเดียวไม่ทดสอบการสุ่ม การหมุนห้อง หรือ party warp ต้องทดสอบผ่าน Map run ด้วย หากยังไม่สุ่มเจอ ให้ใช้ชุด pool/config สำหรับทดสอบที่คง node types จำเป็นครบ และจำกัดตัวเลือกเฉพาะประเภท/รูปทรงที่กำลังตรวจ

ไม่ต้องรัน **Generate Base Room Prefabs**, **Generate Start Base Prefab** หรือ **Apply Test Stage Content** ทุกครั้งที่เพิ่มห้อง เมนูเหล่านี้เขียน asset ที่เครื่องมือเป็นเจ้าของใหม่ ใช้เฉพาะเมื่อตั้งใจสร้างฐานหรือคืนชุดข้อมูลมาตรฐาน

## ห้อง Sewer Boss ที่เตรียมไว้

ไฟล์อยู่ใน `Assets/Prefab/MAP/Sewer/PrefabMapaGame/`:

- `Sewer_Boss.prefab` เป็น Variant ของ `Base_Map/DeadEnd/RoomPrefab.Base.DeadEnd.Up.prefab` และเก็บ `PrefabMapaOrigin/Sewer_Boss.prefab` เป็น nested prefab ใต้ `Visual`; ต้นฉบับงานภาพและ base ไม่ถูกแก้ไข
- `Sewer_Boss.NavMesh.asset` เป็นข้อมูลที่ bake จาก Physics Colliders ภายใน hierarchy ของห้อง ด้วย Agent Type 0 (radius 0.5, height 2) และ voxel size 0.1
- `RoomDefinition.Sewer.Boss.asset` ตั้ง `Node Type = Boss`, `Exit Mask = Up`, `Max Exit Count = 1`, `Allow Superset Exit Mask = false`, `Weight = 1` และเปิด flow การเริ่ม encounter/ล็อกประตูจนเคลียร์ครบทั้งสามตัว

Root อยู่ที่ origin, rotation identity และ scale หนึ่ง งานภาพถูกเลื่อนให้ประตูเข้าอยู่ด้าน +Z พื้นและ mesh ของตกแต่งที่ไม่มี collider ได้รับ MeshCollider เป็น override เฉพาะห้องใหม่นี้

| Socket | ตำแหน่ง local | การใช้งาน |
| --- | --- | --- |
| `Exit.North` | `(0, 0, 29.5)` | ประตูกลับห้องก่อนหน้า, trigger บน layer `Interactable` พร้อม `InteractableLink` |
| `PlayerSpawnPoint` / `EntranceSpawnPoint` | `(0, 0.08, 25)` | หันเข้าห้องไปทาง -Z |
| `EnemySpawnPoint.North` | `(0, 0.08, 7)` | จุดเกิดศัตรู index 0 / บอสใน encounter ที่ spawn หนึ่งตัว |
| `EnemySpawnPoint.East / South / West` | `(14, 0.08, -5)` / `(0, 0.08, -22)` / `(-14, 0.08, -5)` | จุดเสริมสำหรับ encounter ที่มีหลายตัว |
| `LootSpawnPoint` | `(0, 0.08, -23)` | รางวัลหลังการต่อสู้ |
| `StageExitSpawnPoint` | `(0, 0.08, -28)` | ตำแหน่ง portal หลังชนะ ใกล้ประตูปลายห้อง |

ห้องนี้ลงทะเบียนใน `Assets/Data/Map/Profiles/Boss Rush 01 Map Run Config Content Pool.asset` แล้ว โดยแทนช่องห้อง Boss เดิมด้วย `RoomDefinition.Sewer.Boss.asset` ทำให้ `Boss Rush 01 Map Run Config.asset` ใช้ Sewer Boss ทุก run; pool นี้ใช้เฉพาะ Boss Rush 01 ส่วนห้อง Start/Heal, encounter `Boss Rush 01 Boss` และ progression/Stage Exit เดิมยังใช้ต่อ โมเดลประตูปลายห้องไม่ใช่ graph exit ตัวที่สอง และ prefab ไม่ได้ฝังศัตรูหรือ portal ไว้ล่วงหน้า

หลังลงทะเบียน ตรวจ config และสุ่มกราฟด้วย seed 0–31 ผ่าน `MapPathValidator` ครบ 32 run โดย Boss node เลือก Sewer Boss และ encounter เดิมทุกครั้ง ต้องเริ่ม run ใหม่เพื่อใช้ห้องที่เปลี่ยน; ยังไม่ได้ทดสอบการต่อสู้และกลับ Basement ใน Play Mode สำหรับการเปลี่ยนห้องครั้งนี้

ตรวจ Room Definition โดยตรงแล้วไม่มี issues, โหลดข้อมูล NavMesh ที่บันทึกกลับมาตรวจเส้นทางถึงทุก socket ได้ครบที่มุม 0/90/180/270 องศา และตรวจ capsule radius 0.5/height 2 ที่จุดเกิดรวมตำแหน่งปาร์ตี้สี่จุดไม่ชน geometry การตรวจนี้ยังไม่แทน Play Mode ของ encounter, party warp และ portal ในด่านจริง

## อาการที่พบบ่อย

| อาการ | จุดที่ควรตรวจ |
| --- | --- |
| ห้องใหม่ไม่ถูกสุ่ม | อยู่ใน Content Pool ที่ใช้งานจริงหรือไม่, Weight, Node Type, Exit Mask, Max Exit Count และมี exact match อื่นที่ถูกเลือกก่อนหรือไม่ |
| เห็นโมเดลประตูแต่กดไม่ได้ | Trigger, Layer Interactable, InteractableLink.Targets, Direction, Exits array และปลายทางของ node |
| เข้าห้องแล้วหันผิดด้าน | Direction ของรายการ entrance และ forward ของ EntranceSpawnPoint ต้องหันเข้าห้อง |
| เปลี่ยนห้องแล้วถูกส่งกลับ | NavMesh Data, Agent Type, พื้นที่ว่างของ formation และ collider รอบจุดเกิด |
| AI ไม่เดินทั้งที่กด Bake แล้ว | Scope ของ Surface, Include Layers, Agent Type และตรวจว่า NavMesh ครอบคลุมพื้นจริง |
| Combat ไม่มีศัตรูหรือเคลียร์ทันที | Encounter ใน pool ที่ Node Type ตรงกัน, Start Encounter On Enter, Waves และ enemy prefabs |
| ศัตรูเกิดซ้อนกลางห้อง | Enemy Spawn Points ว่าง/null หรือมีจุดไม่พอกับ wave |
| ประตูไม่ปลดล็อก | ศัตรูตกฉาก/ติดผนัง/ยังไม่ตาย, HealthSystem ของศัตรู และเงื่อนไข wave clear |
| กลับห้องเก่าแล้วเนื้อหาซ้ำ | Component ที่สร้างเนื้อหาใหม่ทุก OnEnable แทนการเคารพ lifecycle ของห้องที่ cache ไว้ |

รายละเอียดเพิ่มเติม: [Map System](MAP_SYSTEM.md) และ [Prefab authoring / Party warp](../PREFABS_AND_AUTHORING.md#map-room-spawn-and-party-warp)
