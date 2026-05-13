# WeaponSystem Stats Refresh

## สรุป

`WeaponSystem` ไม่เรียก `RefreshDerivedStats()` ทุก frame แล้ว แต่ใช้ dirty flow แทน:

1. ระบบที่เปลี่ยน stat เรียก `StatsHub.MarkDirty()`
2. `StatsHub` ยิง event `StatsDirty`
3. `WeaponSystem` รับ event แล้ว mark derived stats เป็น dirty
4. `WeaponSystem` refresh ค่าเมื่อจำเป็น เช่น ก่อนยิง, ก่อน reload, ตอน `Update()` frame ถัดไป, หรือเมื่อตัว weapon/instance เปลี่ยน

ผลคือบัพ/ดีบัพปกติยังทำงานทัน โดยไม่ต้องคำนวณ stat ซ้ำทุก frame

## Contract

ทุกระบบที่เปลี่ยนค่า stat ต้องเรียก:

```csharp
statsHub.MarkDirty();
```

เมื่อค่า stat มีผลต่อ weapon เช่น:

- Damage
- CritRate
- CritMultiplier
- FireInterval
- ReloadTime
- Stability
- BulletSpeed
- MaxMagazine

ถ้าระบบนั้นเป็น `IStatModifierProvider` และค่าที่ append เปลี่ยนไป ต้องเรียก `MarkDirty()` ตอนค่ามีการเปลี่ยนจริง

## Flow ที่ปลอดภัย

ระบบเหล่านี้ปลอดภัย เพราะมีการ mark dirty อยู่แล้ว:

- `StatusEffectController` เมื่อบัพ/ดีบัพถูกเพิ่ม, refresh, หมดเวลา, หรือถูก remove
- `WeaponAffixRuntimeController` เมื่อ equip weapon หรือ reload buff ทำงาน
- `WeaponUpgradeRuntimeController` เมื่อ equip weapon หรือ reload buff ทำงาน
- `PlayerInventory.NotifyWeaponInstanceChanged()` และ `WeaponUpgradeService.NotifyWeaponInstanceChanged()`

ดังนั้นกรณีทั่วไปจะทำงานถูก:

- ได้บัพ damage แล้วนัดถัดไปใช้ damage ใหม่
- ดีบัพ fire rate หมดเวลาแล้ว frame ถัดไปกลับเป็นค่าใหม่
- auto fire ค้างอยู่แล้วบัพเปลี่ยน ค่า refresh ก่อนยิงใน frame นั้น
- reload ใช้ reload time / max magazine ล่าสุดก่อนเริ่ม reload

## กรณีที่ต้องระวัง

จะมีปัญหาได้ถ้ามี modifier provider ที่ค่าเปลี่ยนเอง แต่ไม่เรียก `StatsHub.MarkDirty()` เช่น:

- stat เปลี่ยนตามเวลาแบบ custom โดยไม่ผ่าน `StatusEffectController`
- stat เปลี่ยนตาม HP, combo, heat, stance, distance หรือเงื่อนไขอื่นทุก frame
- provider เก็บ internal state แล้ว `AppendStatModifiers()` คืนค่าไม่เท่าเดิม แต่ไม่มีใครบอก `StatsHub`

ถ้ามีระบบแบบนี้ ให้แก้โดย:

1. เรียก `statsHub.MarkDirty()` ตอนค่าที่ส่งออกเปลี่ยน
2. หรือถ้าค่านั้นจำเป็นต้องสดทุก frame จริง ๆ ให้แยกเป็นระบบคำนวณเฉพาะจุด ไม่ควรทำให้ `WeaponSystem` กลับไป refresh stat ทั้งหมดทุก frame

## ไฟล์ที่เกี่ยวข้อง

- `Assets/Scripts/Player/WeaponSystem.cs`
- `Assets/Scripts/Player/StatsHub.cs`
- `Assets/Scripts/StatusEffects/StatusEffectController.cs`
- `Assets/Scripts/Weapons/WeaponAffixRuntimeController.cs`
- `Assets/Scripts/Weapons/WeaponUpgradeRuntimeController.cs`
