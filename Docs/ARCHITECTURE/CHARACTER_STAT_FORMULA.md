# Character Stat Formula

`CharacterStatFormula` is the single source of truth for character stat math. Both
the live combat aggregator (`StatsHub`) and the Basement status preview
(`LobbyCharacterStatPreview`) call it, so a displayed number and an in-combat
number cannot drift apart.

Files:

- `Assets\Scripts\Player\CharacterStatFormula.cs`
- `Assets\Scripts\Player\CharacterStatInputs.cs`
- `Assets\Scripts\Player\CharacterStatTotals.cs`

## Modifier Application

```
result = (baseValue + flatSum) * (1 + addPercentSum * 0.01) * multiplyProduct
```

- `ModifierOp.Flat` sums into `flatSum`.
- `ModifierOp.AddPercent` uses whole percentages (`10` means `+10%`).
- `ModifierOp.Multiply` uses direct factors (`1.2` means `x1.2`), each clamped by
  `Mathf.Max(0f, value)`.

Do not unify `AddPercent` and `Multiply`; they are separate authoring concepts.

## Inputs And Totals

`CharacterStatInputs` carries the eight character base values, already level
scaled as `value + scaling * max(0, level - 1)`, plus the equipped `GunConfig`.
Weapon-derived raw values are read off that `GunConfig` inside the struct, so an
unarmed character automatically feeds `0` damage, `0` crit rate, and
`BaseCritMultiplier` for crit multiplier.

`CharacterStatTotals` carries every final stat, including the ones only combat
uses (fire interval, reload time, stability, bullet speed, magazine, reserve
ammo, skill base damage). Clamps live in `Compute` and are load-bearing:
MaxHP and MaxStamina clamp to at least `1`, crit multiplier to at least `1`, crit
rate to `0..100`, and stability to `MinStabilityPercent..MaxStabilityPercent`.

## Live Combat Path

`StatsHub` keeps everything it already owned: the dirty flag, input-signature
hashing, revision counter, provider subscription, the off-current-weapon
`Get*Internal(GunConfig w)` paths, and its serialized `dbg*` inspector fields. It
collects modifiers from every `IStatModifierProvider` in the character hierarchy,
builds `CharacterStatInputs` from its `GetCharacter*Base()` helpers, then assigns
the returned totals into its cache.

## Lobby Preview Path

`LobbyCharacterStatPreview.Build(stats, level, characterId, inventory, affixDatabase)`
produces the same totals for a character that exists only as data, with no
GameObject and therefore no `IStatModifierProvider` components. It gathers:

| Source | How it is read |
|---|---|
| Weapon base stats | `EquipmentAssignmentService.GetEquippedSlotData(..., Weapon, ownerId)` |
| Weapon upgrade | `WeaponUpgradeCurve.AppendStatModifiers` via `WeaponUpgradeService.ResolveUpgradeCurve` |
| Weapon affixes | Authored data only - see below |
| Accessories | `AccessoryLoadout.AppendInstanceStatModifiers` per equipped slot |
| Passives | `CharacterStats.passives` plus accessory passives; only `AlwaysOnPassiveDef` contributes |

`ownerId` is built with `CharacterEquipment.BuildCharacterOwnerId(characterId)`,
matching how `UIEquipment` binds. Accessory slots are enumerated with
`AccessoryLoadout.ResolveOwnerSlotCount(ownerId)`.

Deliberately excluded, because they can never be active outside combat:

- temporary status effects (`StatusEffectController`)
- timed and stacking weapon affix buffs

### Weapon Affixes Are Read, Never Constructed

The preview never calls `WeaponAffixBehavior.CreateRuntime`, `OnEquip`, or
`WeaponInstanceData.GetOrCreateAffixState` - all three have side effects. It
resolves each rolled affix to its `WeaponAffixDefinition`, casts
`rootBehavior` to `ConfiguredWeaponAffixBehavior`, and includes only the
unconditional kinds `StaticStat` and `ImpactCore`. The buff kinds
(`ReloadBuff`, `KillClip`, `HeadHunter`, `HotStreak`, `BloodMagazine`) are gated
on timers or stacks and evaluate to zero in the lobby anyway.

`ConfiguredWeaponAffixBehavior` is currently the only `WeaponAffixBehavior`
subclass. If another one is added and it contributes static stats, it must be
handled in `LobbyCharacterStatPreview` too, or the panel will silently
under-report.

## Expected Divergence

The Basement panel and a character's in-combat `StatsHub` should match exactly at
rest. They are expected to differ once a buff affix, status effect, or other
timed source is active - the in-combat value is then higher by design.
