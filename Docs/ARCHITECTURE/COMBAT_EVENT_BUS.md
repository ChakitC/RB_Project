# Combat Event Bus

`CombatEventBus` is the local event bus used by combat and passive systems on a
character actor. It publishes `PassiveEventContext` values and lets
`PassiveController` evaluate triggered and custom passives without direct
coupling to weapon, projectile, melee, dash, or health systems.

## Main Types

- `CombatEventBus`: owns event publication for one character actor.
- `PassiveEventContext`: immutable event payload.
- `PassiveEventType`: event category such as `ShotFired`, `Hit`, `Kill`,
  `TakeDamage`, `Reload`, `DashStarted`, `DashEnded`, or
  `MovementDistanceReached`.
- `PassiveEventOrigin`: source origin such as `External`, `Passive`,
  `StatusEffect`, or `System`.
- `PassiveController`: subscribes to the bus and evaluates passive rules.

## Event Context Fields

`PassiveEventContext` carries:

- `Type`: event category.
- `Actor`: owning actor object.
- `Source`: object that produced the event.
- `Target`: optional target object.
- `SourceId`: stable source identifier used by passive rules and optional event
  sources.
- `AttackId`: id used to group one attack or shot.
- `Value`: event-specific numeric value.
- `Time`: event time.
- `ChainId`: id shared by related parent and child events.
- `Depth`: child-event depth guard.
- `Origin`: external, passive, status effect, or system.
- `OriginPassiveId` and `OriginRuleId`: passive provenance for child events.

## Core Event Flow

Core gameplay events should be published directly by the system that owns the
behavior:

- Weapon firing publishes `ShotFired`.
- Projectile, melee, and skill hit logic publish hit and kill events after
  damage is actually applied.
- Health and damage logic publish damage-related events.
- Reload logic publishes reload events.
- Dash logic publishes dash and perfect dodge events.

These events should not be routed through optional `PassiveEventSource`
components. Optional sources are only for monitoring or polling style events.

## Damage Application

`IDamageable.TakeDamage` returns a `DamageResult`. Hit publishers must use that
result instead of the precomputed final damage when deciding whether to show
damage numbers or publish `Hit` and `Kill`.

`Hit` means `DamageResult.AppliedDamage > 0`; collider contact alone is not a
combat hit. Invincible or otherwise prevented damage should publish
damage-prevented events from the health system, but should not trigger owner
`Hit` or `Kill` passives.

## Passive Flow

1. A gameplay system creates a context with `CombatEventBus.CreateExternalContext`.
2. The system calls `CombatEventBus.Publish`.
3. `PassiveController.HandlePassiveEvent` receives the event.
4. Triggered passive rules compare event type, origin filter, source id, target
   requirement, attack id requirement, cooldown, counters, and chain execution.
5. Matching rules execute actions.
6. Actions may grant runtime modifiers, apply status effects, or emit child
   passive events.
7. Child events use `CreateChildContext` and increment `Depth`.

`PassiveController` has a depth limit to avoid recursive passive loops.

## SourceId And AttackId

Use `SourceId` when a passive rule needs to distinguish event sources. Examples:

- weapon source id
- affix source id
- optional event source id
- passive action source id

Use `AttackId` when multiple events belong to the same shot or attack and a rule
needs once-per-attack behavior.

## Publishing Guidelines

When adding a new event publisher:

1. Resolve `CombatEventBus` through `ctx.CombatEventBus` when possible.
2. Create a `PassiveEventContext`.
3. Set a stable `SourceId` if rules must match the source.
4. Set `Target` when the event is target-specific.
5. Set `AttackId` for shot/attack chains.
6. Publish through `CombatEventBus.Publish`.
7. Do not call passive rule code directly from the publisher.

## Adding Event Types

When adding a new event:

1. Add a value to `PassiveEventType`.
2. Publish it from the system that owns the behavior.
3. Update passive authoring docs and any editor tooling that lists event types.
4. If the event requires monitoring/polling, add an optional event source instead
   of ticking every passive directly.
