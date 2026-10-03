# Party Follow-Up Skill System

## Naming and compatibility (2026-10-01)

The event-driven Ally command is **Follow-Up** in the UI and `PartyFollowUp`
in code. `Combo Skill` continues to mean the multi-step Basic Melee skill;
`Chain Attack` keeps its existing name and execution path.

- Types, folders, input actions and callbacks now use `PartyFollowUp`.
- The committed combat fact is `FollowUpSkillCommitted`; its serialized enum
  value remains 13. The trigger enum retains value 1 for the same condition.
- Renamed Unity types retain their script GUIDs and carry `MovedFrom` metadata.
  Renamed authoring fields use `FormerlySerializedAs`, including
  `partyFollowUpSkill`, `followUpId`, and the Player's controller/executor bindings.
- The save slot key remains `party-combo`. Existing definition IDs and upgrade
  tree IDs retain their values so saved upgrades still resolve. These legacy
  storage strings are not UI labels.
- Authoring assets are under `Assets/Data/PartyFollowUp`. Use
  **Tools > Validation > Validate Party Follow-Up Skills** to check bindings.
- `PartyFollowUpRunnerTests` exposes the existing default-assembly fixtures to
  Unity Test Runner, including HUD binding and saved upgrade progress.

Party Follow-Up is an opt-in, event-driven combat system. Each field ally owns one
fixed `PartyFollowUpSkillDef`. A qualifying combat fact opens a short-lived offer;
the player may consume that exact offer to make the owning ally execute its
dedicated follow-up skill. It is separate from the legacy scripted Chain Attack.

## Runtime Flow

1. `PartyRuntimeBinder` binds `PartyFollowUpOpportunityController` after the party
   and Helper runtime are ready and before UI binding.
2. `PartyCombatEventRouter` subscribes to every unique party
   `CombatEventBus`. It assigns missing `FactId` and root `CombatChainId` values
   once at the boundary.
3. `PartyFollowUpTriggerEvaluator` evaluates the fixed follow-up authored on each
   field ally's `CharacterStats`.
4. `PartyFollowUpOpportunityController` creates at most one current offer per role
   and snapshots its target, IDs, expiry, charge eligibility, and provenance.
5. Input consumes the current `OfferId`. The controller rejects stale IDs and
   revalidates the owner, target, charge, window, and party session.
6. `PartyFollowUpSkillExecutor` reserves the ally, applies its placement profile,
   and starts the dedicated skill through `CharacterSkillManager`.
7. A successful charge reservation commits first. Only then is
   `FollowUpSkillCommitted` published with its `SessionId`, `FollowUpWindowId`, and
   `ExecutionId`. Child facts retain that provenance.

The controller uses a pause-aware local clock. Offers and chain windows stop
while gameplay is paused, but every window remains finite. Chain lifetime and
depth limits come from `PartyFollowUpFeatureFlags`.

## Authoring Assets

Create assets through:

- **Assets > Create > Game > Party Follow-Up > Skill**
- **Assets > Create > Game > Party Follow-Up > Execution Profile**
- **Assets > Create > Game > Party Follow-Up > Feature Flags**

Assign the follow-up asset to `CharacterStats > Party Follow-Up Skill`. The MVP uses
one fixed follow-up per field ally and supports `PartySlot1` and `PartySlot2`.
`Helper` input/API support is reserved, but Helper offer generation is not part
of this MVP.

`PartyFollowUpSkillDef` must have a stable, unique `followUpId`, a dedicated
`executionSkill`, a supported trigger, positive offer durations, and an
`executionProfile`. Do not reuse the character's battle-slot, Helper proc,
Helper command, or legacy Chain Attack skill as the follow-up execution skill.

Supported triggers are:

| Trigger | Accepted fact |
| --- | --- |
| `FollowUpSkillCommitted` | another committed Party Follow-Up execution |
| `TargetHasStatus` | `StatusApplied` or `StatusStackChanged` with the required status/tag/stacks |
| `EnteredBreak` | `Hit` whose metadata records the ChainReady/break edge |

Reserved enum values for later phases (`FinalStrike`, `ArtsReaction`, and
`InflictionCountReached`) fail authoring validation and do not create offers.

`TargetHasStatus` reads `StatusEffectController` through the target's
`CharacteContext`; it does not discover actors through physics. The status
lifecycle publisher and the `EnteredBreak` trigger are guarded by
`phase4aPublishersEnabled`.

Damage targets that own their own stagger application must preserve
`DamageResult.StaggerApplied` and `DamageResult.EnteredChainReady` when returning
from `TakeDamage`. The Party Follow-Up publisher consumes this edge from the
attacker's `Hit` fact; entering ChainReady visually without returning the edge
does not create an offer.

Normal Follow-Up targets resolve through `CharacteContext` and must remain living
character effect targets. `Map_TestAI` is the one explicit authoring exception:
its lightweight `ChainAttackTestTarget` can be locked as a living placement
anchor without pretending to be a character or exposing character modules. This
keeps the Test AI Follow-Up popup and placement testable while preserving the
context-first target contract for gameplay actors.

## Cost, Charge, and Commit Contract

Party Follow-Up uses the execution skill's normal shared charge pool but ignores
Energy. `CharacterSkillManager` maintains a dedicated runtime entry and reads
only the Follow-Up definition's `upgradeTree`. Its snapshot uses slot key
`party-combo` and option key `followUpId`, isolating it from battle-slot upgrades.
The Skill Loadout screen includes Follow-Up in the bottom type selector, with the
fixed Follow-Up on the left and its upgrade tree on the right. Unlock/reset uses
the existing shared Skill Points and `activeSkillTrees` save storage; Follow-Up
selection is never written to battle loadout or run snapshot selections.
The manager refreshes the Follow-Up snapshot when its runtime entry is requested,
including before casting, so unlocks and resets affect subsequent casts.
The fixed follow-up choice is not player-selectable save data, and its transient
charge entry is rebuilt full after load under the current charge-pool contract.

Assign a dedicated `upgradeTree` on `PartyFollowUpSkillDef`; a missing tree leaves
the Follow-Up usable with no upgrades. Keep `followUpId` and the tree ID stable after
shipping. Feno/Aires test Follow-Up definitions have starter trees with a one-point
node reducing cooldown by 10%; these are editable test balance values.

Commit means the cast reservation succeeded and was committed. It is not
`CastReleased`, and cancellation before commit creates no follow-up fact. Immediate
casts raise `CastStarted` before committing, matching animated casts. Request-
scoped commit/failure/cancel callbacks prevent unrelated casts from being
mistaken for the accepted follow-up offer.

## Identity and Loop Safety

`PassiveEventContext` carries `FactId`, `CombatChainId`, `Depth`, and
`FollowUpExecutionProvenance`. `CombatEventBus` assigns IDs for new external and
child facts. Existing publishers that still emit zero IDs are normalized once
by the party router.

The controller guards three independent identities:

- `FactId + ComboId + OwnerRole` prevents duplicate delivery of one fact.
- `FollowUpWindowId + ComboId + OwnerRole` prevents reusing a follow-up in one window.
- `ExecutionId` settles pending execution facts and buffers synchronous child
  facts until the parent commit has been accepted.

Self-triggering the same follow-up skill is disabled by default. Old-session,
expired, too-deep, and uncommitted provenance is rejected.

## Scene and Prefab Wiring

On the player/context hierarchy add:

- `PartyFollowUpSkillExecutor`
- `PartyFollowUpOpportunityController`, with the executor and a
  `PartyFollowUpFeatureFlags` asset assigned

Bind both fields on `PlayerContext` (or let its context resolution find them).
`PartyRuntimeBinder` performs runtime binding automatically.

The project prefab is wired at `Assets/Prefab/Player/Player.prefab`. Both
components live on the Player root, the `PlayerContext` fields point to those
components, and the controller uses
`Assets/Data/PartyFollowUp/PartyFollowUpFeatureFlags.asset`. The feature and Phase 4a
publishers are intentionally disabled in that asset until the focused Play
Mode matrix has passed.

For HUD, add `PartyFollowUpHudPresenter` under the player HUD and assign one
`PartyFollowUpSlotView` for each field ally role. Each slot may bind an icon, radial
expiry fill, fallback label, and disabled-state label. `PlayerUIRuntimeBinder`
connects the presenter automatically.

The current HUD lives at
`PlayerUI/UI_Manager/PlayerHUD/PartyFollowUpHud` in
`Assets/Prefab/User Interface/PlayerUI.prefab`. It contains two hidden-by-default
views for `PartySlot1` and `PartySlot2`, stacked on the right edge of the screen.
The presenter reveals only the view that owns an active offer. A new offer fades
and scales in, then uses a subtle pulse while it remains pressable. The popup
shows `FOLLOW-UP READY`, the follow-up display name, the authored icon (or fallback
label), the role's input glyph, and a radial offer-lifetime fill.

Each field-ally vital block also contains a `FollowUpCooldown` indicator above its
HP bar. `PartyFollowUpSlotView` polls the owning ally's follow-up-owned
`CharacterSkillEntry` through `PartyFollowUpOpportunityController`; the horizontal
fill and seconds label therefore use the same shared charge pool as execution.
The indicator reads `READY` at full charge and never derives a second cooldown
timer from the popup. Both the popup and cooldown indicator remain hidden while
the runtime feature flag is disabled.

`PlayerInputHandler` exposes `OnPartyFollowUpSlot1`, `OnPartyFollowUpSlot2`, and
`OnPartyFollowUpHelper`. `Assets/Input/Inputmaneger.inputactions` currently maps
`PartyFollowUpSlot1` to keyboard `4` and gamepad D-pad Left, and
`PartyFollowUpSlot2` to keyboard `5` and gamepad D-pad Right. The Player prefab's
`PlayerInput` Unity Events call the matching callbacks. Keep the visible glyphs
in `PartyFollowUpHud` synchronized if these bindings change; do not edit the
`.inputactions` JSON by hand.

## Vertical-Slice Content

The test fixtures use `Assets/Data/PartyFollowUp/FieldAllyFollowUpExecution.asset` for
Feno and `Assets/Data/PartyFollowUp/AiresFollowUpExecution.asset` for Aires:

- Feno owns `Test.Feno.EnteredBreakFollowUp.asset`; an Entered Break fact from any
  active party member offers `Suppressive Break`. This prevents allied AI from
  stealing the final stagger edge without producing the player-facing offer.
- Aires owns `Test.Aires.FollowUp.asset`; Feno's committed follow-up fact
  offers `Shielded Follow-Up`.

Both definitions are under `Assets/Data/PartyFollowUp/Test/`. Aires uses
`Assets/Data/Skills/Aires/Aires_Skill_1.asset`, including its authored Placement
Point. These remain validation/playtest content.

## Execution and Cleanup

An execution profile may keep the ally in place or reserve a valid position
near the event target. It may keep the final position or return to the recorded
origin. Ally autonomy is temporarily suspended through `FieldAllyMember`; the
same reservation owner is used for placement and released on success, failure,
timeout, disable, party rebind, player death, or cancellation.

Targets are snapshots. An offer does not silently retarget when the current aim
or lock-on changes. A dead or missing owner/target makes the offer invalid.
The target handle also captures the character's enabled-lifetime generation, so disabling and
re-enabling the same pooled GameObject invalidates the old offer. Explicit event targets and the
non-character `Map_TestAI` exception remain authoritative; Party Follow-Up does not replace them with
the player's soft target.

Legacy ChainReady dispatch is the separate Aim-derived path: it snapshots the one committed
`PlayerTargetingController.CurrentTarget`. That same life-aware handle is forwarded through the
intro cutscene, coordinator, Field Ally/Helper steps, warp, facing, and skill request. A later Aim
change cannot redirect the chain, and a pooled new life cannot inherit the old meter transaction or
reservation.

## Follow-Up Slow and Camera Presentation

### Visible warp placement

`AlignPlacementPointToEventTarget` is an opt-in root-motion placement mode.
The executor samples the execution skill's resolved clip on the actor's rig and
reads `GetPlacementPointNormalized()`. Ring positions represent A on the jump's
ground plane; `desiredRange` is its horizontal distance from the target and
`localOffset` offsets that plane in target space. The resolver subtracts sampled
XZ displacement and yaw to calculate the grounded start pose S. The authored Y
curve is retained: A's root height is the grounded start height plus the sampled
Y displacement. It submits the entire 3D trajectory, desired A and placement
time to the shared resolver. Takeoff and landing require NavMesh/ground support;
airborne samples do not require support, but still undergo full 3D obstruction
and reservation checks. Camera ranking still uses
the accepted start pose. Missing sampling data or no safe path fails placement;
this mode does not fall back to a static warp.

When the execution skill enables `IgnoreCharacterCollisionDuringRootMotion`,
placement excludes character layers from body overlap/sweep queries for the
whole trajectory. CC actors use `RootMotionCCDriver.CharacterCollisionLayers`;
NavMesh actors such as Aires use the project's Player/Enemy/Ally layers.
The driver is resolved through `CharacteContext.RootMotionDriver`; no new prefab
binding is required. World geometry outside those layers and Follow-Up trajectory
reservations remain blocking. Static warp placement keeps its existing collision
rules. This allows Aires to continue through the boss after the Placement Point
instead of failing with `PlacementFailed` outside the short target-contact window.

This mode uses `TryGetWithVerticalMotion`, with separate cache entries from
legacy planar sampling. Playback receives `usePlanarRootMotion: false` so the
driver retains Y (the flag means suppress Y, not enable root motion). The skill
state already enables root motion. Cast Point, payload release, HitStart and
presentation timing are unchanged. A pose diagnostic checks the placement beat
including Y in LateUpdate. Cleanup
stops the matching root-motion request before releasing the reservation and
returning the actor, including cancellation after commit.

Aires opts into this mode through its own `AiresFollowUpExecution.asset`, copied
from the shared profile with the same camera/execution settings. Its initial
`desiredRange` remains 2m, now measured at A. Feno's shared profile retains
`PlaceNearEventTarget`. Jump height comes from the animation; this does not scale
the jump to a different target elevation or continuously chase a moving target.

`PlaceNearEventTarget` now evaluates a ring of `placementCandidateCount` poses
(default 8, limited to 1–16), keeping the authored range and target-local offset.
Every pose passes through the existing placement resolver; camera scoring uses
the final NavMesh-snapped position. With `requireUnobstructedPosition` enabled,
penetrating poses are rejected, including the shared resolver's least-penetrating
fallback. If no safe pose exists, placement fails before casting.

CharacterController bodies use their authored capsule dimensions for placement
and visibility samples; planar NavMesh support is checked at the destination's
ground height rather than the capsule's torso height. When the footprint matches
the body collider, the shared resolver treats a non-penetrating result from
its precise physics check as clear; padding touching
the ground must not fall back to a bounding sphere and reject every grounded
Follow-Up destination. Actual wall penetration still rejects the pose.

Among valid poses, Follow-Up prefers the most visible body samples (head, chest,
waist and two shoulders), then the most samples in the viewport, then distance
from the screen edges. Existing placement scores break remaining ties. Offscreen
samples count as not visible. If all poses are obscured, the least-obscured safe
pose wins; being obscured is not itself a placement failure.

Visibility rays use the gameplay camera before this Follow-Up's focus begins and
`visibilityObstructionLayers` (default all layers). World geometry, the event
target and other characters' non-trigger body colliders all block sight. Only
the casting actor's old body/hierarchy is ignored. Sensor/volume triggers do not
block sight; characters should have non-trigger body colliders in the configured
mask. Visibility is sampled collider geometry, not a per-pixel renderer test.
Without a camera, normal placement scoring is used. This adds no actor-discovery
physics queries and does not alter shared Helper/Chain/Summon placement rules.

`PartyFollowUpExecutionProfile.presentation` configures the accepted cast's short
presentation. Defaults enable a 0.25 world scale, eased recovery to normal at
the animation's actual cast point, 0.15-second camera blend-in, 0.25-second
camera hold after release, 0.25-second blend-out and a four-degree FOV reduction. Disable `presentation.enabled` per
execution profile to opt out. No additional scene component is required.

Only the casting ally receives a context world-slow exemption, released at the
cast point or on cancellation/cleanup. The presentation reads the active request's
animation progress, rather than assuming a fixed wind-up duration. Instant and
zero-cast-point skills skip presentation. `CastReleased` ends slow/exemption and
starts `cameraHoldSeconds`; the camera keeps focusing (and may finish blending
in) before returning. Set hold to zero for immediate return. Pause stops the
hold clock, a new Follow-Up replaces it, and pre-release cancellation skips it.
Camera disable, invalid/distant actor and cinematics still end focus early;
successful reservation commit remains the sole trigger for charge/cooldown and
`FollowUpSkillCommitted`. Recovery and placement reservations keep their existing
lifecycle after the camera starts returning.

The Feno Follow-Up execution authors `TauntApply` at normalized time **0.40**, after
its Cast Point **0.35**. Its payload needs that marker to apply taunt. Removing
the marker makes cast validation reject the entire Follow-Up before presentation
starts, so neither camera focus nor world slow begins. The authored Feno and
Aires definitions are checked by `AuthoredAllyCombo_CanStartItsExecution`.

The gameplay camera offsets its normal player pivot toward the ally and event
target, with a bounded displacement (default four metres) and mild zoom. Player
input and HUD remain available. Actors farther than twelve metres skip camera
focus; moving outside that limit ends focus. These are framing safeguards, not
a guarantee that all actors are visible through obstacles. The existing camera
collision handling stays active. Returning tracks the player's current position,
not a saved world-space camera pose. A new follow-up takes over the current blend;
an old execution cannot release the new camera owner.

Follow-Up slow uses a separate, lower-priority manual channel in `TimeSlowManager`.
Existing timed dash/cutscene slows take priority and retain their existing
last-request-wins behavior. Releasing them reveals the current follow-up progress,
if one remains. The newest follow-up owns the presentation channel; superseded
combos cannot update or release it. Full cutscenes and NPC presentation suppress
Follow-Up presentation; it does not resume afterward. Cutscene cleanup now stops
only its own timed slow handle. Global pause stops slow expiry/world-clock
advancement and Follow-Up camera/curve progress.

Disable, party rebind, and executor cleanup release the scope idempotently.
Pre-commit executor cleanup cancels only its matching active skill request so a
disabled executor cannot leave a delayed payload running after its reservation ends.
Validate pause before cast, cancellation, owner death, immediate payload failure,
overlapping ally casts, dash/cutscene interference, changed player position on
return, and close/wall-obstructed/distant camera framing in Play Mode.

## Validation

Run **Tools > Validation > Validate Party Follow-Up Skills** after editing follow-up or
character assets. Edit Mode smoke coverage is in
`PartyFollowUpCoreSmokeTests.cs`. C# compilation must use the repository's
`Assets/Scripts/CheckAssemblyBuild.ps1` workflow.

Before enabling the feature in production content, verify two-player-slot
chains, duplicate facts, status refresh/stack edges, break entry, stale input,
zero charge, pause, death, rebind, cancellation before/after commit, placement
failure, and cleanup in representative ally prefab layouts.
