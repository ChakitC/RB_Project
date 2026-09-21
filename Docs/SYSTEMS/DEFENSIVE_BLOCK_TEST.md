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
The Block row remains available on eligible Main Skills with Block disabled so a
first window can be added from that row.
Seconds are relative to the main animation clip, including when a preceding cutscene
is shown on the same ruler. Both endpoints are inclusive, matching runtime. Green
means the playhead is within the configured timing window; it does not simulate
guard availability, approach movement, collision, or successful interception.

Edits remain in an Editor draft until **Save All** or **Block > Save Block**.
**Block > Revert Block** reloads saved settings; Ctrl+Z / Ctrl+Y undo and redo
timing, mode, movement settings and enable/disable edits. Changing source or closing
the window prompts for pending changes. Drafts survive assembly reload. Preview
never invokes the Block runtime. External changes to the Skill's Block settings
require Revert before saving; unrelated Skill fields are preserved.

Each Skill stores `SkillDefensiveBlockSettings` inline in its **Defensive Block**
Inspector section. **Can Be Blocked** is the opt-in; a new Skill defaults to disabled.
There is no attack-profile asset or sub-asset to create, bind or share. Duplicating
a Skill also copies its settings. Runtime callers use `skill.defensiveBlock`, which
returns null while disabled. `skill.BlockSettings` exposes the stored data, including
disabled settings. Assigning settings copies them so two Skills cannot share mutable
Block windows through that setter.

For a new blockable attack, right-click its Block row and select **Block > Add Block
Window Here**, set the endpoints, check **Hitbox Steps** and **On Success**, then save.
The first window uses the saved/default duration (0.62 normalized for a new Skill),
clipped at the end. Additional windows in gaps initially span 0.1 normalized and
suggest the next unused hitbox step; check it against the actual payload.
**Block > Disable Block** turns off the opt-in on Save and retains all window data
for later re-enabling. Cutscene entries cannot author Block.

Choose **Block > Mode**:

- **Contact**: the skill keeps its normal playback. A ready guard must
  physically intercept an allowed active hitbox before it damages the protected
  player. There is no charge-path requirement or timed movement. This is the default
  for new settings and the mode used by **Rector Melee Heavy / Step 1**.
  Once the defender arrives, forward root motion stops at its guard plane until
  contact resolves or the guard/window ends. This prevents close lunges from
  passing through the defender before HitStart. The authored guard forward offset
  is capped at the attacker's position on arrival for close starts; that offset
  remains fixed for the guard session. No new setting is required. Pending warps,
  lateral misses, retreat, and attacks without an accepted guard retain their motion.
  Reaching the guard plane alone does not count as a Block; an allowed hitbox must
  still touch it before Player takes damage.
- **Timed Approach**: accepted input reserves a safe forward path, suppresses the
  selected attack hitboxes and moves the attacker to the guard. Impact resolves at
  the end of the movement. **Block > Timed Approach Settings...** edits **Duration
  (seconds)** and **Stand-off distance** in the same draft; both must be finite and
  greater than zero. Duration uses the attacker's actor clock. If a frontal attacker
  is already closer than stand-off, it stays at its current position for the same
  duration before Impact; it does not move backwards to create space. Facing away,
  being behind the defender, unsafe ground and obstructed approaches remain rejected.
  **Rector Skill 1** retains **0.22 seconds** and **1.6 m**. Close Block needs no asset change.

Other attack parameters (range, world mask, windup, threat prediction and knockback)
are editable in the Skill Inspector's **Defensive Block** section. Duration belongs
to the attack, not the defender. A zero duration does not switch modes: select
**Contact** explicitly. Contact does not use duration or stand-off distance.

Combo entries edit the selected execution Skill's settings; the combo root is not
the active attack. Basic Melee uses the same request-scoped runtime as active Skills.
Choose the combo Entry first, then select its payload's zero-based Hitbox Steps.
**Interrupt Skill** cancels the active request and clears its combo buffer;
**Continue Skill** suppresses the window's hits while retaining normal progression.

`DefensiveBlockActorProfile` remains character-owned through Character Stats or the
controller fallback. It owns guard animation, placement, dimensions, recoil and
feedback. An available Aires companion has priority; otherwise a ready Player can
guard in place. Receiver selection happens at input time; a failed companion warp
does not automatically switch to Player. Only Rector Skill 1 and Rector Heavy were
enabled during this migration; other Skills retain their existing behavior.

The migration preserves both attacks' timing, hit-step bindings, outcomes, threat
geometry and knockback values. Old embedded Block Profiles, the unused external
`RectorCharge.asset`, the attack profile type and ownership helper were retired.
The backup and original JSON values are outside Assets under
`../.codex-temp/block-inline-20260920`; the migration report is
`../BuildArtifacts/block-inline-migration.txt`. `Configure Production Assets` now
uses inline settings. Rector Skill 1's `windupSeconds` remains zero; no new clip or
automatic timing/balance adjustment is required.

## Multiple Block Windows and outcomes

`SkillDefensiveBlockSettings.windows` is an ordered list with inclusive start/end
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
authors `approachStandOff` (1.6 m root-to-root in front of the guard). A frontal
attacker already inside this distance uses its current position as the endpoint
and waits for the full duration. An attacker already behind the guard is rejected.
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

Before a hitbox is active, Rector's `Defensive Block` estimates the lane with half-width 1.5 m,
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

- `Rector_Skill_1.asset > Defensive Block`: command range, normalized window, allowed hitbox steps, timed approach stand-off,
  knockback distance/time, world mask and threat-prediction width/reach/speed.
- `GuardSetting.asset`: animation profile, placement/guard dimensions, timeout,
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
A sphere sweep shortens the shot near walls, ignoring the protected Player's own
controller and child colliders. Walking backwards through the shot therefore does
not push the camera toward the old Player position. The nearest remaining solid
obstacle still shortens the shot, including walls behind Player on the same layer.
This filtering belongs only to the Block shot; normal follow and aiming masks are
unchanged. Combo focus, cutscenes, NPC presentation
and another active virtual camera take priority; owner-checked cleanup prevents an
old guard ending a newer shot. The legacy `DefensiveBlockCameraShot` class remains
for compatibility/tests and is not attached to the upgraded scene.

## Regression scene

### Capturing real-play Block failures

In a normal gameplay scene, select the live **Player**, find
**InterruptionCommandController > Defensive Block Diagnostics**, and enable
**Log Defensive Block**. In `RectorDefensiveBlock`, use the **Log Defensive Block**
toggle in the test panel instead; the harness carries this setting to replacement
Players after Reset. Logging defaults off and does not change eligibility.

Press the normal Block command when the failure occurs. One attempt snapshot lists
active enemies, current cast/skill/request/window, admission reason and incoming
threat status; each registered companion reports role, busy/reserved/knockback,
animation/life state and readiness or placement/approach rejection. Player fallback
is included. Companion placement snapshots include desired/snapped positions,
stand-ahead settings, body collider/footprint dimensions, world layer mask, NavMesh
snap/footprint-edge failures, physical landing-floor hits and rejected surface normals.
Detailed overlap records identify colliders by name/ID/root/layer and penetration;
the final result compares world penetration against the 0.005 m Block tolerance.
Actor/reservation overlap is labelled as a score, not a Block rejection gate.
This trace runs only for command snapshots or Capture Block State, including ready
placements for comparison; normal readiness polling does not allocate log strings.
The selected guard then logs acceptance, arrival, Impact, timeout,
playback completion/interruption and cleanup. Attack instance ID plus request ID
correlate the command with later events. Events record UTC, frame and scene.

Use **Capture Block State** in the test panel (or the component context menu) to
record a snapshot without issuing a command. This is useful with Auto Block,
which waits for a ready receiver and therefore does not issue failed commands.
Ready-cue polling and idle frames do not produce logs.

Filter Console by `[DefensiveBlock]`. Logs are also appended to one session file
under `Application.persistentDataPath/Diagnostics/DefensiveBlock-<timestamp>-<id>.log`.
Use **Open Block Log Folder** in the panel or **Open Defensive Block Log Folder**
in the component context menu. The file survives scene changes and stopping Play;
a new play session gets a new file on its first logged event. File-write failures
warn once and leave Console logging active. Turn logging off after capturing the
case; no automatic retention/deletion is performed. Play Mode checkbox changes
are temporary unless deliberately authored into the scene/prefab before playing.

### Grounded recoil animation

Companion Block placement now uses the enabled `ctx.cc` locomotion body, as self
guard already does. The production Aires model's `Position` collider is attached
below `root.x` and can dip into the floor during animation; it must not decide
whether a companion can warp onto a clear floor. The accepted locomotion footprint
is also retained for landing revalidation and recoil. Actors without an enabled
CharacterController retain the existing model-collider fallback. NavMesh, physical
floor support, wall checks and the 0.005 m world penetration tolerance still apply;
the fix does not raise the character or loosen collision thresholds.

`BlockAnimationProfile.impactStartNormalized` and `impactEndNormalized` select the
part of the impact clip played over the existing `impactSeconds`/slide duration.
Defaults are 0–1 for compatibility. The production Aires profile uses 0.55–0.68 of
`Aires_Block_Recoil`; the complete source clip contains airborne movements and must
not be sampled in full for a grounded Block. Begin continues to use its own range.
Adjust the profile in the Inspector and check the supporting foot over the entire
selected interval. The clip import settings are unchanged.

Companion recoil applies its swept, NavMesh-checked position directly while its
autonomy is suspended. It does not call CharacterController.Move a second time,
which can push a warped companion upward when another character is close. Player
self guard still uses its CharacterController to stay in sync with vertical movement.

### Interactive controls

Open `Assets/Tests/DefensiveBlock/RectorDefensiveBlock.unity` and enter Play Mode.
The scene starts with a visible, unlocked mouse so the panel is immediately usable.
Press **F1** to switch between test controls and character control. While test
controls are open, the fixture suspends the Player action map and clears movement,
look, fire and aim input; clicking the panel cannot also fire or rotate the camera.
Simulation and the panel's Block/Auto Block controls remain active. **Cast Skill / C**
returns to character control automatically. Reset/Enemy changes reapply the chosen
input mode to the new Player; disabling the fixture restores the input it suspended
and returns cursor ownership to the gameplay camera.
In the Game View test panel, click **Enemy**, select a prefab, then click **Skill**
to select one of its attacks. Both lists can be searched. Light/Heavy combo steps
appear as individual execution Skills; Cast Skill runs that selected Skill through
the external Skill pipeline, not an entire melee combo/input sequence.
Changing Enemy or Skill recreates the actors, clearing previous casts and cooldowns.
**C / Cast Skill** casts the selection, **Space / Block** uses production Block
input, **Shift** dashes, and **R / Reset** recreates the trial. Start distances are
1.5/2/4/6/8/10 m. **Auto block when ready** waits for actual command eligibility and
can request again for each distinct Block Window. **Pause enemy / party AI** resets
the trial when toggled; turn it off to exercise normal actor AI.

The panel shows the selected Skill's saved Block mode/duration, Window versus Ready,
last Block/contact results and actor HP. Block-disabled Skills remain selectable
and are labelled **Block off**; the fixture never enables Block or changes timing
on Skill assets. Save changes in the authoring Timeline before testing.

`PartySpawnPoint` uses `DefaultPartySpawnConfig` and the production prefabs/UI/binder.
Scene-local `definitionOverrides` set party index 0 (Player) to **Roma**, with Aires
in the remaining party slots so the companion Block path stays available;
empty overrides use the saved party normally. They do not rewrite save data.
`CharacterContextPartyLoader.ConfigureRuntimeDefinitionOverride` is applied before
activation and remains authoritative during loader callbacks for that fixture.
Reset calls `DespawnParty`, waits for deferred destruction and spawns through the
same binder again. Only the harness owns manual cast/reset/auto-block controls.

`Tools > RB > Defensive Block > Create or Upgrade Test Scene` updates the existing
scene in place, preserving arena layout. It refreshes Enemy choices from concrete
prefabs under `Assets/Prefab/GameEnemy` and their animation/loadout Skills, excluding
the shared Base and combo containers. Additional prefab/Skill references can be
added to **Test Enemies** on the scene harness; refresh preserves those additions.
The upgrade saves only the test scene, sets its Roma roster, and does not invoke
production asset configuration or save unrelated assets. Production prefabs, Skills,
input and camera are shared; no stripped test actor copies or direct ally binding are used.
Run **Run Production Smoke Tests**, **Run Contact Order Tests**, and the harness
context menu **Run Play Mode Validation**. Contact-order coverage includes damage
before input, damage during departure, and Player moving in front of an arrived
guard. See `Docs/VALIDATION.md` for results and limitations.

The guard has a configurable half-depth of 0.05 m. This matches its displayed
0.1 m Gizmo thickness and catches the measured physical-contact edge where damage
could arrive a fraction before the old zero-thickness plane. A focused test checks
both contact inside this margin and a genuine remaining gap outside it.
