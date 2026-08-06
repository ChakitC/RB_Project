# Weapon Affix Framework Implementation Plan

Last updated: 2026-08-03

## Progress Tracker

Current milestone: **Phases 1–4, 6, and 7 complete — extended validation is in progress.**

Next milestone: **Phase 8–9 — exhaustive affix tests, Play Mode, and profiler validation.**

Status legend:

- `[x]` Complete and validated
- `[~]` In progress
- `[ ]` Not started
- `[!]` Blocked; record the blocker in the phase notes

| Phase | Status | Milestone | Validation / Notes |
| ---: | :---: | --- | --- |
| 0 | [x] | Planning and grill session | Architecture, affix rules, roll ranges, weapon pools, persistence, migration, testing, and performance criteria approved on 2026-08-03. |
| 1 | [x] | Typed combat metadata | 2026-08-03: Projectile, Melee, Skill, Status, damage, health, hit-zone, crit, overkill, ammo, and ChainReady metadata compile with compatibility wrappers. |
| 2 | [x] | Custom affix runtime framework | 2026-08-03: lifecycle, stable synchronous dispatch, provenance filtering, timer scheduling, and per-handler fault isolation implemented; legacy runtime fallback removed. |
| 3 | [x] | Pre-shot, pre-damage, and AoE pipelines | 2026-08-03: immutable shot build flow, equipped pre-damage provider, and deduplicated non-alloc hostile AoE implemented. |
| 4 | [x] | Persistent affix runtime state | 2026-08-03: versioned records, DeepClone, and legacy Echo/Breach shotCounter normalization implemented. |
| 5 | [~] | Migrate all existing affixes | 2026-08-03: migration generated/bound all 27 assets; dry-run passes. Additional validator rule coverage remains. |
| 6 | [x] | Implement 13 new affixes | 2026-08-03: all approved ids, rolls, eligibility, runtime actions, Last Round and Overkill AoE implemented. |
| 7 | [x] | Tooltip and feedback infrastructure | 2026-08-03: behavior-authored tooltip data, rolled display, feedback profile contract/events, and development logging implemented. |
| 8 | [~] | Automated tests and asset validation | 2026-08-03: framework EditMode tests pass 6/6. Full trigger/rejection matrix remains. |
| 9 | [~] | Full regression and performance validation | `CheckAssemblyBuild.ps1` passed 2026-08-03: 0 errors, 77 existing/dependency warnings. Play Mode/profiler remain. |
| 10 | [~] | Documentation and final handoff | System, event bus, authoring, validation, and gameplay docs updated. Final Play Mode/performance handoff remains. |

When work advances, update this table in the same change. Record the completion
date, validation commands/results, material deviations from the approved design,
and any remaining manual Unity checks.

## Objective

Build a scalable custom weapon-affix handler framework, migrate all existing
weapon affixes to it without a runtime fallback, and add the thirteen approved
affixes. Combat triggers must be event-driven through `CombatEventBus`; runtime
affixes must not poll for Hit or Kill in `Update()`.

The completed system must:

- preserve current namespaced affix ids and compatible saves;
- create one plain C# runtime instance per affix and weapon instance;
- keep behavior ScriptableObjects stateless;
- support stat, pre-shot, pre-damage, combat-event, timer, ammo, and AoE actions;
- prevent affix-generated events from recursively triggering affixes by default;
- expose structured tooltip and feedback data;
- avoid framework allocations in steady-state Shot, Hit, and Kill paths;
- include automated coverage for the framework, migration, and every affix.

## Approved Architecture

### Definition and runtime ownership

- `WeaponAffixDefinition` references exactly one root behavior asset.
- `WeaponAffixBehavior` is a stateless ScriptableObject used for authoring,
  rolling, validation, tooltip data, and runtime creation.
- Each equipped affix creates its own `IWeaponAffixRuntime` plain C# object.
- `WeaponAffixRuntimeController` owns create, equip, dispatch, timer, unequip,
  fault-isolation, and dispose lifecycle.
- Hybrid affixes such as Blood Magazine use one root handler that owns all their
  actions. A composite behavior can be added later without changing the
  definition schema.
- Runtime Main-affixes are limited to the Main slot in the first release. Static
  stat behavior remains valid for Sub-affixes.

### Event and modifier flow

The required order is:

1. Base weapon and character stats.
2. Permanent affix and upgrade modifiers.
3. Timed runtime stat modifiers.
4. Pre-shot modifiers.
5. Immutable shot creation and projectile spawn.
6. Pre-damage target-specific modifiers at impact.
7. Target damage, health, hit-zone, and stagger resolution.
8. Typed Hit/Kill publication through `CombatEventBus`.
9. Synchronous affix event dispatch with provenance and recursion guards.
10. Gameplay action followed by feedback publication.

Timed stat modifiers use `StatsHub`. Shot-specific and target-specific effects
must not be fed back into `StatsHub`, preventing double application and per-shot
cache invalidation.

### Runtime state lifetimes

- `WhileEquipped`: buffs, stacks, marked targets, timers, and next-shot charges.
  These reset immediately on unequip.
- `WeaponInstance`: deterministic progress such as every-N-shot or every-N-hit
  counters. These survive weapon swaps and save/load.
- A handler updates live serializable state records owned by
  `WeaponInstanceData`; gameplay runtime does not call or depend on SaveManager.
- If an event arrives after the source weapon was unequipped, event-driven
  affixes from that weapon do not proc. Effects embedded into an already spawned
  shot, such as Last Round's explosion, remain valid.

## Phase 1 — Typed Combat Metadata

Primary files:

- `Assets/Scripts/Passives/PassiveEventContext.cs`
- `Assets/Scripts/Passives/CombatEventBus.cs`
- `Assets/Scripts/Passives/DamageContext.cs`
- `Assets/Scripts/DamageCalculator.cs`
- `Assets/Scripts/Stagger/StaggerMeter.cs`
- `Assets/Scripts/Projectile/Projectile.cs`
- `Assets/Scripts/Melee/MeleeController.cs`
- `Assets/Scripts/Player/Skill/SkillHitboxSequenceRuntime.cs`
- relevant status/damage publishers

Tasks:

- Add optional typed combat metadata without changing the meaning of
  `PassiveEventContext.Value` for existing consumers.
- Include requested, resolved-before-health-clamp, and applied damage;
  HealthBeforeHit, MaxHealth, OverkillAmount, critical result, HitZone, stagger
  applied, and whether the hit entered ChainReady.
- Include weapon instance id, ammo before/after, MaxMagazine, whether ammo was
  consumed, IsLastRound, source kind, and weapon-affix provenance where relevant.
- Keep existing convenience APIs through compatibility wrappers when changing
  damage calculator or stagger result contracts.
- Update Projectile, Melee, Skill, and Status publishers to populate the common
  metadata fields they own.
- Do not inherit typed metadata into child events automatically. Callers must
  explicitly copy metadata when it is semantically correct.
- Preserve ChainId, Depth, Origin, EventSourceId, and AttackId behavior.

Checkpoint validation:

- Existing passive tests and combat flows remain valid.
- Hit means applied damage greater than zero.
- Kill and Overkill data use the resolved DamageResult, not collider contact or
  predicted damage.
- Headshot and critical facts can be read without inspecting target components.
- Stagger metadata identifies the exact hit that transitioned into ChainReady.

## Phase 2 — Custom Affix Runtime Framework

Create a focused runtime module under `Assets/Scripts/Weapons/Affixes/Runtime`.
Expected types include:

- `WeaponAffixBehavior`
- `IWeaponAffixRuntime`
- hook interfaces for stat, pre-shot, pre-damage, and combat events
- `WeaponAffixRuntimeContext`
- `WeaponAffixRollSpec`
- `WeaponAffixTooltipData`
- `WeaponAffixTimerScheduler`
- `WeaponAffixProcEvent`

Refactor `WeaponAffixRuntimeController` to:

- resolve common references through `CharacteContext`;
- build runtimes for the current Main and Sub affixes;
- subscribe and unsubscribe from `ctx.CombatEventBus.EventPublished`;
- filter events by current weapon instance id and provenance;
- dispatch synchronously using a stable runtime snapshot;
- isolate exceptions per handler, log full identity/context, and disable only the
  failed handler until the next equip;
- expose stat modifiers and dirty notifications without mutating WeaponSystem's
  public mirror fields;
- run one conditional timer scheduler only while timers exist;
- avoid `GetComponent`, hierarchy scans, and allocations after equip.

`WeaponAffixDefinition` will gain the root behavior reference. Its current
serialized behavior fields remain temporarily for Unity serialization
compatibility, but runtime code will not read them after migration.

`WeaponInstanceFactory` must ask the behavior asset to roll its primary value.
The behavior owns value kind, range, integer rounding, display precision, and
whether a higher or lower roll is better.

`WeaponAffixDatabase.GetCandidates` must accept the full `GunConfig`, enabling
eligibility beyond `WeaponType` such as Last Round's base-magazine rule.

## Phase 3 — Pre-Shot, Pre-Damage, and AoE Pipelines

### Pre-shot

Add `WeaponShotBuildContext` before immutable `WeaponShotContext` creation. It
must include the source weapon instance, rolled shot values, ammo before/after,
whether ammo was consumed, and `IsLastRound`.

The final order inside `WeaponSystem.TryShoot()` is:

1. Validate the shot.
2. Capture ammo-before.
3. Consume ammo when applicable.
4. Capture ammo-after and last-round state.
5. Build base shot stats.
6. Run deterministic upgrade and affix pre-shot hooks.
7. Finalize the immutable shot.
8. Spawn and publish the real post-modifier metadata.

### Pre-damage

Carry an already-resolved modifier/provider reference in the projectile spawn
context. At impact, invoke the target-specific modifier without `GetComponent`.
The provider must confirm the source weapon instance is still equipped before
applying Marked Quarry or other runtime target effects.

### AoE

Create or extract a common pooled/non-alloc area-damage path for Last Round and
Overkill:

- hostile targets only;
- no owner or allied damage;
- deduplicate targets;
- respect invulnerability and damage prevention;
- use affix provenance and the original ChainId/AttackId;
- affix-generated secondary damage does not Crit, Headshot, or Stagger by
  default;
- affix-generated damage does not trigger an affix again unless explicitly
  opted in with depth and once-per-chain guards.

## Phase 4 — Persistent Affix Runtime State

Add a versioned `WeaponAffixRuntimeStateData` collection to
`WeaponInstanceData`. A record contains:

- affix id;
- schema version;
- serializable typed key/value entries for int, float, bool, or string data.

Handlers bind to their record once and cache entry references, so changing a
counter creates no combat-path allocation. Extend `DeepClone()` to clone every
record and value.

Keep the existing `shotCounter` field temporarily. Save normalization migrates
it into the corresponding Echo Chamber or Breach Chamber state record. Current
namespaced ids remain unchanged. Experimental ids `Damge.1`, `Crit.1`, and
`Stabiity.1` remain unsupported as documented.

## Phase 5 — Migrate Existing Affixes

Migrate all fourteen definitions currently registered in
`WeaponAffixDatabase`:

- nine permanent stat modifiers;
- three timed reload buffs;
- Echo Chamber;
- Breach Chamber.

There is no legacy runtime fallback after migration. Every registered affix must
reference a valid behavior asset.

Add Editor tooling under `Assets/Scripts/Editor/WeaponAffixes`:

- dry-run migration report;
- idempotent behavior-asset generation and binding;
- Unity Undo and dirty/save handling;
- duplicate-id, missing-behavior, invalid-slot, invalid-roll, handler-state, and
  weapon-eligibility validation;
- a build preprocessor that blocks Unity builds on hard validation errors.

Warnings, rather than hard errors, cover zero weight, empty weapon pools, and
missing optional feedback assets. `OnValidate` reports issues but never silently
repairs assets.

## Phase 6 — New Affixes

All rolls happen once when the weapon instance is created and remain stable.
Float rolls use a uniform distribution and are stored at full precision.

| Id | Affix | Slot / Eligible Weapons | Approved Roll and Behavior |
| --- | --- | --- | --- |
| `weapon.sub.stagger_power.v1` | Impact Core | Sub / all firearms | StaggerPower +10–20% AddPercent. Affects only shots from that weapon. |
| `weapon.main.kill_clip.v1` | Kill Clip | Main / all firearms | Kill grants Damage +12–18% for 4s. No stacks; refresh duration. |
| `weapon.main.execution_feed.v1` | Execution Feed | Main / Pistol, Rifle, Smg, Hmg | Each direct Kill restores ceil(8–12% MaxMagazine), minimum 1, without reserve cost or canceling reload. Each killed target can reward ammo. |
| `weapon.main.last_round.v1` | Last Round | Main / Rifle, Smg, Hmg; base MaxMagazine at least 25 | A consumed final round gains +25% direct damage and explodes in a 2.5-unit radius for 45–75% of pre-crit shot damage. Once per AttackId. |
| `weapon.main.head_hunter.v1` | Head Hunter | Main / Sniper, Shotgun, Pistol, Rifle | Head hits grant CritMultiplier +0.08–0.16 per stack, up to 5. Head hits refresh 4s; a damaging non-head hit resets all stacks. One stack per AttackId. |
| `weapon.main.fresh_chamber.v1` | Fresh Chamber | Main / all firearms | A reload that inserted ammo grants 3 charges. Each consumed-ammo shot gains Damage +15–25% and consumes one charge. Multi-pellet attacks consume one charge. |
| `weapon.main.hot_streak.v1` | Hot Streak | Main / Rifle, Smg, Hmg | Direct Kill grants one stack of FireInterval -4–6%, up to 3. Kills refresh all stacks to 4s. Multi-kills may add multiple stacks. |
| `weapon.main.pressure_point.v1` | Pressure Point | Main / all firearms | Repeated damaging hits on the same target grant StaggerPower +6–10% per stack, up to 5, with a 3s timeout. One stack per AttackId; changing target resets. |
| `weapon.main.conservation_round.v1` | Conservation Round | Main / all firearms | Roll an integer threshold of 4–6 confirmed hits. One progress per AttackId restores 1 magazine round at threshold. Progress persists with the weapon and is retained while the magazine is full. |
| `weapon.main.overkill.v1` | Overkill | Main / Sniper, Shotgun, Pistol | A killing hit whose overkill reaches 20% MaxHealth creates a 3-unit explosion for 60–90% of OverkillAmount, capped at the resolved killing-hit damage. No recursive explosion. |
| `weapon.main.broken_guard.v1` | Broken Guard | Main / all firearms | Causing the transition into ChainReady grants one 5s charge. The next consumed-ammo shot has 100% CritRate and CritMultiplier +0.35–0.65. The charge is consumed even if the shot misses. |
| `weapon.main.marked_quarry.v1` | Marked Quarry | Main / Rifle, Smg, Hmg | Five same-target hits apply Exposed for 5s. This weapon deals +20–30% damage to that target. One mark per AttackId; changing target before five resets progress. |
| `weapon.main.blood_magazine.v1` | Blood Magazine | Main / Rifle, Smg, Hmg | A direct Kill from a shot whose AmmoAfterShot is at most 25% restores ceil(15–25% MaxMagazine), minimum 1, and grants Stability +15 for 4s. Once per AttackId. |

Weights:

- every new Main-affix: `1.0`;
- Impact Core: `0.85`;
- existing weights remain unchanged.

Free-ammo shots do not trigger or consume Last Round, Fresh Chamber, or Broken
Guard charges because no magazine round was consumed.

## Phase 7 — Tooltip and Feedback Infrastructure

Replace enum-based tooltip switches with structured data supplied by behavior
assets. Tooltips must display the actual rolled value, trigger, duration, stack
or charge cap, and important restrictions. `WeaponAffixDefinition.description`
remains flavor text rather than the authoritative gameplay rule.

Publish feedback events after gameplay actions succeed:

- Activated
- StackChanged
- ChargeReady
- Consumed
- Expired

The initial scope includes optional feedback profiles and Development Build
debug output. It does not include new production HUD, VFX, or SFX assets.

## Phase 8 — Automated Tests and Asset Validation

Add NUnit/Unity tests under `Assets/Scripts/Editor/WeaponAffixes`.

Framework coverage:

- behavior roll ranges and deterministic test roll sources;
- candidate eligibility and weights;
- equip/unequip/dispose and event unsubscription;
- synchronous dispatch and fault isolation;
- provenance, depth, and once-per-chain protection;
- timer scheduling and expiry;
- modifier phase ordering;
- metadata population and non-inheritance;
- persistent state, DeepClone, and save migration;
- migration-tool dry-run and idempotency.

Affix coverage:

- at least one successful trigger and one rejected trigger per affix;
- rolled minimum and maximum behavior;
- multi-pellet, piercing, multi-target, and multi-kill AttackId rules;
- free-ammo behavior;
- reload interaction;
- weapon swap while a projectile is in flight;
- transient reset and persistent progress behavior;
- affix-generated damage recursion prevention.

## Phase 9 — Full Validation

For C# validation, use only:

```powershell
powershell -ExecutionPolicy Bypass -File 'P:\Game_RB_Project\RB_Project\Assets\Scripts\CheckAssemblyBuild.ps1'
```

Do not use `dotnet build` directly against Unity-generated projects. Keep all
validation artifacts outside `Assets`.

Performance acceptance criteria:

- no framework allocation per Shot, Hit, or Kill in steady state;
- no hierarchy lookup after equip;
- no per-affix coroutine;
- one conditional central timer scheduler;
- non-alloc/pooled AoE queries;
- zero leaked subscriptions after disable/unequip;
- Profiler smoke test with rapid fire and multi-target combat.

Manual Play Mode validation must cover each eligible weapon family, reload
modes, full and nearly empty magazines, world slow, weapon swaps, target death,
ChainReady, save/load, and inventory tooltip display.

## Phase 10 — Documentation and Handoff

Update these documents with the implementation rather than only at final cleanup:

- `Docs/SYSTEMS/WEAPON_SYSTEM.md`
- `Docs/ARCHITECTURE/COMBAT_EVENT_BUS.md`
- `Docs/PREFABS_AND_AUTHORING.md`
- `Docs/VALIDATION.md`
- `Docs/GAMEPLAY_OVERVIEW.md` for the player-facing affix summary

The final handoff must state:

- the last completed phase;
- validation commands and results;
- automated and manual checks completed;
- remaining Unity Editor actions;
- documentation updated;
- any approved-plan deviations.

## Definition of Done

- All fourteen existing affixes run through the new framework with no runtime
  fallback.
- All thirteen new affixes are registered, eligible, rolled, displayed, and
  tested according to this document.
- Current namespaced saves load without changing existing affix rolls.
- Combat publishers provide correct typed metadata.
- Framework and per-affix automated tests pass.
- Assembly validation passes.
- Migration and asset validators report no hard errors.
- Manual Play Mode and performance checks pass.
- Required system, architecture, authoring, validation, and gameplay docs match
  the implemented behavior.

## Out of Scope

- Migration of experimental ids `Damge.1`, `Crit.1`, and `Stabiity.1`.
- More than one runtime Main-affix on a weapon.
- Runtime behavior on Sub-affixes other than static stat modification.
- New production HUD, VFX, or SFX content.
- Allowing affix-generated damage to proc affixes by default.
- Retaining event-driven affix runtimes after their weapon is unequipped.
