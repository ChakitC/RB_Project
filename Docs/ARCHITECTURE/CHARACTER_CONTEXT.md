# Character Context Architecture

`CharacteContext` is the shared base reference hub for character actors. The
class name is intentionally spelled `CharacteContext`; keep using that name
until there is a dedicated rename task.

## Main Types

- `CharacteContext`: shared base for player, ally, and enemy characters.
- `PlayerContext`: player-only references such as input, inventory, helper
  manager, field ally manager, chain attack coordinator, party command, and UI.
- `AllyContext`: ally-only references such as `AITargetSensor`, `NavMeshAgent`,
  and `AgentMoveDriver`.
- `EnemyContext`: enemy-only references such as enemy `NavMeshAgent`,
  `StaggerMeter`, collider, dropper, animator, and forced infinite reserve ammo
  setting.
- `CharactorContext.cs`: legacy alias only. Do not add new logic there.

## Base Context Responsibilities

`CharacteContext` owns references that can be shared by multiple character
types, including:

- core state and stat systems: `StateHub`, `StatsHub`, `CombatEventBus`
- movement bodies: `Rigidbody`, `CharacterController` (`cc`)
- common modules: `TargetInfo`, `CharacterEquipment`, `AccessoryLoadout`,
  `WeaponSystem`, `CharacterAnimBrain`, `CharacterAnimDriver`,
  `CharacterPairOffsetApplier`, `MeleeController`
- visual and collider support: `CharacterContextPartyLoader`,
  `CharacterVisualController`, `CharacterVisibilityController` (`Visibility`),
  `UIManager`, `CharacterColliderRefs`
- gameplay modules: `StatusEffectController` (`StatusEffects`),
  `LevelSystem`, `HealthSystem`, `StaminaSystem`,
  `DashSystem`, `CharacterKnockbackMotor`, `CharacterVerticalMotor`,
  `PassiveController`, `SkillUserSystem`, `Interactor`, `CharacterSkillManager`

Common systems should primarily depend on `CharacteContext` and read peer
modules through `ctx`.

## Enabled-lifetime Identity And Registry

Every `CharacteContext` increments `LifeGeneration` in the base `OnEnable` and registers with
`CharacterContextRegistry`; base `OnDisable` unregisters it. `EnemyContext` overrides the disable
hook only to call `base.OnDisable()` before restoring world-slow values. New subtype lifecycle
overrides must do the same. The registry is cleared at subsystem registration and reconciled once
after scene load, which supports pools, scene changes, and Enter Play Mode without domain reload;
it is not a licence to run scene discovery every frame.

Delayed combat work must pair a character reference with its `LifeGeneration` (normally through
`SkillTargetHandle`). A Unity instance ID and Transform are insufficient because a pool can disable
and re-enable the same object between two animation events. Code must validate the original life
before warp, facing, impact, or reservation completion and must not mutate the modules belonging to
the new life.

### `cc` Is Optional

`ctx.cc` is **required by the current Player movement stack only**.
`PlayerMovementCC`, `DashSystem`, and `RootMotionCCDriver` all move the Player
through `CharacterController.Move()`, so removing it from `Player.prefab` would
be a locomotion rewrite. Ally prefabs still carry one as well.

Enemy prefabs have **no `CharacterController`**. Their navigation is owned by
`NavMeshAgent` / `AgentMoveDriver`, and collision-aware displacement resolves
through `ctx.ColliderRefs.CharacterPositionCollider` instead (see
`CharacterBodySweepUtility`). Their runtime animation movement is likewise owned
by `RootMotionNavMeshDriver`; `CharacterVisualController` must not bind an Enemy
model to `RootMotionCCDriver`.

Shared character code must therefore treat `ctx.cc` as an optional, null-safe
optimisation branch. A failed `ctx.cc` lookup must never gate gameplay: do not
write `if (ctx.cc == null) return;` in code that also runs on enemies. Where a
body shape is needed, read `ctx.ColliderRefs.CharacterPositionCollider` and fall
back to `ctx.cc`, never the reverse, and never search the hierarchy for "some
`CapsuleCollider`" — on several characters that returns a hit-zone capsule.

### `Visibility` Is Rebound Per Model

`ctx.Visibility` (`CharacterVisibilityController`) lives on the character root and
owns "is this character visible right now". Gameplay asks in gameplay words —
`ConcealForTeleport` / `RevealAfterTeleport`, `Appear` / `Disappear`,
`SetHiddenImmediate` / `SetVisibleImmediate` — and the controller animates the
dither alpha on the model's `ZLZ_CharacterVFX`.

Two rules follow from where the state actually lives:

- **The renderer state belongs to the model, not the character.**
  `CharacterVisualController` calls `Visibility.BindVisual(currentModel)` after
  every model build, existing-model bind, form override, and form restore. A
  controller left bound to a destroyed model writes into nothing, and the new
  model comes up visible regardless of what gameplay believes.
- **The controller never disables the actor.** It raises `Disappeared` when a
  fade-out completes and the sequence owner (`AllyHelperManager`,
  `FieldAllyTransitionController`) does the `SetActive(false)` and any protection
  rollback from there.

`ZLZ_CharacterVFX` resets its dither to visible in `Awake` and `OnDisable`, and
`Dither.SetInstant` is a silent no-op while Dither is disabled on the component.
The controller therefore re-pushes the desired value for a couple of `LateUpdate`s
after a bind or re-activation, and warns once (never per frame) when the bound
visual has no `ZLZ_CharacterVFX` or has Dither switched off.

Animation commands resolve through `ctx.AnimDriver`. Direct `ctx.AnimBrain`
access is reserved for state queries, sampling, and event subscriptions. See
`Docs/ARCHITECTURE/ANIMATION_COMMAND_FLOW.md`.

## Identity And Time

Use context properties instead of subtype checks for common behavior:

- `ctx.TargetIdentity` identifies player, companion, enemy, neutral, or generic
  AI target identity.
- `ctx.UsesWorldSlow` controls whether systems should use world-slow time.
  Returns `false` when the context has an active world-slow exemption (see
  below) or when the subtype overrides it (e.g. `PlayerContext`).
- `ctx.PushWorldSlowExemption()` / `ctx.PopWorldSlowExemption()` temporarily
  make `UsesWorldSlow` return `false` for any character type. Used by
  `CutsceneSkillPresenter` so the cutscene caster's own animation and agent
  run at normal speed while the rest of the world is slowed. Calls are
  ref-counted and safe to nest.
- `ctx.ForceInfiniteReserveAmmo` allows subtype-specific ammo policy without
  hard-coding enemy checks in common weapon logic.

Do not use `ctx is PlayerContext` in common code just to branch identity or
world-slow behavior.

## Reference Resolution Contract

`ResolveReferences()` is the main binding point.

Rules:

- Shared references belong in `CharacteContext.ResolveReferences()`.
- Subtype-only references belong in that subtype override.
- Every subtype override must call `base.ResolveReferences()` first.
- Modules should use `ctx` as the primary entry point.
- Prefer `ctx.StatsHub`, `ctx.HealthSystem`, `ctx.WeaponSystem`,
  `ctx.CombatEventBus`, and similar context-owned references over duplicated
  serialized peer references.

`ResolveActorComponent()` resolves in this order:

1. keep the current reference if assigned
2. component on the same object
3. child component when allowed
4. parent component

## Actor Boundary Invariant

**A character module is always at or below its actor's `CharacteContext`.**
A context is therefore always **self or an ancestor** of the module asking for it.

This is enforced, not assumed. `CharacterContextModuleLookup.ResolveContext` walks self then
ancestors (with `includeInactive: true`) and returns `null` if it finds nothing. It never searches
downwards and never sweeps the scene tree.

Why it has to be that way: party members live side by side under one `PartyRuntimeRoot`, created by
`PartySpawnPoint.TrySpawnNow`. A downward or root-level search from one actor reaches its
neighbours, and returning "the first match" there means silently handing back a different
character's context, bus, or status controller. That is worse than returning nothing, because the
caller has no way to tell.

`includeInactive` is required throughout for the same reason it is easy to forget: actors are
routinely inactive. The helper is hidden between summons, and the entire party is built under a
root that is activated only after binding finishes. A hidden actor is still that actor.

Consequences for authoring:

- A prefab that places a `CombatEventBus` or `StatusEffectController` outside its own context's
  subtree is an **authoring error**. Every production actor prefab currently satisfies the
  invariant. Do not "fix" such a prefab by widening the lookup.
- Sibling branches *within* one actor are fine and supported — the combat bus lives on
  `GamePlayStats_System`, a child branch, and resolves through
  `context.GetComponentInChildren<T>(true)`.
- An object that merely *contains* an actor — a spawn point, a formation node, a pooling container,
  the party root — is not that actor and resolves to `null`.

History: see `CharacterContextCrossBind_Handoff.md` at the repo root for the bug that produced this
rule and the evidence behind it.


## Adding A Shared Module

When adding a component/reference used by multiple character types:

1. Add a field to `CharacteContext`.
2. Bind it in `CharacteContext.ResolveReferences()`.
3. Update prefab context blocks when useful.
4. Read the component through `ctx` in runtime systems.
5. Avoid adding duplicate serialized references to every module unless the field
   is local authoring data.

## Adding A Subtype Module

When adding a player-, ally-, or enemy-only component:

1. Add it to `PlayerContext`, `AllyContext`, or `EnemyContext`.
2. Bind it in that subtype's `ResolveReferences()` override.
3. Call `base.ResolveReferences()` first.
4. Keep common systems typed against `CharacteContext` unless they truly need
   subtype-specific fields.

## Good Patterns

Use this style in common systems:

```csharp
ctx?.ResolveReferences();
var statsHub = ctx != null ? ctx.StatsHub : null;
var combatEventBus = ctx != null ? ctx.CombatEventBus : null;
```

Use subtype references only when required:

```csharp
if (ctx is AllyContext allyContext)
{
    AITargetSensor sensor = allyContext.AITargetSensor;
}
```

## PlayerContext Static Access

`PlayerContext` exposes a static `Instance` property for secondary systems that
need a reference to the player context but live outside the player prefab
hierarchy (e.g. camera, UI overlays, mini-map).

```csharp
PlayerContext ctx = PlayerContext.Instance; // null if no player is alive
```

`Instance` is set in `PlayerContext.Awake` and cleared in `PlayerContext.OnDestroy`.
It is safe to read every frame; null-check before use.

`PlayerContext.Targeting` is a player-only reference to `PlayerTargetingController`. Common
character modules do not own or depend on it. Aim-derived player commands read this reference;
AI actors continue to use `AllyContext.AITargetSensor` or their graph-provided explicit target.

**When to use `PlayerContext.Instance`:**

- The calling component is not on the player prefab (camera, world UI, etc.).
- Only one player exists in the scene.
- No manual serialized reference is needed.

**When NOT to use it:**

- Inside character modules that already have `ctx` available — read through
  `ctx` directly.
- In systems that run on ally or enemy objects — use their own context.
- Any future multi-player scenario where `Get(index)` on a registry would
  be needed instead.

---

## Avoid

- Declaring another `public class PlayerContext`.
- Adding new logic to `CharactorContext.cs`.
- Branching common identity behavior with `ctx is PlayerContext`.
- Removing serialized prefab-facing fields without confirming runtime resolution
  and prefab usage.
- Moving newly created Unity classes into unrelated files to work around stale
  `.csproj` generation.

## Transient Summon Context

`SummonContext` is a `CharacteContext` subtype for transient spawned actors. It
uses companion targeting identity but overrides the capability flags for
persistent progression, persistent loadouts, party runtime participation,
pickup collection, aim-rig creation, and runtime equipment creation to false.
Shared modules should continue to consume the resolved `CharacteContext` seams;
summon-specific ownership and lifetime belong to `SummonedEntityRuntime` and
`SummonController`.
