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

1. configured passive slots in `CharacterSkillManager`, when present
2. `ctx.baseStats.passives`, when configured passive slots are not used
3. `runtimePassives`
4. `IPassiveDefinitionProvider` components
5. `extraPassives`

After loadout refresh, the controller rebuilds stat modifier providers, raises
`StatModifiersChanged`, and refreshes optional event sources.

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
