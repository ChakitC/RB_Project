# Defensive Block — production and regression scene

## Editing Block timing in Animation/VFX Timeline

In **Animation / VFX**, select Rector's `Rector_Skill_1` and its **Main Skill** entry.
The **Block Window** timeline row sits below VFX. Drag **Block Open / Block Close**
or right-click and use **Block > Set Block Open Here / Set Block Close Here**.
All Block controls live in the timeline. Right-click within a window to target it,
then use **Block > On Success**, **Hitbox Steps** (zero-based checkboxes), or
**Remove Window**. Right-click a gap to add a window; gap menus do not edit another
window. Drag the endpoints to extend or shorten a window. There is no settings panel above
the timeline. An asterisk on the row marks unsaved changes.
Context menus are filtered by the clicked row: Block, VFX, Hitbox, Cast Point,
and Other Events expose only their own actions. Existing event markers have their
own selection/removal menu. The ruler and animation strip do not add combat events.
The Block row remains available on eligible Main Skills without a profile so a
first window can be added from that row.
Seconds are relative to the main animation clip, including when a preceding cutscene
is shown on the same ruler. Both endpoints are inclusive, matching runtime. Green
means the playhead is within the configured timing window; it does not simulate
guard availability, approach movement, collision, or successful interception.

Edits remain in an Editor draft until **Block > Save Block** in the right-click
menu. **Block > Revert Block** reloads the current
profile and Ctrl+Z / Ctrl+Y undo or redo draft changes. Changing source, character,
or mode offers Save / Cancel / Discard; closing the window uses the same unsaved
changes flow. Dirty drafts survive assembly reload and preview never invokes the
Defensive Block runtime. Save writes window timing, step bindings and outcomes to
the Skill-owned profile, saving the containing Skill asset.
Other Skill fields and attack-profile settings are preserved. An external
profile edit or changed skill binding requires Revert before saving over it.

Each opted-in Skill owns one `DefensiveBlockAttackProfile` sub-asset. For a Skill
without Block, right-click **Block > Add Block Window Here** in the Main Skill view,
set timing, then choose **Block > Save Block**. Add is an undoable draft operation:
no profile is created or bound until Save. New windows start at the clicked time,
using the default 0.62 normalized duration clipped to the clip end. Basic Melee combo
entries and Cutscene VFX do not expose this as their own defensive window.

External or foreign-Skill profile bindings are legacy data: Save copies their
settings into this Skill rather than editing the referenced profile. **Block > Embed
Profile on Save** in the right-click menu stages that conversion even when timing is unchanged. The Skill Inspector
shows the binding read-only to prevent authoring shared references. Expand the Skill
asset in Project and inspect its `Block Profile` to tune allowed `hitboxSteps`,
windup, threat geometry and knockback. Duplicating the complete Skill asset copies
its profile as well; the two Skills can then be tuned independently.

Right-click the main animation timeline for **Block > Add Block Window Here**.
For an enabled Skill, choose a gap to add another window (initial length 0.1
normalized, clipped before the next window/end). The new window suggests the next
unused Hitbox Step index; verify it against the actual payload. **Select Window**
chooses which endpoints the context commands edit; dragging a marker also selects
its window. The controls provide Remove, Save and Revert. These operations support Undo/Revert,
and are unavailable in the cutscene segment or Play Mode.

`Tools > RB > Defensive Block > Embed Existing Skill Profiles` migrates existing
bindings without enabling other Skills. It is idempotent, preserves external source
assets, and keeps open timing drafts unsaved while updating their profile reference.
Rector's embedded profile retains the former `RectorCharge` values. The old external
file is retained for legacy references; production now reads the embedded profile.
`Configure Production Assets` also uses the Skill-owned profile.

`DefensiveBlockActorProfile` remains character-owned through Character Stats (or
the controller's existing fallback). Guard animations, dimensions and presentation
are separate from the attack-owned window. The timeline edits Defensive Block timing,
separately from Pre-Cast events and HitStart/HitEnd.

Only Rector Skill 1 opts in to Defensive Block. An available Aires companion has
priority; otherwise a ready Player can guard in place. Both use the same zero-damage
interception, Rector knockback, recoil, HitLag, VFX and camera settings. Other skills
retain their existing interruption flow. Receiver selection happens at input time;
a failed or late companion warp never automatically switches to Player.

Rector Skill 1 plays continuously from its first frame. Its `Block Profile.windupSeconds`
is zero: the experimental 0.4 s pose hold was disabled because it broke animation
continuity. The field/optional hold path remains for compatibility, but production
authoring does not enable it. Production now uses a timed approach: accepted Block
suppresses the selected attack's hitboxes immediately and moves Rector to a reserved
point in front of the guard over `GuardSetting.timedApproachSeconds` (0.5 by default).
Impact occurs at the end of that movement, without waiting for a hitbox contact.
This replaces the rejected experiment which waited in place after physical contact.
Set the duration to zero to use the previous swept-contact behavior described below.

## Multiple Block Windows and outcomes

`DefensiveBlockAttackProfile.windows` is an ordered list with inclusive start/end
times. Leave a gap between windows and bind each zero-based Hitbox Step to only
one window. Invalid ranges, overlap, shared steps and invalid outcomes fail closed.
Empty lists preserve the legacy single range/steps and Interrupt Skill result;
existing authored Skills are not automatically split or retimed.

- **Interrupt Skill** stops the request, cancels all its hitboxes and knocks the
  attacker back, as before.
- **Continue Skill** suppresses only the window's bound hitbox steps. The defender
  still plays Impact/recoil, sound, VFX and HitLag, but the enemy is not knocked
  back and its skill continues. A timed approach restores its previous playback
  speed and root-motion policy before the next attack.

Success/consumption and applied-damage rejection are tracked per window, while
request ID and life generation still isolate casts. A hit from an earlier window
does not prevent guarding a later one, and no damage is rolled back. Matching
steps remain suppressed for the rest of this execution, including payloads bound
after command acceptance; later steps stay enabled in Continue mode. Costs and
cooldowns are still settled by the original skill once.

Each new window restarts the flare and may play the ready sound once. A physical
guard cannot carry over into a different window; it releases when its window
closes. A timed command remains bound to its accepted window until it resolves.
Cancelled Continue approaches release the guard and resume the surviving skill,
without restoring the consumed hitboxes or awarding success. Death/playback loss
still cleans up the owned motion scope.

Leave enough animation time for defender recoil/Exit and the next safe placement.
The current range, incoming-direction and supported-approach checks still apply;
adding windows does not turn a departing attack into an incoming one. In particular,
Rector's existing one-way charge is not automatically redesigned as a multi-hit
combo. Author window timing/steps and movement for the intended skill.

### Timed approach presentation

The full-request interruption details below describe Interrupt Skill (including
legacy profiles); Continue Skill uses the selective suppression/resume rules above.

`GuardSetting.impactCue` plays through `AudioService` once on confirmed Impact,
for both Aires and Player self guard. It does not play on command acceptance or
cancelled guards. The default `BlockImpact` AudioCue uses `RB_Project_Block_SFX`
as a global 2D one-shot in the Sfx category; volume follows the existing Sfx mix.

The command and bright cue both require a valid receiver, a supported landing and
a clear straight NavMesh/body-sweep path to the impact endpoint. Rector's profile
authors `approachStandOff` (1.6 m root-to-root in front of the guard). An endpoint
requiring backwards movement is rejected; another ready receiver may be selected.
The attack must still be incoming, inside its command window, and must not have
already damaged Player. Acceptance reserves this outcome, subject to interruption
and path validity, rather than requiring Player to catch an actual collider later.

The original Skill animation keeps running through AnimDriver's request-scoped
`TryBeginSkillApproach`. It gives up root motion and adjusts positive playback speed
only when the remaining charge segment would otherwise end too early. It retains
the cast-point/timeline so ordinary payload/cost/cooldown settlement happens once.
An already released hitbox execution stops immediately; a matching later execution
is stopped during `Bind`, before it can damage anything. No other request receives
this suppression, and neither Player nor Ally gains general invincibility.

The approach motor interpolates the attacker from its accepted position to the
fixed endpoint. Its duration and the charge animation use the caster's actor clock
(World Slow unless exempt, plus global HitLag/pause). Guard animation/recoil still
use the defender's clock. Camera blends retain their unscaled clock. The motor
checks world collision, NavMesh and floor support each step; external displacement,
guard movement, death/down, control loss, cinematic, disable or reset cancels it.
An accepted attack stays suppressed after cancellation; it never resumes damage or
awards a later timed success. A pending unreleased cast uses the existing Blocked
cancellation cost policy; already committed costs are not refunded or repeated.

At the deadline, the guard must have arrived and still own Begin/Loop. The attack
releases its movement scope, stops the original skill request, then invokes the
existing knockback/Impact/HitLag/VFX once. It never parks a charging actor at the
endpoint to wait for a late warp. Failed arrival cancels. The ordinary no-contact
guard timeout is suspended only while the timed approach owns the session.

## Shared runtime flow

`SkillGemDefinition.defensiveBlock` selects an attack profile. `DefensiveBlockAttack`
subscribes to actual casts and binds caster, life generation and request ID to the
released hitbox execution. `InterruptionCommandController` selects an active member
from its own `FieldAllyManager.RegisteredMembers`, in party-role order, using the
loaded `CharacterStats.defensiveBlock` profile. If none can reserve a safe landing,
`TrySelectDefensiveBlockDefender` uses `Player.DefensiveBlock.CanBeginSelf`. There is no enemy-to-ally setup
reference required in a scene. `defensiveBlockEnabled` is the Player feature switch.
Rejected opted-in casts never fall through to the legacy Player interruption.

The bright ready flare and input use the same eligibility query. Range is 8 m in XZ,
height difference at most 1 m, with world LOS, NavMesh, placement and reservation
checks. Selection does not spend resources or reserve the ally; acceptance repeats
the checks and takes actor/landing reservations for the companion. Self guard takes
the Player reservation and control tokens, faces the attacker and stays in place;
it does not teleport or fade. A bright flare means input is
available, not guaranteed interception. No key prompt is displayed. A dim flare warns of
the incoming attack even when neither actor can guard. `TrySelectDefensiveBlockThreat`
uses the attack window and incoming lane without receiver/range availability gates.

Defensive Block does not require aiming or a committed target. The command queries
active enemy contexts for opted-in attacks whose forward charge lane overlaps the
Player, then selects the eligible attack with the earliest estimated contact time.
Distance and instance ID break ties. Attacks moving away or passing beside Player
are excluded. Each candidate still needs an available defender and safe placement.
The ready cue calls this same selector; an offscreen threat remains blockable although
its world-space flare is hidden. Legacy interruption targeting is unchanged.

`GuardSetting.readyCue` plays `RB_Project_BlockOpen_SFX` through the Sfx mix when
the actionable flare becomes visible. Dim unavailable threats and offscreen flares
are silent. The local cue remembers the last announced caster/request/life so it
does not repeat every frame or when the same selected attack flickers with range
or camera visibility; a new request or newly selected attack can announce again.

Before a hitbox is active, Rector's `Block Profile` estimates the lane with half-width 1.5 m,
forward reach 2.3 m and speed 8 m/s. Active hitbox bounds and measured forward speed
replace those estimates when available; Player collider extents expand the lane.
This predicts a straight charge, not future steering or a guaranteed collision.
These estimates select a threat; they do not decide the timed impact moment.

Companion placement additionally requires a non-trigger world surface within 0.2 m
of the resolved NavMesh point, with an upward normal of at least 0.5. Character
colliders do not count as ground. The same pose validator runs again before the
fade-out warp commits, so a removed floor cannot leave the defender on stale NavMesh.
This is a landing support check, not a guarantee for platforms removed after arrival.
Begin samples only normalized 0.55 to 0.65 of the production Aires clip and Loop
holds 0.65, avoiding its airborne approach animation.

Both receivers use `FieldAllyMember.TryBeginTransientExecution(owner, false, false)`.
The shared scope owns suspension/restoration of AI, agent, movement and Rigidbody.
It grants neither invincibility nor collision exclusion. Chain, Helper and Combo
continue using their existing defaults and cannot claim a reserved defender.
`CharacteContext.FieldAllyMember` resolves this shared reference across prefab layouts.
The shared `DefensiveBlockController` resolves `CharacteContext`; Player self guard
also holds an owner-scoped Move/Rotate/Shoot/Skill token and suspends its movement
module, including overlap separation. Cleanup restores the captured movement state
and releases only its own token. Player uses its normal time-domain exemption.
Self recoil snapshots the Player's active CharacterController footprint, falling
back to the authored body only when no controller exists. This avoids treating an
animated model capsule dipping through the floor as a wall blocking every slide.
The final displacement goes through `CharacterController.Move`, so the vertical
motor cannot overwrite a raw Transform movement on the following frame. Companion
actors without a controller retain the existing NavMesh-constrained transform path.

## Physical contact fallback, animation and reactions

- Snapshot a landing point 0.8–2.5 m ahead of Player, preserving up to 4.2 m approach
  clearance. Do not chase Player after acceptance. Recheck the point when hidden.
- Fade out 0.04 s, warp, fade in 0.08 s through `ctx.Visibility`. Begin runs during
  departure, but contact cannot succeed until arrival.
- One Block animation request owns Begin → held-pose Loop → Impact → Exit. Frontal
  contact during Begin after arrival jumps directly to Impact. Aires skill payloads never execute.
- Swept contact tests the active charge volume before target damage iteration.
  Rector's 2.166667 s clip charges through normalized 0–0.62; only hitbox steps 0/1
  intercept. The following strike is excluded. Rear/lateral misses do not succeed.
- The ready guard must contact the charge before Player. The guard sweep returns a
  frame fraction and compares it with a relative sweep against Player's eligible
  collider bounds, including Player movement. Player-first contact rejects the
  guard even if its callback runs first. When activation initially overlaps both
  volumes, a ready guard's front must be ahead of Player's front to win the tie;
  otherwise the guard fails. A previous applied hit always takes precedence.
  Bounds are a conservative ordering veto, not a new source of damage; ordinary
  hitbox contacts still decide damage. Rotating/animated collider shapes use their
  current bounds, rather than an exact continuous shape simulation.
- Applied damage is remembered for the victim's life within the caster/request
  execution, including damage before a command or during warp departure. That
  Player cannot later block the same request. A missed guard releases Aires and
  its camera/reservations, but leaves the enemy cast and subsequent hitboxes running.
  There is no HP rollback, extra invulnerability, cost refund or cross-attack veto.
- Success synchronously stops every hitbox for that execution and cancels playback
  and planar root motion through AnimDriver, before knockback starts. Keep already
  paid costs and cooldowns; no refund, duplicate payment or damage rollback.
- Rector uses its existing knockback motor: 2 m / 0.4 s away from Aires, force-replace,
  MiniStun reaction. Its collision mask includes the authored world layers.
- Aires slides up to 1.5 m / 0.35 s. The accepted placement footprint supplies the
  sweep geometry, including production animated trigger colliders. Recoil bones do
  not resize the sweep; solid Player/world geometry and NavMesh constrain motion.
  Close starts may leave no safe recoil distance. Translation root motion stays off.
- HitLag and impact VFX happen once. Each actor releases its own reaction independently.
  Timeout at 2 s is a miss, not a success. Death, down, control loss, cinematic,
  disable, reset, feature disable and character replacement release ownership.

Guard timeout and recoil use the defender's actor clock, matching Block animation:
`WorldDeltaTime` when `ctx.UsesWorldSlow`, otherwise `Time.deltaTime`. The shared
knockback motor uses the same rule for displacement and reaction recovery. World
Slow therefore stretches the movement and animation together; Player and temporary
world-slow exemptions retain normal actor speed. Global HitLag still composes through
`GlobalTimeScaleManager`, and global pause holds both clocks. These durations do not
use `WorldTime`, which advances independently of HitLag. Warp fades keep their
existing presentation timing.

`Accepted`, `Arrived`, `Impacted`, and `Finished(controller, impactSucceeded)` on the
controller distinguish request acceptance from actual impact. `RequestId` and
`ProtectedPlayer` are valid for the session; legacy command Success still means
accepted. Consumers must not interpret it as blocked damage.

## Authoring

Use `Tools > RB > Defensive Block > Configure Production Assets` for initial setup.
It binds original Rector Skill 1 and Aires definitions without changing the skill ID,
payload or costs. Attack settings live inside the Skill asset; presentation assets
live in `Assets/Data/DefensiveBlock`:

- `Rector_Skill_1.asset > Block Profile`: command range, normalized window, allowed hitbox steps, timed approach stand-off,
  knockback distance/time, world mask and threat-prediction width/reach/speed.
- `GuardSetting.asset`: animation profile, placement/guard dimensions, timeout, timed approach duration,
  slide distance/time, warp fade, impact prefab/lifetime and global HitLag.
- `AiresBlockAnimation.asset`: Begin/Impact clips, guard pose and phase/fade timing.
- `BlockReadyFlare.prefab`, material and shader: reusable gold prompt presentation.

The shared `Ally_Stryker` and `Ally_Helper` prefabs carry a controller; capability
still comes from the loaded character definition. `Player.prefab` carries the ready
cue and enabled command switch. The existing Rector variant now binds the Rector
stats definition and attack adapter. Runtime production references do not point at
`Assets/Tests/DefensiveBlock`. Historical test copies remain only as legacy assets.

Tune ready-light entry/exit on `Player > DefensiveBlockReadyCue`: 0.18 s appearance,
0.12 s disappearance, size, offset and brightness. Its fade-out tail cannot extend
eligibility. The billboard is reused without creating a new material every frame.

Author defender tuning on `DefensiveBlockActorProfile` through
`CharacterStats.defensiveBlock` (Aires uses `GuardSetting.asset`). The Controller's old
serialized tuning fields are hidden compatibility/runtime copies, not Inspector
settings. Existing prefab data and regression overrides retain those fields.
`Settings` exposes the character's profile; no scene-specific binding is required.
Profiles load on character/profile assignment; tune before Play Mode or respawn
the character to reload edits to the same asset during Play Mode.

The SO groups Animation, Placement, Guard, Recoil, Warp presentation, Impact
presentation and Global HitLag. Guard center height defaults to 1.2 m and impact
VFX lifetime to 2 scaled seconds. HitLag retains 0.06 real seconds at time scale
0.1 for the whole world. Set duration to zero to disable it. An optional curve
maps normalized duration to blend strength (1 = configured slow scale, 0 = normal);
an empty curve uses the global default. It fires once on confirmed impact.

## Production camera

Tune **Camera** on `GuardSetting.asset`: enable/disable the shot, position
(2.65, 1.55, -1.91), Euler (-0.35, -35.39, 10.49), FOV 60,
entry 0.16 s, post-impact hold 0.12 s, exit 0.4 s. The origin snapshots Player on
arrival, with Aires ahead along the incoming attack direction.

`DefensiveBlockController` supplies its loaded actor profile on arrival. The camera
copies all settings at shot entry and keeps them through hold/return, so changes
to a shared SO cannot alter an in-flight shot. Camera values are read anew for the
next shot. Disabling Camera suppresses the shot without disabling guard, HitLag or
knockback. Blend/hold timings continue using unscaled time. The camera's old fields
remain hidden for serialized/API compatibility; production shots use the SO.

A `DefensiveBlockCameraExtension` blends the existing Cinemachine camera state.
The normal follow rig continues updating underneath, so exit returns to the live
Player-follow position even if Player moved. No separate free camera takes control.
A sphere sweep shortens the shot near walls. Combo focus, cutscenes, NPC presentation
and another active virtual camera take priority; owner-checked cleanup prevents an
old guard ending a newer shot. The legacy `DefensiveBlockCameraShot` class remains
for compatibility/tests and is not attached to the upgraded scene.

## Regression scene

Open `Assets/Tests/DefensiveBlock/RectorDefensiveBlock.unity`. Controls are **C** charge,
**Space** Block, **Shift** Dash (either Shift key), **R** reset. The UI provides
4/6/8/10 m starts, optional auto-block, and a toggle to pause automatic combat.
Changing that toggle resets the trial. Turn it off to exercise normal actor AI.

`PartySpawnPoint` uses `DefaultPartySpawnConfig` and the production prefabs/UI/binder.
Scene-local `definitionOverrides` specify a deterministic Aires roster by party index;
empty overrides use the saved party normally. They do not rewrite save data.
`CharacterContextPartyLoader.ConfigureRuntimeDefinitionOverride` is applied before
activation and remains authoritative during loader callbacks for that fixture.
Reset calls `DespawnParty`, waits for deferred destruction and spawns through the
same binder again. Only the harness owns manual charge/reset/auto-block controls.

`Tools > RB > Defensive Block > Create or Upgrade Test Scene` updates the existing
scene in place, preserving arena layout. Production prefabs, skill, input and camera
are shared; no stripped test actor copies or direct ally binding are used.
Run **Run Production Smoke Tests**, **Run Contact Order Tests**, and the harness
context menu **Run Play Mode Validation**. Contact-order coverage includes damage
before input, damage during departure, and Player moving in front of an arrived
guard. See `Docs/VALIDATION.md` for results and limitations.

The guard has a configurable half-depth of 0.05 m. This matches its displayed
0.1 m Gizmo thickness and catches the measured physical-contact edge where damage
could arrive a fraction before the old zero-thickness plane. A focused test checks
both contact inside this margin and a genuine remaining gap outside it.
