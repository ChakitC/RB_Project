# Passive System

## Stat scope and stacks

Passive stat entries and dynamic modifier steps expose `Global` and `WeaponOnly`.
Global is the serialized default and affects the combined character/weapon stat;
WeaponOnly affects only the gun contribution before character base and Global
bonuses are applied. WeaponOnly supports Damage, CritRate, CritMultiplier,
FireInterval, ReloadTime, ReloadSpeed, Stability, BulletSpeed, MaxMagazine and
MaxReserveAmmo. Character-only and skill-only stats must remain Global.

Weapon grants forward `PassiveGrantOptions.WeaponInstanceId` through equipped,
dynamic, temporary and status modifiers; snapshots and previews preserve it.
Item/character grants can leave it empty to affect the equipped gun. Unsupported
scope/stat pairs are rejected by recipe validation. Dynamic expressions already
resolve their own variable counts and receive no additional stack multiplication.

One active stack group uses `value*stacks` for Flat/AddPercent and
`max(0, 1+(value-1)*stacks)` for Multiply. Thus x1.2 at three stacks is x1.6,
x0.8 at three stacks is x0.4, and excessive reductions clamp the factor to zero.
No modifier is emitted at zero stacks. Distinct/Independent groups still multiply;
refresh, replacement, expiry and maximum-stack policies retain their meanings.
Status effects and weapon upgrade steps share this rule. Status strongest-only
comparison uses the same stack math and includes scope in modifier shape.

Lobby accessory passive previews read unconditional Equipped recipe steps through
`PassiveStatPreview`; legacy AlwaysOn fields are not another calculation path.
Triggered, timed and conditional combat buffs are absent from an idle preview.

Runtime status producers must call `SetModifierOverride` / `SetDurationOverride`
when overriding a StatusApplicationSpec channel. Merely assigning its public value
does not enable the override; `Clone` preserves the flags and weapon binding.

PassiveDefinition keeps identity, icon, description and upgrade tree, and points to a
PassiveRecipe. All installed passives execute through PassiveRuntime. AlwaysOn,
Triggered and Custom definitions remain asset wrappers for serialized reference
compatibility; their legacy fields are migration input, not a second runtime.

## Recipes and reusable fragments

A recipe contains stable-ID rules. Each rule selects Combat, Equipped,
ResourceSpent or ResourceChanged, conditions, gates and an ordered step list.
Weapon grants also use BeforeShot, ShotCommitted and BeforeDamage. These are
explicit preparation/commit hooks, not synthetic combat facts.
Lists execute as sequences without rolling back completed effects. A
PassiveBranchStep selects its true/false list; LastSucceeded branches on the
immediately preceding result. PassiveFragmentStep invokes a reusable
PassiveFragment; fragments never subscribe to events.

Recipe/fragment steps use SerializeReference. Keep IDs stable when reordering.
A fragment declares Number/Status parameters and optional local variables.
Overrides are scoped to each invocation; numeric expressions resolve defaults,
invocation overrides, numeric upgrade flags and then their clamp. Assets are not
modified while executing. Variable bindings explicitly share a fragment variable
with its parent; otherwise each invocation has its own state.
Recipes can declare root parameters. PassiveGrantOptions freezes numeric roll
overrides at installation; fragment parameterBindings explicitly map parent
parameters to local names. Preview resolves the same PassiveNumber expressions.

PassiveRecipeValidator rejects missing recipes/fragments, duplicate IDs in a scope,
unknown parameters, mismatched override types, undeclared variables and fragment
cycles. Persistent registrations require Equipped. The initial conversion adapter
supports StaminaRegenRate -> MaxStamina; other routes, including conversion chains,
are rejected.

Current pieces:

| Purpose | Types |
| --- | --- |
| Conditions | All / Any / Not, health or stamina ratio, status/stacks, upgrade ID, source/provenance, event type, previous success |
| Gates | count/window, reset/carry-over, cooldown, chance, once per attack or Dash |
| Targets | owner, event target, around owner/target using CharacterContextRegistry and TargetIdentity |
| Effects | stat modifier, status, healing, stamina restore, magazine restore, pickup, PassiveSignal |
| State | accumulator add/subtract/set with clamp, dynamic modifier, conversion, next-attack modifier |

Add a new reusable behavior as a PassiveStep subtype with its own Execute method,
then compose it in data. Do not add behavior switches or character-specific fields
to PassiveController or CharacteContext. Persistent registration steps opt into
RunOnRefresh; ordinary Equipped effects run once when a grant is installed.

## Installation identity and lifecycle

WeaponAffixDefinition.passiveDefinition supplies the same runtime as character
and accessory grants. WeaponAffixRuntimeController is an IPassiveGrantProvider
adapter; it has no combat subscription, timer loop, or behavior runtime. The old
ConfiguredWeaponAffixBehavior fields remain migration/reference data; calling its
CreateRuntime is unsupported. Rolling, equip rules, ammo and saving remain owned
by the equipment systems.

For legacy weapon actors without the shared modules, the adapter installs one
PassiveController only after a valid affix grant is equipped. It creates a
CombatEventBus only for recipes with combat/weapon phases and refreshes the weapon's
context-bound bus reference. A static-only installation needs no combat module.
After the last grant is removed the empty controller has no subscription or timer.
Optional step success/expiry signals and committed reservation signals forward to
the equipment feedback API; cancelled preparations do not emit Consumed feedback.

RolledAffixData.grantId identifies each installation independently of affixId.
New rolls get IDs; old saves receive stable slot-derived IDs once. Legacy progress
is claimed by the first matching grant, never copied to duplicate grants.
Only variables explicitly marked persistent use IPassiveStateStore; the weapon
adapter stores them lazily in WeaponInstanceData.affixRuntimeStates. Schema 1
integer counters upgrade to schema 2 numeric entries on write. DeepClone preserves
identities and copies entries. Static affixes create no persistent record.

Transient charges/stacks/target memories/expiry deadlines belong to the grant and
disappear on unequip. PassiveExpiryStep resets its declared variables; unlike an
independent timed buff it does not outlive the grant. Existing weapon expiry uses
scaled game time; ordinary character passive clocks continue to use UsesWorldSlow.
PassiveTrackTargetStep keeps the context plus LifeGeneration, scoped to the
fragment invocation. Pool reuse cannot inherit a target mark.

BeforeShot permits reversible shot patches and variable reservations. The weapon
commits only after the primary projectile spawns; cancellation refunds reserved
charges unless their expiry/lifetime has ended. ShotCommitted runs persistent
counters and extra projectile effects. Prepared damage and impact payloads stay
with the projectile after unequip. Generated shots carry WeaponAffix provenance,
retain the parent chain, and use captured ordinary damage without Roma Momentum
or Fresh Chamber. BeforeDamage applies current target-sensitive grant modifiers;
its adapter checks the source weapon and owner lifetime.

PassiveStatPreview projects unconditional Equipped stat registrations without a
PassiveRuntime or state store. Conditional/timed effects are described by authored
tooltip data rather than shown as permanent weapon totals. Weapon definitions own
rollSpec/tooltip/eligibility data; equipment authoring no longer requires a runtime
behavior class for each affix.

WeaponPassiveRecipeMigration.MigrateAll(true) validates a dry run;
MigrateAll(false) installs subassets while retaining definition GUIDs/references.
It is repeatable and preserves already-authored recipes. The 27 original affixes
now have composed recipes. New behavior belongs in reusable step/condition types,
not in a switch inside the equipment adapter or PassiveController.

PassiveGrant contains Definition, SourceId, GrantId and a copied upgrade snapshot.
DefinitionId is the asset GUID, independent of the public passiveId string.
Two Roma assets can retain the same public ID without sharing runtime state.

Skill slots supply slot IDs. Accessory grants use accessory instance IDs plus the
definition occurrence. Runtime/extra lists retain serialized IDs across reordering;
SetRuntimeGrants accepts explicit identities. Legacy definition-provider APIs remain
callable, but new providers should implement IPassiveGrantProvider.

State belongs to owner LifeGeneration -> grant key -> rule/step/invocation path.
Variables are allocated only when read/written. A stat-only grant has modifier
registrations, no counter or timer state. An empty controller has no runtime,
combat/resource subscription or Update method. Its skill-loadout change listener
remains available to activate it later.

Refresh retains an existing grant's counters/variables, rebuilds registrations and
clamps the remaining cooldown to the new duration. Removing a grant immediately
removes its registrations and variables. Applied temporary buffs/statuses continue
under their recipient's lifetime/time rules. Disable clears old state and releases
subscriptions. Saved/queued targets and attack reservations check lifetime validity.

PassiveTicker is one shared scheduler, active only for owners with a pending
cooldown, counting window or timed modifier. Owner timers use UsesWorldSlow.
Timed buffs run on the recipient controller; status time remains owned by Status.

## Event processing

Combat rules are indexed by event type. Resource notifications are subscribed only
for rules or dynamic modifiers that need them. MovementDistanceEventSource is added
only when requested and disabled/removed when no rule needs it. Pure upgrade
conditions can suppress its installation; health/status conditions are evaluated
when an event arrives.

Evaluation order is condition -> count -> cooldown -> chance -> reserve -> steps.
OnAttempt consumes gates before executing; OnSuccess reserves against reentry and
consumes only after a successful result, preserving the migrated custom behavior.
Effects return Succeeded, Skipped or Failed, actual quantities where available, and
successful targets. HealActual / Restore / RestoreMagazineActual obtain quantities
inside the owning system before callbacks can change state.

PassiveDispatch queues only passive work, FIFO. Other CombatEventBus subscribers
remain synchronous. Child resource effects retain the current chain. External is
the default origin filter; receiving Passive or StatusEffect origins is explicit.
Fact and per-rule/per-target chain guards, depth 8 and 1,024 steps bound chains.
PassiveSignal is a distinct enum value; do not synthesize Hit/Kill/StatusApplied.
Diagnostic mode records the last rule outcome/gate reason; budget overflow warns.

StatusApplied describes actual status application independently of the Follow-Up
feature switch. Follow-Up consumers own their feature gate. FactId, ChainId and
FollowUpProvenance remain part of the event contract.

## Stamina, conversion and dynamic damage

StaminaSystem.Spend(float) remains supported; Spend(float, reason) emits
ResourceSpent only for the clamped amount actually paid, with a unique FactId.
Failed/zero/invalid spending, regen, Restore and max-stamina changes do not emit it.
Restore returns the applied amount and leaves the regen delay unchanged.

StatsHub computes ordinary totals first. Conversion reads the positive regen bonus
above StaminaSystem.BaseRegenerationRate from that snapshot, removes the consumed
bonus from regen and adds the converted maximum stamina. Competing conversion
fractions share the available source proportionally; the same bonus is never spent
twice. Rebuilds always start from the ordinary snapshot, so they cannot compound.
Negative bonuses and baseline regen are not converted.

Dynamic registrations read Current/Max directly, never call StatsHub from a
provider, and invalidate via OnStaminaChanged. They reuse their registration key.
Maximum changes preserve the existing stamina ratio and emit only Changed.
No Momentum field is added to the context, stamina system or save data.

## Next normal attack

ReserveNextAttack(attackId) returns null immediately when no grant uses this hook.
It reserves accumulator amounts and captures a multiplier. Commit consumes the
reservation once the normal shot is spawned or normal melee HitStart activates;
Dispose before commit refunds only the reserved amount to the surviving grant.
Amounts earned after reservation remain for the following attack.

Normal weapon shots bake the multiplier after shot preparation/affix modification
and before projectile critical/defense processing. Melee keeps its multiplier and
attack identity on the active step, shared by its hits and defensive-block snapshot.
Projectiles retain their prepared damage even after unequip. A committed miss spends
the bonus. Skills, status ticks, Follow-Up and affix-generated extra shots do not
reserve or inherit this modifier.

## Migration and example data

Tools > RB > Passives > Dry Run Recipe Migration validates without saving.
Migrate Recipes adds a recipe subasset to each legacy definition, retaining its
GUID, name, external references, upgrade tree and serialized legacy input.
Already migrated assets are validated and skipped; rerunning is safe.
All ten existing definitions were migrated. Legacy custom behavior assets retain
their authoring fields but no longer maintain actor dictionaries or execute effects.

Assets/Tests/PassiveComposition/Fixtures/RomaStaminaMomentum.asset is an opt-in test
definition with a recipe and four fragment subassets. It is not assigned to any
production character:

| Fragment | Test values |
| --- | --- |
| Perfect Dash restore | +10 stamina, once per DashExecutionId |
| Regen conversion | +1 regen/second converts to +4 max stamina |
| Missing stamina damage | below 70% remaining, +0.5% damage per 1% missing from full |
| Momentum | actual stamina spend -> charge, cap 100; next normal attack +2% per point |

Momentum is explicitly bound between the spending and equipped rules. These values
are test parameters, not production balance. The four mechanics are composed from
general pieces, without a Roma-specific runtime class.

## Production Roma loadout

Roma now equips `Assets/Character/Roma/Passive/Passive.Roma_StaminaMomentum.asset`
in the trailing `roma.passive.stamina-momentum` skill slot (hotkey None), after
its existing Ultimate. This production asset owns its recipe and all four
fragment subassets; it does not reference the test fixture asset. Its definition
ID is distinct from the example and its public ID is
`passive.roma.stamina-momentum`.

Roma retains Perfect Dash +10 stamina, regen bonus conversion 1:4 and below-70%
missing-stamina damage at 0.5% per missing 1%. Production Momentum now grants
Combat Surge: each actual Stamina payment adds the amount paid, capped at 100.
Partial charge and full readiness do not decay. The next successful normal weapon
shot consumes 100 and activates a five-second buff: +30% reload speed and 50% less
aim movement penalty. Failed shots, melee, Dash, reload, skills and generated shots
do not activate it. Spending during the buff grants no Momentum; firing during it
does not extend it without Keep the Pressure. Expiry resumes accumulation from zero. Removing/disabling the
grant clears its buff and charge; ordinary loadout refresh preserves the deadline.

The recipe uses ShotCommitted, a `surge-active` variable, dynamic Equipped stat
registrations and PassiveExpiryStep. ReloadSpeed is an appended stat with a 1.0
baseline (+30 AddPercent gives 1.3); AimMovementPenaltyReduction is a clamped
0-100 percentage-point stat. StatsHub interpolates the authored aim multiplier
toward 1, so 0.6 becomes 0.8 at 50%. Firing, directional, crouch and sprint movement
modifiers remain independent. WeaponSystem snapshots reload speed on an accepted
reload: all magazine/per-bullet phases and animation duration divide by the same
speed. A reload started during Surge retains that speed until completed/cancelled,
even if Surge expires. No extra damage or fire-rate bonus comes from Momentum.

Tune the production recipe directly; `RomaCombatSurgeAuthoring.Install()` reapplies
these defaults, including the upgrade rules below, while preserving asset identity.
The original next-attack example remains separate regression-test data.

### Roma Combat Surge upgrade tree

`Roma_StaminaMomentum_UpgradeTree.asset` uses tree ID
`roma.passive.stamina-momentum.tree.v1`. Nine nodes cost 12 shared skill points;
each independent column requires its preceding node and costs 1, 1, then 2.
There are no extra level requirements or cross-column exclusions. The passive
works without upgrades. Node IDs and granted flags use `roma.passive.` plus
the suffix below; existing slot/option IDs and definition GUID are unchanged.

| Path | Node suffix | Effect |
| --- | --- | --- |
| Dodge | perfect-recovery | Perfect Dash refund 10 -> 15 Stamina |
| Dodge | stamina-reserve | +10 Max Stamina |
| Dodge | slip-reload | Perfect Dash transfers ceil(25% magazine capacity) from reserve during Surge |
| Continuous fire | fast-hands | Surge reload speed +30% -> +40% |
| Continuous fire | mobile-aim | Surge aim penalty reduction 50% -> 65% |
| Low Stamina | deep-reserve | Regen bonus conversion 1:4 -> 1:5 |
| Low Stamina | desperate-power | Below 70%, damage per missing 1% Stamina 0.5% -> 0.6% |
| Continuous fire | keep-the-pressure | Every five eligible shots add 1 second, maximum +3 seconds |
| Low Stamina | redline | Opening low-Stamina damage bonus remains a floor until Surge ends |

Slip Reload claims each Dash before attempting transfer, including full-magazine
and empty-reserve attempts. Only successful transfers consume its two-per-Surge
quota. It affects the currently held weapon, obeys infinite-reserve rules, and
does not emit Reload or interrupt a running reload. Perfect Dash's Stamina refund
is independent.

Pressure requires an external normal weapon Hit credited to the owner, positive
applied damage and an Enemy target. Melee, skills, status, generated affix shots
and follow-up skill shots are excluded. Each AttackId is remembered for the whole
round, including out-of-order duplicates and piercing targets. Impact time controls
eligibility: a shot fired before Surge can count; an impact at/after the deadline
cannot revive it. Hits 5/10/15 extend the existing deadline, at most eight seconds
total. Counters and identities clear at the start/end of each round.

Redline captures the upgraded damage formula at activation, or zero at >=70%
Stamina. The single dynamic damage modifier uses max(current bonus, captured
floor); it never adds them or records later low-water marks. Pressure extends the
same floor. Expiry, unequip and disable clear it. Prepared projectiles, including
the shot activating Surge, retain their existing damage snapshot.

All round state belongs to the passive runtime. Ordinary loadout refresh preserves
it; changing upgrade flags ends an active Surge and clears its round state, while
idle Momentum survives. `PassiveNumericUpgrade` and `HasUpgrade` drive minor nodes.
`PassiveReserveAmmoStep` returns actual transferred ammo. `PassiveExpiryEventStep`
claims Attack/Dash identities against a live timer, `PassiveExtendExpiryStep` adds
time with a cumulative quota, and `PassiveExpiryStep.cancelOnUpgradeChange` opts a
timer into cancellation. Dynamic modifiers can set `minimumVariable` for a floor.

The HUD keeps its existing gauges. Surge fill is remaining time / total duration
awarded in this round (5 through 8 seconds). Damage displays the actual Redline
modifier. Successful transfers/extensions emit brief `SLIP RELOAD +N AMMO` and
`PRESSURE +Ns` feedback.
## Skill slots and preview

Passives remain SkillDefinitionBase assets in CharacterStats.skillSlots, sharing
the skill-point pool and upgrade trees. A slot's options must all be one kind.
Passive slots remain last and use hotkey None. CharacterSkillManager publishes
PassiveLoadoutChanged; it does not own passive execution.

The existing LobbyCharacterStatPreview does not evaluate composed recipes or
skill-slot passive runtime state. A runtime stat-bearing recipe therefore needs
runtime inspection; the lobby preview limitation is not resolved by this overhaul.

## Validation

PassiveComposition.Tests runs typed fixture cases through the dedicated test
assembly. PassiveComposition.PlayMode.Tests exercises real-frame lifecycle and
timer cleanup. See ../VALIDATION.md for executed results and broader regression
checks. A full authoring UI, node graph, Delay and Repeat remain outside this version.
## Passive HUD ownership

`PassiveDefinition.hudPrefab` optionally references a `PassiveHudView` prefab.
The asset owns only this reference. `PassiveHudHost` creates one view per installed
`PassiveRuntime`/Grant, and binds it to that runtime rather than to the shared
definition. Missing prefab means no view. The host listens to
`PassiveController.RuntimeLoadoutChanged`; it never polls or subscribes to combat.
Refresh preserves existing views; removing a grant, disabling the HUD or changing
owners detaches listeners and destroys its instances. Re-enable recreates bindings.

`PassiveRuntime.TryGetVariable` and `GetNextAttackBonusPercent` read values without
allocating accumulator state. `StateChanged`, `EffectResolved` and `AttackChanged`
are optional presentation notifications scoped to one runtime. Resource effect
results carry actual applied amounts. Attack phases distinguish reservation,
commit and cancellation; the HUD never consumes or restores gameplay resources.
The existing `PassiveTriggered` API remains intact.

Roma's production definition references
`Assets/Character/Roma/Passive/UI/RomaPassiveHUD.prefab`. Its custom view displays
Stamina, Momentum charge/readiness, Combat Surge time remaining and the grant's actual Damage
AddPercent modifiers. The inner ring has a 70% presentation marker; amber follows
the active damage bonus. Perfect Dash feedback displays the actual refund, including
partial refunds near maximum. The two-ring artwork is editable uGUI vector geometry.
The `momentumVariable` field binds the recipe's shared variable ID `momentum`.
There is no gameplay formula or additional accumulator in the view.
During Surge the Momentum ring drains with the grant deadline. `TimerChanged`
and `GetVariableExpiryRemaining` provide presentation updates from PassiveTicker;
the HUD adds no Update loop or independent gameplay clock. Legacy next-attack
bindings remain supported for the original example.

Create another character's view by deriving from `PassiveHudView`, assigning its
local UI references and setting that character passive's `hudPrefab`. Subscribe and
detach through the base lifecycle; check `HasLiveRuntime` before using retained
references. Do not put scene instances or actor references on Definition assets.
