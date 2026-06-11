# Passive Event Source Architecture

This document describes the hybrid passive event architecture used by the passive system.

## Goal

Passive events are split into two groups:

- Core gameplay events that already happen inside existing systems.
- Optional monitored events that require extra runtime watchers.

Core gameplay events stay where they are. Optional monitored events are created only when at least one equipped passive requests them.

## Core Gameplay Events

These events are published directly by the system that owns the behavior:

- `ShotFired` from weapon firing logic.
- `Hit` and `Kill` from projectile, melee, and skill hit logic.
- `TakeDamage` and `DamagePrevented` from health/damage logic.
- `Reload` from reload logic.
- `DashStarted`, `DashEnded`, and `PerfectDodge` from dash logic.

These events do not use `PassiveEventSourceKind`. They should keep publishing directly to `CombatEventBus`.

## Optional Event Sources

Optional event sources are used for events that need monitoring or polling, such as:

- Movement distance reached.
- Standing still for a duration.
- Low health pulse.
- Nearby enemy count.
- Time without taking damage.

These should be implemented as `PassiveEventSource` components. They are not required on every character.

## Main Types

### `PassiveEventSourceKind`

Identifies a source category.

Current source kinds:

```csharp
public enum PassiveEventSourceKind
{
    None,
    MovementDistance
}
```

Future sources should add one enum value here.

### `PassiveEventSourceRequest`

Represents one passive rule's runtime request for an optional source.

Important fields:

- `Kind`: source kind to activate.
- `EventType`: event type the source should publish.
- `EventSourceId`: stable id used to route events back to matching rules.
- `FloatValue`: source-specific numeric value, such as movement distance.
- `IntValue`: source-specific integer value for future sources.

### `PassiveEventSource`

Base class for optional source components.

```csharp
public abstract class PassiveEventSource : MonoBehaviour
{
    public abstract PassiveEventSourceKind Kind { get; }

    public abstract void ApplyRequests(
        CharacteContext ctx,
        CombatEventBus combatEventBus,
        IReadOnlyList<PassiveEventSourceRequest> requests);

    public abstract void ClearRequests();
}
```

Rules:

- A source should only tick/scan while it has active requests.
- `ClearRequests()` must stop source logic and clear runtime state.
- A source should publish events through `CombatEventBus`.
- A source should set `PassiveEventContext.EventSourceId` to the request `EventSourceId`.

### `PassiveEventSourceRegistry`

Maps `PassiveEventSourceKind` to the component type that implements it.

```csharp
PassiveEventSourceKind.MovementDistance => typeof(MovementDistanceEventSource)
```

When adding a new source kind, update this registry.

## Triggered Passive Rule Contract

`TriggeredPassiveRule` has optional event source fields:

```csharp
public PassiveEventSourceKind eventSourceKind = PassiveEventSourceKind.None;
public string eventSourceId;
public float eventSourceFloatValue = 2f;
public int eventSourceIntValue;
```

Rules:

- `eventSourceKind == None` means the rule uses a normal gameplay event and needs no optional source.
- `eventSourceKind != None` means `PassiveController` must activate a matching optional source.
- `eventSourceId` may be left empty. The rule generates a stable runtime id from kind, trigger, float value, and int value.
- Matching optional-source rules must compare `context.EventSourceId` with `rule.RuntimeEventSourceId`.

This prevents different configs from triggering each other. For example, a 2-meter movement passive and a 5-meter movement passive must not share the same event route.

## PassiveController Responsibilities

`PassiveController` is the orchestrator.

On passive loadout refresh:

1. Rebuild always-on, triggered, custom, and provider passives.
2. Scan triggered rules for `eventSourceKind != None`.
3. Create `PassiveEventSourceRequest` entries.
4. Group requests by `PassiveEventSourceKind`.
5. Find an existing matching source on the character, or add one through `PassiveEventSourceRegistry`.
6. Call `ApplyRequests(...)` on required sources.
7. Call `ClearRequests()` on unused sources.
8. Destroy only sources that were auto-added by `PassiveController`.

Manual source components placed on prefabs are never destroyed by the passive system. They are only cleared/deactivated when unused.

## Current Source: Movement Distance

`MovementDistanceEventSource` is the first optional source.

Behavior:

- Tracks the character root position.
- Counts planar XZ distance only.
- Publishes an event each time accumulated distance reaches the configured step.
- Supports multiple simultaneous requests, such as 2 meters and 5 meters.
- Uses `PassiveEventContext.Value` for the distance step.
- Uses `PassiveEventContext.EventSourceId` for request routing.

Example passive rule:

```text
trigger = MovementDistanceReached
eventSourceKind = MovementDistance
eventSourceFloatValue = 2
eventSourceId = empty
```

This means the passive rule fires whenever this character moves another 2 meters.

## Adding A New Optional Source

1. Add a value to `PassiveEventSourceKind`.
2. Add a `PassiveEventType` if the source needs a new event type.
3. Create a new class that derives from `PassiveEventSource`.
4. Implement `ApplyRequests(...)` and `ClearRequests()`.
5. Publish events through `CombatEventBus`.
6. Set `EventSourceId` on every published event.
7. Register the source type in `PassiveEventSourceRegistry`.
8. Configure triggered passive rules with the new `eventSourceKind`.

## Design Rules

- Do not move existing core gameplay events into optional sources unless the event truly requires monitoring.
- Do not add optional event source references to `CharacteContext` unless a future design needs a shared context reference.
- Do not solve Unity `.csproj` discovery issues by merging new classes into existing files.
- Optional sources should be cheap when inactive and should not tick when no rule requests them.
- Source-specific config belongs in `TriggeredPassiveRule` request fields until a source needs a dedicated serializable config type.

## Stat Dirty Flow

Optional event sources may trigger passives that change runtime stat modifiers.

If an optional source, custom passive behavior, or modifier provider changes the
stat values it exposes, it should notify the stat pipeline through
`IStatModifierProvider.StatModifiersChanged`. `StatsHub` subscribes to provider
events and also probes modifier signatures before returning cached values, so
missed notifications should not leave weapon stats permanently stale.

Do not rely on `WeaponSystem` refreshing all derived stats every frame.

See `WEAPON_SYSTEM_STATS_REFRESH.md` for the dirty flow contract used by weapon stats.
