# Rector Defensive Block Test

Open `Assets/Tests/DefensiveBlock/RectorDefensiveBlock.unity` and enter Play Mode.
The isolated scene spawns a Player, Aires and Rector from test-only prefab copies.
Press **C** to charge, **Space** (the existing interruption input) to block, and
**R** to reset. The on-screen controls also offer 4/6/8/10 metre starts and an
optional auto-block assistant. The assistant uses the same committed-target
command as manual input and attempts only once per charge. The test-only input
asset binds Space to Block, Shift to Dash, and T to Reload; production bindings
(Ctrl for Block and Space for Dash) are unchanged. Focus the Game View before
using keyboard controls.

`Tools > RB > Defensive Block > Create Test Scene` rebuilds the generated test
prefabs and scene. Save custom scene edits elsewhere before rebuilding. It keeps
the existing test skill/stats copies and block profile, and does not modify the
production Rector skill or prefabs. `Run Smoke Tests` runs the five focused
regression tests plus four camera tests (nine total). `DefensiveBlockTestHarness.RunValidation()` runs six repeatable
main Play Mode trials plus wall, reset, lifecycle and Begin-contact checks (31 total), exposing
outcomes in `ValidationReport`. In Play Mode, use the harness component's context
menu **Run Play Mode Validation**. The lifecycle matrix covers disable, death,
down, external control loss and reset during Begin/Loop/Impact/Exit.

## Authoring and behaviour

- `DefensiveBlockAttack` is opt-in on the test Rector. Its skill reference is the
  copied `RectorCharge.Test.asset`, with unique ID `test.rector.defensive_charge`.
  The original payload, cost, cooldown, damage and hit windows are retained.
- Sampling the original 2.166667-second clip shows root travel of approximately
  10.364 metres, ending around normalized time 0.62. Input is admitted during
  0–0.62; interception is limited to hitbox steps 0 and 1. The third finishing
  strike is not defensive-blockable.
- Range is checked from Player to Rector in XZ at command acceptance (8 m), with
  a 1 m height limit and a world-layer line-of-sight check. No pre-cast hold is
  used. The selected enemy stays the target; Player never substitutes for Aires.
- Aires' stand-ahead distance is clamped between 0.8 and 2.5 m, leaving 4.2 m of
  approach clearance when possible. The shared character placement resolver
  checks NavMesh, world/actor geometry and reservations. Close starts can have
  no room for recoil; collision safety takes precedence over the full slide.
- The prototype guard is 1.4 m in front of Aires, at height 1.2 m. Its half-width
  is 0.65 m and half-height 1.2 m. This projects in front of the existing hurt
  volumes. These dimensions are Inspector tuning data, not a universal shield.
- The active hitbox bounds sweep against that guard before contact damage.
  Front-facing overlap on hitbox activation counts as contact; rear approaches,
  movement away, lateral misses and vertical misses do not.
- Success synchronously stops every hitbox step belonging to that execution,
  cancels its animation request through AnimDriver, then force-replaces Rector's
  knockback: 2 m over 0.4 s away from Aires, using the motor's existing knockback
  playback and MiniStun recovery. There is no second cooldown charge,
  refund, damage rollback or global invulnerability.
- Aires receives no offensive skill payload. Its motor slides up to 1.5 m over
  0.35 s, constrained by solid geometry/Player and NavMesh. A 0.06 s HitLag
  happens once on success; `impactVfx` uses the existing CFXR Lightning Impact
  prefab and can be replaced in the test prefab Inspector.

## Warp fade

Aires uses `ctx.Visibility` to fade out at its original position (0.04 s), warp
only after the fade completes, and fade in at the guard point (0.08 s). Tune
`warpFadeOutSeconds` / `warpFadeInSeconds` on `DefensiveBlockController`.
Block Begin runs during the fade so the normal guard startup does not get an
extra animation delay; interception requires actual arrival, then accepts Begin
or Loop. A valid frontal charge contact during Begin goes straight to Impact,
skipping Loop. Fade-in grants no general invulnerability: wrong-direction hits,
other attacks and hits before arrival still use normal damage handling.
Longer authored fade-out values can make a close interception too late.

The destination is reserved at acceptance and revalidated when hidden. It stays
fixed if the Player moves. World penetration is rejected explicitly (the shared
resolver ranks colliding candidates too). Existing actor-contact scoring remains
unchanged, so the advancing charge does not invalidate its own interception point.
The static landing probe uses the exact collider footprint (zero inflation) and
a 0.005-unit contact tolerance to avoid rejecting floor-contact numerical noise.
An obstructed landing cancels the request and reveals
Aires at its current position. Cancellation, death, disable and reset unsubscribe
the fade callback, restore visibility, and release the destination reservation.
Actors without an enabled visibility controller retain the instant-warp fallback.
`CharacterVisibilityController.Appear(float)` supplies a sequence-local fade-in
duration; existing callers and serialized visibility defaults remain unchanged.

## Block camera

`Main Camera > DefensiveBlockCameraShot` frames the guard from a low, angled
over-Player-shoulder view after `DefensiveBlockController.HasArrived` becomes true.
Player stays in the foreground, with Aires ahead between Player and Rector.
Pending/rejected warps do not move the camera. The shot snapshots Player's position
and Aires's guard heading at arrival, so Player movement and the recoil slide cannot
re-aim it. `Bind(actor, player)` supplies the Player anchor; the single-argument
overload retains actor-relative framing for existing standalone callers.
Default local position is (2.65, 1.55, -1.91), local Euler angles
(-0.35, -35.39, 10.49), and FOV 60, based on the supplied reference camera.
Tune these on the scene's camera rather than on the character prefab.

The camera eases in over 0.16 s, stays through Block, holds 0.12 s after Block
ends, then returns over 0.4 s to the exact position, rotation and FOV captured
before the shot. Presentation uses unscaled time for hit lag and stops during
game pause. A Default-layer sphere sweep shortens the shot position at walls;
`obstacleLayers` must include only world geometry, not character colliders.
Normal exit, miss/timeout, death, down and control loss all release the shot.
Reset/rebinding or disabling the camera component restores immediately. The
scene-owned component keeps returning even when the actor has been disabled.
Another Block during return retains the original return pose.

This is a free-camera adapter for this test scene. It yields to an active
Cinemachine virtual camera, `GameplayCameraController`, cinematic or NPC
presentation, without disabling their drivers or overwriting their pose.
Production camera integration remains separate. The harness binds the newly
spawned Aires on each reset, and the scene builder recreates this binding.

## Ready cue

The copied Player has `DefensiveBlockReadyCue`: a gold horizontal flare with a
vertical glint anchored above the selected Rector, facing the gameplay camera.
It appears only while the selected charge accepts Block, within 8 m, with clear
line of sight and a safe placement for an available Aires. When the command is
accepted or eligibility ends, `IsReady` becomes false immediately and the glow
contracts and fades over `disappearSeconds` (0.12 s). This tail cannot extend the
gameplay window. It is a prompt to act, not a promise that contact succeeds.

`DefensiveBlockAttack.CanRequestBlock` shares the command's read-only eligibility
checks; `DefensiveBlockController.CanBegin` probes placement without reserving,
warping or playing animation. The flare reuses one billboard instance and a
property block. Over `appearSeconds` (0.18 s), its horizontal ray expands first,
followed by the vertical ray and a brief glint, then settles without repeated
flashes. Entry/exit use unscaled time, and a renewed window during exit smoothly
reopens the same cue. Target switches restart entry on the new target; disabling
the component or losing the camera/off-screen target hides immediately.
Tune `appearSeconds`, `disappearSeconds`, `targetOffset`, `screenWidth`,
`screenHeight` and `brightness` on the copied
Player component; change the gold tint in `BlockReadyFlare.mat`. The analytic
additive shader needs no texture or post-processing bloom. The scene builder
recreates the prefab binding. Production prefabs are unaffected.

## Animation ownership

`BlockAnimationProfile` uses the existing Aires block clips for presentation.
Begin advances to a configured guard pose in 0.12 s, Loop holds the pose, Impact
samples the recoil clip over 0.35 s, and Exit blends back. This is a temporary
held-pose prototype, not a newly authored looping animation.
Once Aires has arrived, Begin can transition directly to Impact on a valid
contact. The animation API accepts Begin/Loop only for the matching request;
Impact/Exit reject repeated impact commands. Damage interception still stops the
matching hitbox execution and Rector playback before applying reactions.
Impact lasts at least the configured slide duration, so tuning a slower slide
does not release the character before its movement has finished.

`CharacterAnimationMode.Block` owns one request across these phases. Commands
go through `TryBeginBlock`, `TryBlockImpact` and `EndBlock` on `CharacterAnimDriver`.
The Brain reports the phase and one terminal `PlaybackKind.Block` signal.
Translation root motion is disabled for this mode: the recoil motor owns travel.
Normal combat commands cannot cut Block short. Life/cinematic/control-loss
transitions can; controllers release AI, agent and member reservations on exit.
Timeout after 2 s has no success reward. Actors reset by recreation, invalidating
old life handles and clearing test cooldowns without touching saved party data.

## Boundaries

This prototype only covers Rector's copied Skill 1 and Aires. It is not an
Offensive block, a projectile barrier, or a replacement for pre-cast interruption.
Shared references resolve through `CharacteContext.DefensiveBlockAttack` and
`CharacteContext.DefensiveBlock`; ordinary actors leave these null.
The test prefabs remove party loaders and unused melee hitbox components and
disable autonomous combat/spawning, so loading the scene does not load or save
the user's party. Production prefab behaviour remains unchanged.
