# Defensive Block — production and regression scene

## Cooperative Block

`Cooperative` is mode 2; Contact (0) and Timed Approach (1) retain their serialized values.
It requires a ready Player plus the first ready, opted-in Companion in party-slot order.
Both actor profiles must enable `cooperativeEnabled`; the default is false. No solo fallback
is offered. The Player stays in place, and the helper uses the first safe left/right position
computed from both body footprints. Ground, NavMesh, actor/world overlap, reservations and
the attacker's approach path are checked before readiness and again before movement.

The first bound payload step in the accepted window supplies an immutable damage snapshot.
Until that runtime is bound, the command is unavailable. Accepting suppresses the entire
request, including later-bound runtimes. The default approach lasts 0.22 seconds with 1.6 m
stand-off from the pair's midpoint. The request's animation/root motion is held at impact.

`CooperativeBlockSession` owns Preparing → Struggling → Success/Failure → Finished, with
Cancelled available before resolution. `State`, `Outcome`, `Meter`, `Remaining`, `Changed`,
`PressAccepted` and `Ended` are the HUD contract. `Ended` reports the outcome exactly once;
`Outcome` remains readable after Finished. The command controller exposes `ActiveCooperativeBlock`
and `CooperativeBlockStarted`. The initiating press contributes no meter. Only fresh Block
presses count; holding, preparing, paused and terminal input do not contribute.

When the session enters `Struggling`, the Player HUD plays its assigned `Screen wind` full-screen
effect once, oriented from the Player toward the incoming caster and using the cooperative
duration. This is presentation feedback only; it does not change meter, timing, or outcome rules.
The session keeps a `PlayerScreenEffectHandle` returned by `BeginScreenWindFullscreenEffect`.
It manually advances particles and fade with the meter's pause-aware real clock, including input
timestamp advances. The handle needs no HUD coroutine, so an inactive PlayerHUD can still supply
its existing prefab reference. The requested duration is exact, without a prefab/minimum-lifetime
extension. Success/failure disposes the wind immediately; cancellation and cleanup also dispose
it idempotently. Each handle owns only its instance, leaving other screen effects untouched.
The existing autonomous Heal/Perfect Dodge/`PlayScreenWind` APIs retain their previous behavior.
Owned-wind validation on 2026-09-22 passed the required build (0 errors, 93 existing warnings),
four Editor lifecycle tests and all 11 Play Mode impact trials. The fixture kept PlayerHUD
inactive; checks confirmed manual particle advance, pause, exact short duration, independent
handles, camera destruction and cleanup on success/failure/cancel/disable, with no coroutine
startup errors. Reports are under the workspace's `BuildArtifacts/cooperative-wind-*`.
Visibility follow-up: both Player UI prefabs had a `screenEffectSizeMultiplier` override of
110.8, placing the wind pattern outside the view despite successful particle playback. This
was corrected to 1.15 in `PlayerUI` and `PlayerUI_PartyHud`; a fresh Play Mode spawn was visually
verified, with the screen quad directly in front of the camera. See
`BuildArtifacts/cooperative-wind-visible.png` and `cooperative-wind-visibility.txt`.

Defaults: meter 50%, +10% per press, −20% per real second, 3 seconds, presentation world scale
0.15. The session's real clock excludes pause, including pause transitions. It advances before
input so a late press cannot undo timeout/depletion. All values belong to the skill's
`cooperative` settings and participate in Timeline draft, Save, Revert, conflict detection and
Undo. Choose **Block > Mode > Cooperative**, then **Block > Timed Approach Settings...**.
Cooperative always interrupts the whole request; Continue Skill is disabled and rejected on Save.

All three participants receive owned invincibility, status-application and knockback-application
tokens plus World Slow exemptions. Existing status effects continue ticking; they are not removed.
Protection tokens do not clear another owner's immunity. Success stops the request, pushes the
enemy and plays guard recoil without an automatic counterattack. Failure stops the request and
applies one captured hit to each defender through normal armor, damage and combat-event handling.
Authored hit knockback is used, otherwise 2 m / 0.4 s. Request/life/definition changes, death,
disable and forced cinematics cancel without a failure penalty. Cleanup is idempotent.

On success, each defender releases its protection and World Slow exemption when its own
Block animation ends and its control/movement scope is restored. A fast Player recovery
does not retain immunity while waiting for a slower Ally. The session finishes after both
guards end. The recovery watchdog uses the longer accepted Impact + Exit duration, including
the recoil minimum, plus 1 second of grace. It advances with scaled gameplay time, matching
the protected guards' animation clock through HitLag/global slow motion and pause; the old
fixed 2-second real-time cutoff no longer truncates long authored recovery. Active playback
and the watchdog retain their accepted settings if an author edits a profile mid-session.

Command diagnostics distinguish an existing guard, missing/disabled Block controller and
locked gameplay input from a physical line-of-sight obstruction. Cooperative planning reports
Player/Ally readiness, Player placement, Ally placement, attacker approach and damage snapshot
failures separately. Companion selection still uses party order and remains exempt from the
normal Ally Block minimum body-gap distance.

Recovery validation (2026-09-28): the required build passed with 0 errors / 93 existing
warnings; all 6 Cooperative Editor tests passed. Play Mode passed all 12 outcome/cleanup
trials, Input System edges, and the added asymmetric recovery case (Player 1.12 s,
Ally 3.9 s), checking camera release at 80%, independent protection release, pause and
snapshot stability. Selection/placement was rerun successfully after isolating other eligible
party members in the unavailable-helper fixture; normal slot fallback is still tested with
them restored. Disabled-command and locked-input reason checks also passed. Reports:
`P:/Game_RB_Project/BuildArtifacts/cooperative-recovery-build.log`,
`cooperative-recovery-playmode.txt`, and `cooperative-recovery-selection.txt`.

Player input remains enabled during the session. A `PlayerMovementCC` suspension token stops
both movement and overlap separation; releasing it cannot re-enable a component disabled by
another owner. Input System event timestamps feed `PressAt` before the render update advances
the meter. Out-of-order, pre-start and post-deadline events never rewind the clock. Active-scene
changes also cancel sessions when actors survive the scene transition.

One session-owned camera frames Player and Ally using the normal Block profile during
approach, then blends at contact to the profile's **Cooperative Struggle Camera** settings.
The default oblique rear shot puts the defenders on the left with space for the Block prompt
on the right. The transition pauses with the session. On success, return blending starts at
80% of the Player's actual Impact playback, with no extra camera hold. Player Exit/completion
also releases the shot if an update skips the threshold. The Ally can finish its own animation
afterward; cleanup never restarts the camera hold. The session's presentation World Slow
ends at the same 80% threshold, so the boss resumes normal speed during the camera return
instead of waiting for Ally Exit. This release also runs without a camera and preserves slow
effects owned by other systems. Failure/cancel releases both the shot and its World Slow immediately.
The 2026-09-29 follow-up passed the required build (0 errors, 93 existing warnings) and
the asymmetric recovery Play Mode test: slow remains before the threshold, ends with the
camera at 80%, and stays released while Ally finishes. No-camera release, repeated cleanup,
and preservation of another owner's slow also passed. Reports are in the workspace's
`BuildArtifacts/cooperative-slow-release-build.log` and `cooperative-slow-release-playmode.txt`.
The right-side gameplay HUD fades out during struggle/success and returns when the session
ends; left status/party panels and the Block prompt stay visible.
The camera follows the live Player position and starts at the authored offset,
moving backwards only as far as needed to fit padded body bounds inside the central 85%
of the output camera's aspect ratio. The approaching enemy does not widen the shot.
Existing obstruction checks take priority over framing, and blend return is unchanged.
The 2026-09-24 presentation check passed 27 Editor HUD/camera cases and the live
cooperative harness (selection/placement, 12 outcome/cleanup trials, Input System edges).
The live struggle capture confirmed right-panel alpha `0`; cancel/finish restored alpha `1`
and released the camera. Evidence: `BuildArtifacts/block-presentation-playmode.txt` and
`RB_Project/Captures/cooperative-struggle-presentation.png` in the workspace.
Cooperative camera shake uses the Player guard profile's **Cooperative Camera Shake**
fields. Contact starts one decaying rotational kick (1.8 degrees / 0.22 seconds / 22 Hz)
and blends in gentle struggle motion (0.12 degrees / 9 Hz). Each strength can be zero
to disable it. `CooperativeBlockSession` advances the owned shake with the same real,
pause-aware clock as the meter, including timestamped input advances; world slow does
not reduce the shake frequency. Sampling adds rotation to the clean camera pose, with
no positional drift. Resolution stops both effects immediately. A win starts a separate
one-time kick from **Cooperative Success Camera Shake** on the Player guard profile:
`cooperativeSuccessShakeDegrees` (1.8), `cooperativeSuccessShakeSeconds` (0.2), and
`cooperativeSuccessShakeFrequency` (20 Hz). Zero degrees disables it. The success kick
uses real time excluding Pause while the defenders recoil; failure/cancellation never
starts it. Settings are snapshotted with the shot. Cleanup, camera disable
and cinematic takeover clear them without clearing other systems' impulses.
On that same Preparing-to-Struggling transition, the session spawns the Player guard's
`impactVfx` once at the midpoint of the two guard contact centers. This uses the existing
`impactVfxLifetime` and does not resolve the Block or start guard recoil. No VFX is emitted
for a rejected command or cancellation before contact. Cleanup removes a surviving contact
effect; the existing success feedback remains separate. An empty VFX field is allowed.
The Player guard's `impactCue` also plays once at that initial contact point through
`AudioService`, alongside the VFX and camera kick. Struggle updates and mash presses do
not replay it. Empty cues are silent; cue volume, variation and playback limits apply
normally. The existing success sound still plays separately when the pair wins.
Initial-audio validation on 2026-09-22 passed the required build (0 errors, 93 existing
warnings). Play Mode confirmed one playing `BlockImpact` source before the outcome,
no repeat during struggle/mash, retained success sound and complete guard/camera cleanup.
Reports are under the workspace's `BuildArtifacts/cooperative-contact-audio-*`.
Contact-VFX validation on 2026-09-22 passed the required build (0 errors, 93 existing
warnings) and all 11 Play Mode impact trials, including initial position, no respawn
during struggle and cleanup. The initial flash was also visually checked in the Rector
fixture. Reports and screenshot are under the workspace's `BuildArtifacts/cooperative-contact-vfx*`.
Shake validation on 2026-09-22 passed the required build (0 errors, 93 existing warnings),
19 Editor cases and all 11 Play Mode impact trials. Checks include no shake before contact,
one-time impact, changing struggle motion, pause, zero-strength disable, owner isolation,
and cleanup after success, failure, cancellation and disable. Reports are under the
workspace's `BuildArtifacts/cooperative-shake-*`.
Close-camera validation on 2026-09-22 passed 11 Editor camera/framing/obstruction cases,
11 Play Mode impact/cleanup trials and the required build (0 errors, 93 existing warnings).
The live Rector fixture was visually checked with both defenders visible; reports and
the screenshot are under the workspace's `BuildArtifacts/cooperative-camera-*` and
`BuildArtifacts/cooperative-close-camera.png`.
`CooperativeBlockHud` uses uGUI/TMP and can bind authored fields; a default overlay
is created on the Player on first use. It shows meter, remaining time, partner, the actual Input
System binding label and accepted-press feedback. `CharacterAnimProfileSO.block.cooperativeStruggleClip`
is optional for Player/Ally; absent clips retain the held guard pose.

The enemy uses a separate per-skill `defensiveBlock.cooperative.enemyStruggleClip`,
with `enemyStruggleFadeSeconds` (default 0.06 seconds). Leave the clip empty to hold
the attack's impact pose. Assign an in-place, non-Legacy clip with positive length
to loop it after the attacker and both guards reach Struggling. The fade must be
finite and nonnegative. These fields are available in the Inspector and Timeline's
Timed Approach Settings popup and use the existing draft/Save/Revert/Undo workflow.
`CharacterAnimDriver.TryBeginEnemyCooperativeStruggle` checks the active request;
`CharacterAnimBrain` owns a separate presentation state, even when the same clip is
also the attack clip. Request time stays at impact, root motion stays disabled, and
sampling the loop does not replay attack timeline events. Pause freezes the loop.
Leaving the skill on success, failure or cancellation destroys the temporary state.

Enemy-loop validation on 2026-09-22: `CheckAssemblyBuild.ps1` passed with 0 errors
and 93 existing warnings. All 33 focused Editor cases and 11 impact/approach Play
Mode trials passed, including the four enemy-loop outcomes. Reports are stored in
`BuildArtifacts/enemy-struggle-build.log`, `enemy-struggle-editmode.txt` and
`enemy-struggle-playmode.txt` at the repository's parent workspace. The sample
keeps its optional clip empty; the loop trials assign the attack clip to a temporary
skill copy to test isolation without changing production or sample animation assets.

Use **Tools > RB > Defensive Block > Add Cooperative Test Skill** to create/update the fixture.
The existing test scene's Rector skill picker includes **Rector Skill 1 — Cooperative Block**.
Its asset and embedded payload are under `Assets/Tests/DefensiveBlock/Cooperative`; Player/Aires
test profiles also live there. The harness overrides only spawned actor definition copies when
this sample is selected. Production skill modes, damage values and profiles are unchanged.
Run **Run Cooperative Validation** on the harness in Play Mode for selection/placement, success,
failure, pause, overlapping protection, preparing/struggling cancellation, disable, reset,
death, definition swap, control loss and Input System press/hold/release handling. The fixture
waits for bounded recoil/knockback and camera recovery before checking cleanup. Death animation
can legitimately keep a member busy; its reservation and sequence ownership must still release.
`CooperativeBlockTests` covers
meter deadlines, frame-rate-independent timestamped inputs, settings snapshots and authoring rules.

Validation on 2026-09-22: the required `CheckAssemblyBuild.ps1` completed with 0 errors
(93 existing warnings). 67 Editor test cases passed across cooperative meter/authoring and
Block geometry, timing, selection, windows, camera, grounding and production integration.
The cooperative Play Mode fixture passed 14 groups, plus an active-scene change test that
preserved an independently owned movement token. Live Contact and Timed Approach trials also
passed with one successful Block, no Player HP loss and complete camera/control return.
Failure delivered one hit to each defender (57.08 / 49.59 HP with the fixture's different armor)
and committed/released the enemy cast once. Synthetic Keyboard state changes exercised actual
InputAction press/cancel callbacks, including the held initiating button. No new animation clips
were needed. Full run logs are in the workspace's `BuildArtifacts` directory.

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
only when the remaining charge segment would otherwise end too early. It keeps
the authored playback speed (including speeds below or above 1x) when the remaining
window fits the approach duration; it only slows playback to fit a shorter window.
Continue Skill restores that original speed after the approach. It retains
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

### Player CommanBlock gesture

Normal Ally Block impact also slides the protected Player with the Ally. Each frame
uses the Ally's actual planar displacement after its recoil sweep/NavMesh limits;
the Player's CharacterMovementMotor applies its own collisions and NavMesh boundary
checks. A wall can stop the Player earlier. No movement is added if the Ally is
blocked. Self guard and Cooperative Block keep their existing recoil without this
extra follow movement. During the recoil, separate Move/Rotate and movement-suspension
tokens prevent input/overlap separation from fighting the slide. They are released
at recoil completion or cancellation; Player death, recycling, knockback or a new
Player guard also ends the follow movement.

Validation (2026-09-30): required build passed with 0 errors / 93 warnings
(`../BuildArtifacts/PlayerBlockRecoilBuild.log`); Unity compiled runtime and Editor
sources successfully. `DefensiveBlockPlayerRecoilRunnerTests` passed 5/5, covering
open-ground displacement, a blocking wall, NavMesh edge, missing NavMesh and token
cleanup/self-guard exclusion. Live RectorDefensiveBlock testing accepted a normal
Ally Block and observed Impact: both Player and Ally moved -1.500 m along Z; Player
settled +0.065 m vertically on the sampled surface. After recovery CanMove/CanRotate
were true and control flags were None. The live probe used the real request API,
not keyboard input; Cooperative Block and every terrain layout were not replayed.

`CharacterAnimProfileSO.commanBlock` is an optional full-body action-layer ClipTransition.
After a normal Ally Block request is accepted (including approach setup), the same
call starts `Player.AnimDriver.TryPlayCommanBlock()`. Before starting the gesture
and its rotation lock, the Player root
faces the accepted attack's caster on the XZ plane. Rejected requests do not turn
the Player; a coincident planar target preserves the current facing. Player and
Ally start in the same frame; neither waits for the other's animation. The gesture
uses the Brain's separate full-body action layer with a fixed mask. Completion at
the transition's End Time, or interruption, fades that layer while preserving its
full-body pose. The upper-body mask stays fixed for shooting/reloading. It plays once and fades
back to locomotion/held fire. During playback it acquires a StateHub token blocking
walking and rotation. Completion, interruption, disable or destruction releases only
its own token, preserving locks from other systems. It does not reserve a skill.
Missing clips or locked animation states do not
cancel the accepted Ally Block. Self guard and Cooperative Block keep the Player's
guard animation and do not play this command gesture. Exclusive animations clear
the action layer as usual. Roma's production animation profile uses
`Assets/Animation/Ch_Roma_Anim/Roma_CommanBlock.fbx` for testing.

Validation (2026-09-30): assembly build passed with 0 errors / 93 warnings
(`../BuildArtifacts/CommanBlockBuild.log`), and Unity compiled successfully.
Unity Test Runner Edit Mode: `CommanBlockRunnerTests` passed 6/6 (0.76 s), covering
immediate start/completion, full-body mask and restoration, missing clips,
self-guard priority, Roma assignment, and lock cleanup on completion/disable while
preserving another owner's lock. Live RectorDefensiveBlock probing confirmed an
accepted request with Ally in Begin, Player playing Roma_CommanBlock at normalized
time 0, and CanMove/CanRotate both false in the same frame. After natural completion
both returned true and ActiveControlBlockFlags was None. The probe called the real
request API; physical keyboard input and every character combination were not tested.

Facing validation (2026-09-30): required assembly build passed with 0 errors /
93 warnings (`../BuildArtifacts/CommanBlockFacingBuild.log`); Unity compilation
completed without source errors. `CommanBlockRunnerTests` passed 6/6 after the
Edit Mode fixture explicitly initialized StateHub before admission queries.
In the live RectorDefensiveBlock scene, a Player starting at yaw 90 degrees faced
the caster with 0-degree error on an accepted request, stayed upright, played
Roma_CommanBlock and had CanMove/CanRotate false. A rejected TargetWindowClosed
request changed rotation by 0 degrees. These checks used the real request API;
physical keyboard input and other character combinations were not retested.
Console still reported the existing URP AutodeskInteractive shadergraph load
error and a TimeSlowManager cleanup error on leaving Play Mode; these remain
outside the facing change.

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

Ready cue recovery (2026-09-30): the cue resolves the current `Camera.main` each
frame, falling back to the active GameplayCameraController camera if no main camera
exists. Re-enabling the component rebinds its Player context and resets presentation.
Missing instances are recreated; a disabled renderer or missing instance material
is restored from the authored root renderer. The cue does not enable its own disabled
component or change camera masks. Offscreen behavior is unchanged.
`PresentationStatus` exposes the current presentation result. Enable `logPresentation`
on DefensiveBlockReadyCue to log status/readiness transitions, including MissingCamera,
MissingPrefab, Offscreen, CameraExcludesLayer and MissingRendererOrMaterial. Logging
is off by default. `Visible` describes the submitted cue, not a pixel visibility test.
The `BlockReadyCue.Tests` Edit Mode assembly covers camera replacement, re-enable,
renderer/material recovery, destroyed instances, invalid prefabs and log deduplication.
Validation for this change: the required assembly build passed with 0 errors and
93 warnings; Unity recompile reported no errors. After the initial Editor timeout,
Unity Test Runner Edit Mode was rerun on 2026-09-30: all six BlockReadyCueTests
passed (0 failed, 0 skipped; 0.30 seconds). Post-change gameplay/rendering remains
unverified; these tests validate presentation state and recovery, not rendered pixels.

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
  World-space hitbox bounds are projected separately onto the guard's forward,
  lateral and vertical axes. A long hitbox does not expand lateral/vertical reach
  using its forward depth; wide or tall shallow hitboxes retain their own reach.
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
- `GuardSetting.asset`: placement/guard dimensions, timeout,
  slide distance/time, warp fade, impact prefab/lifetime and global HitLag.
- `CharacterStats.animProfile > Block`: Begin/Impact/Struggle clips, guard pose and phase/fade timing.
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

Normal Contact and Timed Approach Block trigger one decaying camera kick on
successful impact, alongside the existing impact sound, VFX and HitLag. Tune
**Normal Block Camera Shake** on the defender's `DefensiveBlockActorProfile`:
`normalImpactShakeDegrees` (1.2 degrees), `normalImpactShakeSeconds` (0.15 real
seconds), and `normalImpactShakeFrequency` (20 Hz). Zero strength disables it.
The shot snapshots these settings, freezes shake during Pause, and clears it on
end/cancellation or camera takeover. Repeated contact cannot restart it.
Cooperative uses its separate shake settings and does not receive this kick.

Tune **Camera** on `GuardSetting.asset`: enable/disable the shot, position
(2.65, 1.55, -1.91), Euler (-0.35, -35.39, 10.49), FOV 60,
entry 0.16 s, post-impact hold 0.12 s, exit 0.4 s. Normal and Cooperative Block
shots follow the live Player position through movement/recoil and the return blend.
The heading stays fixed to the accepted guard direction, so Player turning does not
swing the shot. Cooperative framing still retreats enough to fit both defenders;
Ally movement does not replace Player as the camera's anchor. The prototype free-camera
shot also follows Player while blocking and retains its original-pose return behavior.
The 2026-09-29 follow check passed the required build (0 errors, 93 existing warnings)
and 18 Editor camera cases, including live Player translation, fixed heading, return blend,
Cooperative framing and wall filtering. Obstacle tests passed after removing leaked test
fixtures and rerunning with per-test teardown. Reports: workspace
`BuildArtifacts/block-camera-follow-build.log` and `block-camera-follow-tests.txt`.

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

`CharacterAnimProfileSO.block.impactStartNormalized` and `impactEndNormalized` select the
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

## Optional Block Impact marker

Each `DefensiveBlockWindow` can opt into `useImpactMarker` and an `impactNormalized`
main-clip time. The default is off, including for existing assets. Open/Close still
control command eligibility. Impact controls the attacker's animation pose when the
approach reaches its destination; it does not move the command window.

Impact is supported by Timed Approach and Cooperative. It must be at or after
Close, before normalized time 1, before the next Block window, and before the
clip's authored animation end. An unreachable/past marker rejects readiness.
Contact uses physical contact and does not accept an enabled Impact marker.

With a marker, the approach motor drives animation time from the accepted pose to
Impact using Animancer `MoveTime`, preserving intervening release/timeline events.
The actor's original playback speed is restored on continuation or cancellation.
The final pose waits for the animation/event update before resolving (up to two
render frames); it cannot overshoot into later hits on a long frame. Cooperative
holds this exact pose while struggling. Without a marker, approach playback and
its timing remain unchanged.

In Animation/VFX Timeline, right-click at the desired main-clip time and choose
**Block > Impact > Window N > Set Impact Here**. The pink Impact handle is draggable;
its hit area sits above Open/Close so coincident Close/Impact handles can be edited.
**Remove Impact (use default timing)** restores the default behavior. The selected
window's numeric value is also available in **Timed Approach Settings**. All edits
use the Block draft, Save/Revert and Undo/Redo, including single-window skills.

`DefensiveBlockImpactTests` checks exact poses and event preservation at simulated
30/60/144 FPS and a frame longer than the approach. `DefensiveBlockTimelineTests`
covers marker persistence, removal, Undo/Redo and validation. In Play Mode select
the Cooperative Rector sample, then invoke **Run Impact Marker Validation
(Cooperative sample)** on the harness. It uses a temporary skill clone to cover
Cooperative success/failure, Timed Interrupt/Continue, default playback, and cancel.
Four additional enemy-loop trials cover success, failure, cancellation and disable,
including pause, fixed request time, no repeated timeline events, no root movement,
wrong-request rejection and temporary animation-state cleanup. Timeline tests also
cover clip Save/Undo/Reload and invalid clip/fade values.

Validation on 2026-09-22: 49 focused Editor cases passed, including impact playback,
Timeline drafts and existing Block/meter regressions. All 7 Impact Marker Play Mode
trials passed with exact marked poses, one commit/release per cast and cleanup.
`CheckAssemblyBuild.ps1` passed with 0 errors and 93 existing warnings. Logs are in
`P:/Game_RB_Project/BuildArtifacts/block-impact-{editmode,playmode}.txt` and
`block-impact-build.log`. The original unsaved scene was restored after testing.

## Character-owned Block animation (2026-09-22)

### Aires separate Block clips (2026-09-28)

Incoming-threat selection keeps the authored threat lane when a hitbox becomes
active and additionally checks that hitbox's actual lateral center and width.
A narrow or offset animation hitbox no longer replaces/shrinks the authored lane.
Player body size/center is resolved through context, preferring its enabled
CharacterController. This only selects a Block candidate; command windows,
defender readiness, approach validation and actual hit interception still apply.

`InterruptionCommandController.allyBlockMinimumDistance` defaults to 1.5 m.
Distance is the horizontal gap between the Player and attacker's body colliders,
resolved through their contexts (CharacterController first, then CharacterPositionCollider).
Missing/disabled bodies fall back to the corresponding actor position. Overlapping
bodies count as zero gap. This allows close-range self guard against large bosses
whose collider radii already exceed the old root-distance threshold.
At or inside that body gap, companion selection is
disabled and normal Block uses the Player if self-guard eligibility passes.
Cooperative Block is exempt from this distance restriction and retains its
existing Player + Ally eligibility checks. The ready cue and command use the same
selection rule. Distance is checked when selecting a defender, not after the
enemy has begun its approach, so accepted Blocks continue normally. Set the
Inspector's Ally Block Minimum Distance to zero to disable this restriction.

Companion guard placement supports `DefensiveBlockActorProfile.standSideways`.
The production GuardSetting uses 0.8 m to either side of the Player-to-attacker
heading, preferring the right candidate when otherwise equally safe. Forward
distance is unchanged, and recoil remains parallel to that heading so it passes
beside the player's original position. Both candidates use the existing world,
ground and reservation checks; zero restores centered placement. This applies
to ordinary companion guard placement, not self guard or cooperative pair placement.
Guard hit detection remains based on its actual position and authored width;
check narrow Contact attacks when tuning the offset.

`Aires_AnimProfile.block` uses `Aires_Block_Begin`, `Aires_Block_Loop`,
`Aires_Block_Impack` (the supplied filename), and `Aires_Block_Exit`.
Begin and Impact sample the full clip range; their durations and Exit duration
match the authored clips. Loop repeats at its authored speed until impact or exit.
The optional `loopClip` falls back to the held Begin pose when empty; optional
`exitClip` falls back to the existing exit delay. Other character profiles retain
their previous behavior. Cooperative Struggle remains optional and unchanged.
Configure these fields in the character Animation Profile's Block section.
The gameplay Block camera starts blending out at 80% of the defender's actual
Impact playback duration, without an additional camera hold. This follows the
animation clock, including slow motion and any minimum impact duration. Exit
also releases the camera if Impact was skipped or the threshold was crossed
between updates. The defender finishes its animations and releases gameplay
reservations normally; final cleanup does not restart the shot.

Block clip/pose/timing data now lives inline at `CharacterStats.animProfile.block`.
`CharacterAnimBrain.BlockAnimation` uses the character's normal animation binding,
including overrides; `DefensiveBlockController.AnimationSettings` reads that source.
The normal driver entry is `TryBeginBlock(requestId)`. Active playback snapshots
its settings, so authoring changes cannot alter an in-progress Block.

`DefensiveBlockActorProfile.IsConfigured` validates gameplay settings only; readiness
also checks the character's Begin/Impact clips. Player fallback guard settings no
longer own clips. Legacy serialized animation references and the explicit legacy
API overload are retained for compatibility, but normal gameplay uses the character
profile. See PREFABS_AND_AUTHORING.md, Block animation ownership, for migration.

Aires/Roma/Feno/Milano profiles were migrated and all old animation fields compared
equal after the copy. Ownership, overrides, migration Undo/idempotency, grounding and
Block regressions passed 67 Editor cases. All 7 Impact Marker Play Mode cases passed
with the migrated data. CheckAssemblyBuild.ps1 passed with 0 errors / 93 existing
warnings. Logs: P:/Game_RB_Project/BuildArtifacts/block-animation-profile-build.log,
block-animation-profile-editmode.txt and block-animation-profile-playmode.txt.
