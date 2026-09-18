# Defensive Block — production and regression scene

Only Rector Skill 1 opts in to Defensive Block. An available Aires companion has
priority; otherwise a ready Player can guard in place. Both use the same zero-damage
interception, Rector knockback, recoil, HitLag, VFX and camera settings. Other skills
retain their existing interruption flow. Receiver selection happens at input time;
a failed or late companion warp never automatically switches to Player.

Rector Skill 1 plays continuously from its first frame. `RectorCharge.windupSeconds`
is zero: the experimental 0.4 s pose hold was disabled because it broke animation
continuity. The field/optional hold path remains for compatibility, but production
authoring does not enable it. Interception requires actual swept contact with active
hitbox steps 0/1 and reacts immediately. The minimum-delay/contact-presentation
experiment was reverted; there is no forced wait between accepted input and impact.

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

Before a hitbox is active, `RectorCharge` estimates the lane with half-width 1.5 m,
forward reach 2.3 m and speed 8 m/s. Active hitbox bounds and measured forward speed
replace those estimates when available; Player collider extents expand the lane.
This predicts a straight charge, not future steering or a guaranteed collision.
Swept guard contact remains the only confirmation of success.

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

## Contact, animation and reactions

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
payload or costs. Assets live in `Assets/Data/DefensiveBlock`:

- `RectorCharge.asset`: command range, normalized window, allowed hitbox steps,
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
