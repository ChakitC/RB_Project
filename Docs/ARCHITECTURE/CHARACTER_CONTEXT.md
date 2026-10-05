# Character Context Architecture

## Crouch and Slide locomotion queries

`CharacteContext.IsCrouching`, `IsSliding`, `CancelSlide()`, `CanStandForDash()` and
`PrepareForAcceptedDash()` default to false/no-op/allowed for non-player actors.
PlayerContext delegates to its existing movement binding. PlayerMovementCC owns
input, shape snapshots and momentum; no new peer component is required. Dash checks
standing clearance before spending stamina and prepares locomotion after success.
`CancelSprint()` deliberately does not cancel Slide, so aim/fire preserve it.
StateHub projects appended Crouch/Slide move IDs and passes read-only flags through
CharacterAnimDriver to the Brain. Reference resolution and actor ownership remain unchanged.

## Sprint locomotion query

`CharacteContext.IsSprinting` and `CancelSprint()` provide the shared locomotion
query/cancellation contract. Their defaults are false/no-op. PlayerContext
delegates to its existing `movement` binding; PlayerMovementCC owns all Sprint
state. StateHub projects `MoveStateId.Sprint` (appended to preserve existing enum
values) and exposes the read-only query to CharacterAnimDriver. Combat requests
cancel Sprint before normal action admission. No extra peer lookup or prefab
component is required. See `CHARACTER_MOVEMENT_REFACTOR_PLAN.md` for controls.

`CharacteContext` is the shared base reference hub for character actors. The
class name is intentionally spelled `CharacteContext`; keep using that name
until there is a dedicated rename task.


## Shared AI owner binding

BehaviorTree custom Tasks receive the owner through Graph variable `Ctx`
(`SharedVariable<CharacteContext>`, default null). `InitializeAIContext` prepares
references before behavior, once per real tree start/enabled lifetime; consumers
only read the shared binding with `AIContextBinding`. No consumer resolves owner
or peer components through the hierarchy. Each actor owns its graph runtime value.

`AITargetSensor` and `AgentMoveDriver` are declared on this base, with their existing
serialized names/types preserved from Ally/Enemy/Summon. `AimAxisDriver` is a common
optional reference. `ResolveReferences` resolves these peers within owner boundaries
and refuses ambiguous candidates unless an explicit binding is supplied. Sensor
bootstrap lookup reads the common binding without recursive reference resolution.
Missing optional peers are valid at initialization; Tasks fail only when their own
required module is missing. Ally interruption and Enemy SpecialShootPoints remain
subtype-specific and are read by casting Ctx.

Replacing a module requires updating its context binding. Event consumers compare
that binding and detach the old module before attaching the replacement. Active
Actions capture their execution generation and do not clean up a newer pooled life.
See `SYSTEMS/AI_AND_TARGETING.md` and `PREFABS_AND_AUTHORING.md` for authoring.

## Animation binding

`CharacterAnimationBinding` reads the existing `CharacteContext` hub and supplies
cached reference snapshots to the Brain's presentation/timeline modules. Common
modules still resolve through `ctx`; no new Player/Ally/Enemy-specific lookup or
prefab component is required. The normal frame path compares references rather
than resolving the hierarchy again.

After rebuilding/replacing a model, call
`ctx.AnimDriver.InvalidateAnimationBinding()`. Explicit invalidation rebuilds even
when the Animator/profile references are unchanged. A missing/destroyed model
releases its old sessions; a later valid model can initialize again. Existing
root/nested context layouts retain their fallbacks. If several Animancer components
are candidates, assign the actor's component explicitly on Brain.

## Main Types

- `CharacteContext`: shared base for player, ally, and enemy characters.
- `PlayerContext`: player-only references such as input, inventory, helper
  manager, field ally manager, chain attack coordinator, party command, and UI.
- `AllyContext`: ally-only BehaviorTree, AimTargetDriver and InterruptionController references.
- `EnemyContext`: enemy-only references such as enemy `NavMeshAgent`,
  `StaggerMeter`, collider, dropper, animator, and forced infinite reserve ammo
  setting.
- `CharactorContext.cs`: legacy alias only. Do not add new logic there.

## Base Context Responsibilities

`CharacteContext` owns references that can be shared by multiple character
types, including:

- core state and stat systems: `StateHub`, `StatsHub`, `CombatEventBus`
- movement bodies: `Rigidbody`, `CharacterController` (`cc`), `NavigationAgent`
- shared displacement and navigation ownership: `MovementMotor` (`CharacterMovementMotor`)
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

`ctx.Visibility` (`CharacterVisibilityController`) lives within the character's
context subtree (Player uses `CharacterVisual_System`) and
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

## AI target identity and lifetime

AI perception reads the shared active-context registry. `AITargetHandle` captures
context identity plus `LifeGeneration`; a common hierarchy root is not an actor
identity. Health, state and target metadata are read through the target context.
`AITargetSnapshot` separates the selected actor, aim position, real sight and last
observation. Target selection never requires the actor to have a collider.

`StatusEffectInstance.SourceContext` and `SourceLifeGeneration` capture the source
life at application/refresh. The AI sensor uses these values for Taunt so an old
status cannot track a recycled source. The status system does not depend on AI
handle types. Existing shared and subtype reference ownership stays unchanged.

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

### Shared movement motor

`ResolveReferences()` resolves `NavigationAgent` through the normal actor lookup.
At runtime it calls `EnsureMovementMotor()`, the single character-owned creation
point. Explicit initialization and tests can also call that method. Edit Mode
reference inspection and `CharacterMovementMotor.Resolve` only look up existing
components; reading suspension state never creates one. Binding the motor does
not call back into `ResolveReferences()`.

Character movement calls `ctx.MovementMotor.Move(delta, settings)`. The motor
reads the live controller, body collider and agent from its context, so replacing
the model/body does not require passing peer references on each move. Root-motion
adapters call `Configure` during initialization/rebind for explicit actor roots
and standalone authoring. A later context refresh preserves that root binding.
Context-free standalone setups must provide a motor component explicitly.
`Move(delta, settings)` and `PlaceOnNavMesh(position)` use only the bound actor
and body; callers configure those references once before submitting movement.

Walking, dash, both root-motion adapters, knockback, gravity, companion separation
and defensive approach submit displacement to this motor. It returns actual
movement after collisions and synchronizes the agent. Behaviour modules retain
their timing, admission rules, collision settings and animation/combat decisions.
An enabled child controller moves the whole actor while keeping its local offset.

The motor belongs to the context, so replacing a visual model does not replace its
navigation baseline. The existing subtype agent fields remain supported.
Ordinary NavMesh path following still uses NavMeshAgent.

Internally the motor separates controller movement, body-shape resolution and
body sweeps. Navigation suspension remains in `CharacterNavigationSuspension`.

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

`PlayerContext.inputHandler` and `interruptionController` resolve the player's
input and interruption modules across child groups. Chain autonomy reads the
handler and movement through this hub; party commands read the interruption
binding. They must not depend on sharing a GameObject or ancestor with those
modules. See `PREFABS_AND_AUTHORING.md` for the six Player system groups.

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

## StateHub ownership and initialization

StateHub exposes MoveSM, WeaponSM, LifeSM and UISM as IReadOnlyState<TId>.
Each view is a separate object with CurrentId and OnChanged only. It cannot be
cast back to the writable StateValue or StateMachine. StateMachine.CurrentId
also has a private setter. Consumers change state through owner commands.

| State | Owner and commands | Projection order |
| --- | --- | --- |
| Weapon | WeaponSystem: Equip, SetFiring, TryShoot, TryReload, CancelReload, ammo restoration | Melee, Reloading, NoBullet, Firing, Ready |
| Life | HealthSystem: damage depletion, Down, Die, Revive | Explicit owner LifeState |
| Movement | DashSystem and CharacterKnockbackMotor activity, status locks, movement intent | Knockback, Stunned, Dash, Moving/Stand |
| UI | Each screen holds its own AcquireUIStateToken/ReleaseUIStateToken | Pause, Inventory, Normal |

MeleeController owns combo activity. Its ActivityChanged event and the weapon,
health, dash and knockback owner events update the projections synchronously.
Admission reads owners directly, including reload state before the next Update.
StateHub.Update only reconciles frame-dependent movement/playback and resumes
held-fire intent. No gameplay admission decision reads the four projections.

PlayerContext, AllyContext, EnemyContext and SummonContext call
PrepareRuntimeModules once during Awake. It resolves peers, supplies the legacy
AudioEmitter/MeleeController defaults, and supplies StateHubDebugView when an
unmigrated runtime rig needs it. Admission queries never create components or
resolve references. ResolveReferences in Edit Mode does not add components.
Optional modules block only actions that require them.

Explicit peer bindings remain authoritative. Missing StateHub-related peers are
resolved from the actor hierarchy only when a single candidate belongs to the
actor. Ambiguous candidates produce a configuration error requiring a binding;
parent peers require an unambiguous actor subtree. Deliberate relationships to
another context must be explicitly bound.

Control flags are composed by CharacterControlLocks. StatusEffects, Stagger,
Reaction and Legacy have independent channels. External owners acquire tokens
with a debug owner name and release only their own token. Token 0 is inert and
unknown/duplicate releases are no-ops. CutsceneSkillPresenter uses its own token.
Tokens survive StateHub disable/enable; input intents and transient actions do
not. Enable subscribes once and reconciles owner snapshots without reviving or
restarting input. Before Awake, tokens and fire intent may be recorded, while
all Can* queries reject actions. Owners releasing holds tolerate destroyed hubs.

### Admission rules retained from the previous implementation

CharacterActionPolicy evaluates a CharacterActionFacts value without side effects.
ActionAdmissionResult reports the first rejection reason. Module-specific stamina,
fire cadence, combo windows and ammo transactions remain in their owners.

| Action | Shared restrictions and exceptions |
| --- | --- |
| Move | Alive or Down; normal UI; no Move lock, stun, melee, dash or knockback; FullBody reload blocks, UpperBody does not |
| Dash | Alive; normal UI; no Move lock, stun, melee, dash or knockback; reload is allowed; only a successful dash cancels FullBody reload |
| Fire request | Alive; normal UI; no Shoot lock, stun, melee, dash, knockback or blocking playback; fresh requests may cancel configured reload with loaded ammo |
| Shoot | Same restrictions, always rejects live reload and empty magazine; WeaponSystem additionally checks weapon, cadence and launch references |
| Reload | Alive; no blocking playback or knockback; historical menu/root/stun behavior remains; owner checks reserve/capacity |
| Skill | Alive; normal UI; no Skill lock, stun, melee, dash or knockback |
| Melee | Alive; normal UI; no stun, dash, knockback or skill playback; Skill-only lock does not block basic melee; owner checks reload interrupt configuration |
| Rotate | No stun or Rotate lock; historical life/UI freedom remains |

Initialized, enabled admission and required module availability are prerequisites.
A held-fire resumption cannot cancel reload. A new request may cancel it and
fire a Semi round in that same command.
