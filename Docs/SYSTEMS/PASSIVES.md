# Passive System

The passive system is event-driven. Core gameplay systems publish combat events
to `CombatEventBus`, and `PassiveController` evaluates equipped passive
definitions.

## Main Files

- `Assets\Scripts\Passives\PassiveTypes.cs`
- `Assets\Scripts\Passives\PassiveDefinition.cs`
- `Assets\Scripts\Passives\TriggeredPassiveDef.cs`
- `Assets\Scripts\Passives\CustomPassiveDef.cs`
- `Assets\Scripts\Passives\PassiveController.cs`
- `Assets\Scripts\Passives\CombatEventBus.cs`
- `Assets\Scripts\Passives\PassiveEventContext.cs`
- `Assets\Scripts\Passives\IStatModifierProvider.cs`
- `Assets\Scripts\Passives\PassiveEventSource.cs`
- `Assets\Scripts\Passives\PassiveEventSourceKind.cs`
- `Assets\Scripts\Passives\PassiveEventSourceRequest.cs`
- `Assets\Scripts\Passives\PassiveEventSourceRegistry.cs`
- `Assets\Scripts\Passives\MovementDistanceEventSource.cs`

## Passive Kinds

Current passive kinds:

- `AlwaysOn`: contributes stat modifiers while equipped.
- `Triggered`: listens to events and executes configured actions.
- `Custom`: runs custom behavior objects on equip and on passive events.

## Passive Loadout Sources

`PassiveController.RefreshPassiveLoadout()` gathers passives from:

1. configured passive-kind options in `CharacterStats.skillSlots` (via
   `CharacterSkillManager`)
2. `runtimePassives`
3. `IPassiveDefinitionProvider` components
4. `extraPassives`

`CharacterStats.passives` and the either/or fallback onto it were removed in
Phase 2 (hard cut, no save-compat shim) now that every passive is authored as
a skill slot — see "Passives Are Skills" below.

After loadout refresh, the controller rebuilds stat modifier providers, raises
`StatModifiersChanged`, and refreshes optional event sources.

`RefreshPassiveLoadout()` also preserves state across a rebuild instead of
wiping it: in-flight `_activeModifiers` are restored wholesale, and each
`TriggeredPassiveRule`'s `Counter`/`WindowExpiresAt`/`CooldownReadyAt` are
copied onto the matching rule in the new loadout by `(passiveId, ruleId)`.
`OnEquipped`/`OnUnequipped` on `PassiveCustomBehavior` only fire for
definitions that actually joined or left the loadout. This matters because
unlocking a node in a passive's upgrade tree (see below) triggers a full
refresh mid-combat.

### Refresh Ordering Contract

`PassiveController` runs at `[DefaultExecutionOrder(-112)]`, ahead of
`CharacterSkillManager` (default order 0), so its first `RefreshPassiveLoadout()`
in `OnEnable` can run before the skill manager has resolved its slots for the
first time. `CharacterSkillManager` exposes `event Action PassiveLoadoutChanged`,
raised at the end of `RebuildResolvedCommandSlots()` and again whenever a
passive-kind slot's selected option or upgrade tree changes
(`ApplySelectedSkillOption`, `HandleActiveSkillTreeChanged`).
`PassiveController.ResolveReferences()` subscribes to whichever
`CharacterSkillManager` it resolves through `ctx.SkillManager`, re-subscribing
if that reference ever changes. Do not call `PassiveController.RefreshPassiveLoadout()`
directly from `CharacterSkillManager` — publish through the event instead, so
the skill manager does not own passive lifecycle.

## Passives Are Skills (Passive Execution Mode)

A passive **is** a skill that executes in passive form instead of cast form.
`SkillGemDefinition` and `PassiveDefinition` both derive from the abstract
`SkillDefinitionBase` (`Assets\Scripts\Player\Skill\SkillDefinitionBase.cs`),
and `CharacterSkillLoadoutOption.skillAsset` is typed as `SkillDefinitionBase`.
There is one `CharacterStats.skillSlots` list, one loadout option type, and one
shared `skillPoints` point pool / upgrade-tree progress system
(`CharacterActiveSkillProgress`) for both active gems and passives — a
passive's upgrade tree unlocks and refunds exactly like an active skill's.

- `CharacterSkillLoadoutOption.IsPassive` is true when `skillAsset` is a
  `PassiveDefinition`; `ActiveSkillAsset` / `PassiveAsset` cast to the
  concrete kind.
- **A slot's kind is fixed**: every configured option in a slot must be the
  same kind (all active or all passive). A slot carries a `hotkey` and cast
  semantics, which are meaningless if the kind can vary per option.
- **Passive-kind slots must be last** in `skillSlots`. `CharacterSkillManager`
  resolves the runtime slot index 1:1 with the authored index, so a passive
  slot ahead of an active one would shift every active slot's index.
- **Passive-kind slots must use `hotkey: None`** — passives are never
  castable; `CharacterSkillManager` clears the runtime slot instead of
  building a `SkillInstance` for a passive option.
- `SkillUpgradeTreeValidator` enforces all three rules as errors (a mixed
  slot, or an active slot after a passive one), plus a warning if a passive
  slot keeps a non-`None` hotkey.

### Flag-Gated Rules

Two mechanisms consume upgrade-id flags (`grantedUpgradeIds`) regardless of
whether a node also carries numeric `statModifiers`:

- `TriggeredPassiveRule.requiredUpgradeId` — the rule stays inert unless the
  owning slot's upgrade snapshot grants that id. Checked through the shared
  `PassiveUpgradeGate.IsRuleEnabled(rule, snapshot)` predicate, at both the
  event-handling site and the optional-event-source request site.
- Upgrade-id fields on `PassiveCustomBehavior` subclasses, e.g.
  `DropAmmoOnShotPassiveBehavior.extraDropUpgradeId` — read from the
  snapshot passed into `OnPassiveEvent` per call, not cached at equip, since
  behaviors are shared assets across characters.

Every `PassiveDefinition` subclass overrides `CollectUpgradeIds` so the
validator can cross-check declared vs. granted ids: `TriggeredPassiveDef`
collects its rules' `requiredUpgradeId`s and its rules' `upgradeOverrides[].upgradeId`s
(see below), `CustomPassiveDef` forwards to its behaviors, and
`AlwaysOnPassiveDef` collects nothing — it has no gate mechanism, so **an
upgrade tree on an `AlwaysOnPassiveDef` is unsupported** and the validator
errors on it.

### Numeric Scaling (Phase 2)

Passive nodes can carry numeric `statModifiers` like active-skill nodes.
`SkillUpgradeStatSnapshot.AddNode` no longer filters by
`SkillUpgradeStatSnapshot.Supports(StatType)` — every `StatType` on a node
aggregates into the snapshot. `Supports()` still exists, scoped to exactly the
stats `Apply(FinalSkillStats)` can write; only `SkillUpgradeTreeValidator`
still uses it, and only to flag an out-of-whitelist stat as an error on an
**active**-skill-owned tree (it's expected and untouched on a passive-owned
tree, since custom behaviors and always-on/triggered modifiers read those
stats through the mechanisms below, not through `Apply`).

Two ways a passive reads an aggregated value:

- `SkillUpgradeStatSnapshot.TryGetAggregate(StatType, out float add, out float multiply)`
  — for a genuinely continuous knob when an existing `StatType` honestly means
  that thing. `PassiveActionDefinition.modifiers` (`AppendStatModifiers`,
  `AddAlwaysOnPassive`) scale this way: `(value + add) * multiply * stackCount`
  — the snapshot aggregate scales the authored value first, stack count
  multiplies the already-scaled result. Always-on modifiers scale once at
  loadout-rebuild time (`AddAlwaysOnPassive`, static per loadout); active
  triggered modifiers scale per call where `RuntimeStatModifier` is
  constructed (`AppendStatModifiers`), since the snapshot travels with the
  `ActivePassiveModifier`.
- `SkillUpgradeStatSnapshot.HasUpgrade(id)` — for a discrete step, e.g.
  `DropAmmoOnShotPassiveBehavior.procChanceBoostUpgradeId` /
  `cooldownReductionUpgradeId` add a fixed bonus/reduction when granted. This
  is the recommended default; reach for `TryGetAggregate` only when a
  continuous stat genuinely applies. Never add passive-only entries to
  `StatType` to make a custom behavior's knob work.

`TriggeredPassiveRule.upgradeOverrides` (`List<PassiveRuleFieldOverride>`)
gives `requiredCount`/`countWindowSeconds`/`cooldownSeconds` — none of which
are `StatType`s — their own per-upgrade-id override list, keyed by
`PassiveRuleField` and reusing `ModifierOp` (`Flat`/`AddPercent`/`Multiply`,
same semantics as `CharacterStatFormula.ApplyModifiers`). These resolve once
per loadout rebuild, not per event: `PassiveController`'s internal
`TriggeredRuleState` caches `ResolvedRequiredCount` /
`ResolvedCountWindowSeconds` / `ResolvedCooldownSeconds` from
`(rule, runtime.Upgrades)` when the runtime is (re)built, and
`RecordEvent`/`IsReady`/`Consume` read the cache. If a rebuild shortens
`cooldownSeconds`, the carried-over `CooldownReadyAt` is re-clamped to
`min(carried, now + newCooldownSeconds)` in `CopyStateFrom` — an upgrade must
never leave the player worse off than it promises.

### Known Limitation: Lobby Stat Preview

`LobbyCharacterStatPreview` has no path into `skillSlots`, so a slotted
passive does not contribute to the lobby stat preview (only equipped
weapon/accessory bonuses and accessory-sourced passives do). This is invisible
today because `ForgottenBulletBag` (Custom, below) contributes no stat
modifiers, but it will be wrong the moment a stat-bearing passive is authored
onto a skill slot. Not fixed in Phase 2.

## Triggered Passive Rules

`TriggeredPassiveRule` can filter and gate events by:

- event type
- event origin
- required count
- count window
- cooldown
- counter consume mode
- target requirement
- attack id requirement
- once-per-target-per-chain behavior
- optional event source kind and source id

Matching rules execute action definitions.

## Passive Actions

Current passive action types:

- `GrantModifier`: applies runtime stat modifiers through the target
  `PassiveController`.
- `ApplyStatusEffect`: applies a status effect through the target
  `StatusEffectController`.
- `EmitEvent`: publishes a child event on the same combat event chain.

Child events carry passive origin metadata and increment event depth. Depth is
bounded to avoid recursive passive loops.

Each action type has its own optional identity override:

- `modifierKeyOverride` affects only runtime modifier stacking and refresh.
- `appliedByIdOverride` affects only status-effect application provenance.
- `emittedEventSourceIdOverride` affects only child-event routing.

Author the scoped override for the action type, or leave it empty to use
`{passiveId}:{ruleId}:{actionId}`.

## Ammo Drop On Shot

`DropAmmoOnShotPassiveBehavior` listens for external `ShotFired` events and can
spawn a configured `SkillPickup` behind the firing character. Proc chance,
internal cooldown, collector rule, drop count, placement, and drop arc are
authored per behavior asset, so character passives and accessory passives can
reuse the behavior without sharing balance values.

`Feno's Field Pack` uses its own behavior, pickup prefab, and pickup effect. It
has a 6% proc chance with a 2-second internal cooldown and restores 4 magazine
rounds to the collector and allies. Feno's character passive remains separate
and keeps its stronger 8-round pickup.

Feno's own `Passive.Feno_ForgottenBulletBag` (a `CustomPassiveDef`) is
authored as the last, `hotkey: None` skill slot on `ChaDef.Feno` rather than
in `passives:` — the first character migrated onto the passive-as-skill model.
Its behavior's `extraDropUpgradeId` (`passive.feno.bulletbag.extra_drop`) is
granted by the one node in `Passive.Feno_ForgottenBulletBag_Tree`; unlocking
that node doubles the pickup drop count for that proc. The base 8% chance and
1.5s cooldown are the same `HasUpgrade`-flag convention's two other knobs
(`procChanceBoostUpgradeId`, `cooldownReductionUpgradeId`) — inert until a
tree node is authored to grant one of those ids.

## Core Events

Core gameplay events should stay in their owning systems:

- `ShotFired` from weapon firing
- `Hit` and `Kill` from projectile, melee, or skill hit logic
- `TakeDamage` and `DamagePrevented` from health/damage logic
- `Reload` from reload logic
- `DashStarted`, `DashEnded`, and `PerfectDodge` from dash logic

These events publish directly to `CombatEventBus`.

## Optional Event Sources

Optional event sources are for monitored or polling-style events that should
exist only when at least one equipped passive asks for them.

Current source kind:

- `MovementDistance`: implemented by `MovementDistanceEventSource`

`PassiveController` scans triggered passive rules for source requests. For each
required source kind it:

1. finds an existing matching `PassiveEventSource`, or
2. auto-adds one from `PassiveEventSourceRegistry`, then
3. applies only the requests for that kind.

When a source kind is no longer required, the controller clears its requests and
destroys auto-added source components.

## Movement Distance Source

`MovementDistanceEventSource` tracks horizontal movement distance and publishes
an event every configured distance step.

Rules configure:

- `eventSourceKind = MovementDistance`
- `trigger = MovementDistanceReached`
- `eventSourceFloatValue` as the distance step
- optional `eventSourceId`; when empty, the rule generates a stable runtime id

The source publishes through `CombatEventBus` and sets the event `EventSourceId`.
Triggered rules match `context.EventSourceId` against `rule.RuntimeEventSourceId`.

## Adding A New Optional Source

1. Add a value to `PassiveEventSourceKind`.
2. Implement `PassiveEventSource`.
3. Keep the source inactive when it has no requests.
4. Publish events through `CombatEventBus`.
5. Set `PassiveEventContext.EventSourceId` to the request source id.
6. Add the source type to `PassiveEventSourceRegistry`.
7. Add authoring guidance for rule fields.

## Dynamic Stat Modifier Rule

Runtime stat modifiers use `ModifierKey`, not event source identity. The key is
used for passive stack/replace/refresh behavior and for `StatsHub` cache input
signatures. Status-effect modifiers use `status:{effectId}` so changing who
applied an effect does not change the modifier's ownership key.

If a passive, custom passive behavior, or optional event source changes the
output of an `IStatModifierProvider`, it must raise `StatModifiersChanged`.

`StatsHub` still probes modifier signatures before returning cached stats, so a
missed notification should not leave stats permanently stale. The event remains
the preferred fast path for immediate invalidation.
