# Defensive Block production integration plan

Status: runtime integration and shared production test scene implemented; validation is recorded in Docs/VALIDATION.md. Campaign-wide release checks remain separate.
Date: 2026-09-17

## First release scope

Enable the existing Defensive Block behaviour for production Rector Skill 1 and
Aires as an available companion. Other attacks retain their current behaviour.
Use production party spawning, target selection, AI, input, camera and save flow.
Do not ship the test harness, forced actor spawns, auto-block, reset UI or copied
test input bindings as gameplay dependencies. Keep the test scene as a regression fixture.

Defaults for this plan: choose only spawned, active, available companion actors
with an authored Block profile; never select the Player, summon an extra Aires,
revive an unavailable companion or interrupt an already reserved party sequence.
A missing/unavailable Aires means no Defensive Block opportunity. Do not add a
new resource cost or cooldown in this integration; retain current enemy costs.
Use the existing production Block action/bindings (currently Ctrl), not the
test scene's Space mapping. Offensive blocking and other characters/skills are
outside this release, although opt-in data must support later authoring.

## Behaviour to preserve

- Maximum command range 8 m in XZ, valid height/LOS/NavMesh/landing checks; no pre-cast hold.
- Snapshot the landing point on acceptance; Aires does not chase Player afterward.
- Fade out 0.04 s and in 0.08 s. Before arrival, no interception is possible.
- After arrival, a valid frontal contact during Begin OR Loop transitions to Impact.
  A Begin contact skips Loop. Misses, rear approaches and timeout do not succeed.
- Stop all hitboxes for that caster/life/request, cancel its playback/root motion,
  then knock Rector away: 2 m / 0.4 s. No refund or duplicate cost/cooldown charge.
- Aires recoils up to 1.5 m / 0.35 s, constrained by Player/world/NavMesh; no translation root motion.
- No damage from the intercepted execution, no damage rollback, and no immunity
  to other enemies or attacks. HitLag/VFX/success resolve once.
- Each actor resumes its own AI after its reaction. Death/down/control loss,
  pooling, party replacement, scene unload and cancellation release ownership.
- Preserve the animated ready flare, warp fade and low angled Block camera:
  Player foreground, Aires ahead, Rector beyond; then return smoothly.

## Verified integration gaps

| Area | Current implementation | Required integration |
| --- | --- | --- |
| Defender selection | Test harness assigns `DefensiveBlockAttack.ally` | Resolve from the issuing Player's party; bind the chosen defender only for the accepted session |
| Attack authoring | One exact copied skill, normalized window and step indices 0/1 in the component | Optional production skill profile; validate allowed windows and hitbox steps |
| Character authoring | Test Aires prefab directly binds animation/tuning | Bind capability from the loaded character definition, so shared companion prefabs do not make every character Aires-capable |
| Command/result | Defensive route returns acceptance through `Finish` immediately | Distinguish command acceptance from impact success/timeout/cancellation without changing legacy pre-cast event semantics |
| AI/ownership | Prototype manually saves BT/agent/Rigidbody flags | Coordinate with party reservations, formation and lifecycle; preserve collision and vulnerability |
| Camera | `DefensiveBlockCameraShot` yields when a production camera rig is active | Own the shot through `GameplayCameraController` / Cinemachine |
| Readiness | Flare queries an enemy component with a manually assigned ally | Shared player-side eligibility query used by both UI and command execution |
| Assets | Profiles/VFX reference test assets and a `.Test` recoil clip | Production assets and explicit opt-in prefab/skill binding |

## Implementation sequence

### 1. Move authoring to production data

Add an optional attack profile reference to `SkillGemDefinition`: enabled window,
allowed hitbox step indices, command range and enemy reaction tuning. Null means
no Defensive Block support; keep the original skill ID, payload, timeline, costs
and cooldown. Initially author only Rector Skill 1, allowing its charge steps
and excluding the finishing strike.

Add an optional defender profile to `CharacterStats` containing the existing
`BlockAnimationProfile` plus placement/guard/slide/fade/presentation settings.
Initially author only Aires. Resolve it when the character definition/model is
applied, including party swaps; clear old capability on character replacement.
Preserve existing classes/APIs and serialized fields during migration, with
explicit test overrides where the test builder needs them.

Gate: unsupported characters/skills remain unchanged; configuration validation
detects missing clips, invalid step indices/windows and missing placement bodies.

### 2. Bind party selection and command lifecycle

Extend `InterruptionCommandController` to resolve candidates from
`playerContext.fieldAllyManager.RegisteredMembers` using `member.ActorContext`.
Filter by companion identity, life/control state, active Block profile, reservation,
animation eligibility and safe placement. Choose deterministically (party order
as tie-breaker) and reserve actor plus landing point atomically; release both on
any failure. Do not retain an enemy-to-ally reference outside the accepted session.

Share the read-only candidate/window/placement query with `DefensiveBlockReadyCue`;
revalidate on input. The ready query must not warp, reserve or spend anything.
Route opted-in casts to Defensive Block; a rejected Defensive attempt must not
fall through to Player substitution or the legacy guaranteed pre-cast interruption.
All other skills keep their existing pre-cast route.

Publish request-scoped acceptance, arrival, impact and terminal outcomes for
presentation/diagnostics. Legacy `CommandFinished(Success)` must not silently
become an impact-success event; consumers must use the explicit Defensive result.
Resolve common references through `CharacteContext`; player-only selection belongs
to the player command system, not to the enemy or global actor searches.

Gate: no ready flare/command without a valid party member; two enemies cannot
claim the same member; Chain/Combo/Helper commands respect the same reservation.

### 3. Harden AI, execution and placement lifecycle

Retain `SkillHitboxSequenceRuntime.StopExecution` and interception before contact
damage. Track caster, defender and protected Player life generations plus request
identity; stale callbacks cannot affect reused actors or a new cast. Register and
unregister safely on enable/disable and after context/module replacement.

Use the existing `FieldAllyMember` transient-execution ownership where possible.
Audit `FieldAllyAutonomyScope`: `protectActor: false` disables actor protection,
but its separate collision override also needs a Block-specific opt-out. If new
options are needed, preserve defaults for current Chain/Helper/Combo callers.
Do not use blanket invincibility, untargetability or collision disabling for Block.
Release AI/formation/movement authority exactly once, respecting newer owners,
death/down and active reaction state rather than blindly restoring captured flags.

Validate real-world obstacle masks rather than the test's Default-only mask.
Retain landing revalidation, actor/placement reservations, floor tolerance and
body-sweep movement constraints. Check narrow spaces, slopes, stairs, edges,
moving actors and collider layouts on real prefabs. Sweep only eligible active
hitbox groups; unrelated attack/body colliders must not grant block success.

Gate: Begin-to-Impact still works, other attacks still damage normally, and
pooling/scene transitions/party replacement leave no reservations or callbacks.

### 4. Integrate presentation with the gameplay camera

Add an owner/request-scoped Block shot entry/exit path to `GameplayCameraController`.
Use a Cinemachine shot/override owned by that controller instead of competing
LateUpdate writes to Main Camera. Feed it session events; no harness binding.
Start after arrival and use the Player snapshot plus guard heading for framing.
Carry current shot defaults (0.16 s entry, 0.12 s post-Block hold, 0.4 s exit).

Keep the normal follow rig updating its target while the shot runs. Return to
the live gameplay camera, preserving current aim/FOV/input state, so Player
movement cannot cause a snap back to a stale world position. Suppress camera-look
input during the shot without locking Player movement/combat. Retain world
obstruction handling. Define ownership priority: cinematic/NPC and active
Chain/Combo presentation preempt Block; cancellation never steals camera ownership
back. Gameplay Block may finish without its optional camera shot.

Bind the ready flare through the actual Player runtime. Reuse the billboard,
avoid per-frame allocations, and share one readiness evaluation per frame rather
than repeat party/placement scans for UI and camera. Input always revalidates.
Respect death, pause, blocking menus, target changes and party/scene replacement.
Keep cosmetic fade tails separate from actual command eligibility.

Gate: framing shows Player/Aires/Rector; camera returns correctly while Player
moves or aims and when Block is interrupted by another camera owner.

### 5. Author and enable production assets

Use Unity Editor API/CLI to create production profiles and migrate/promote the
validated flare, material, shader and recoil clip dependency. Production content
must not rely on `Assets/Tests/DefensiveBlock` or test-only skill/config identity.
Bind production Player/companion/Rector prefabs through their real loaders;
preserve party loaders, saved loadouts, AI, auto-attacks and input configuration.
Update the test builder to exercise the same runtime implementation with fixture
data, not a parallel implementation.

Use a local opt-in feature setting with a disabled default during integration,
then enable only the authored Rector/Aires path after validation. Disabling must
clean up an active session and return to the existing skill routing safely.
No save schema migration is expected; Block configuration belongs in assets and
runtime state is not saved. Verify existing saves/loadouts still load unchanged.

Gate: enter through the normal game bootstrap with a real party and fight an
AI-controlled Rector without a harness or inspector reference patching.

### 6. Validate and document before release

Retain the 31-case prototype suite and nine smoke tests. Add production integration
coverage for character swaps, absent/dead/busy Aires, multiple Rectors, two input
attempts, unrelated concurrent damage, spawn/despawn reuse, scene transitions,
menu/input conflicts, real layers/obstacles, and camera preemption/live return.
Keep explicit Begin-contact and pre-arrival cases; test 15/30/60 FPS and variable
frame timing. No-block runs must retain normal enemy damage/AI/cooldown behaviour.

Validate C# only via `Assets/Scripts/CheckAssemblyBuild.ps1`; verify assets and
scenes in Unity, then perform a development-build gameplay smoke run (after
resolving any existing build blockers). Inspect the profiler for per-frame GC,
readiness/placement query cost and VFX lifetime. Report pre-existing failures
separately from regressions; prototype passes alone are not a production sign-off.

Update `Docs/SYSTEMS/DEFENSIVE_BLOCK_TEST.md`, `AI_AND_TARGETING.md`,
`Docs/ARCHITECTURE/ANIMATION_COMMAND_FLOW.md`, `Docs/PREFABS_AND_AUTHORING.md`
and `Docs/VALIDATION.md` with production setup, defaults and verified results.

## Definition of done

The normal gameplay scene can show a valid Block prompt, select the party's Aires,
warp and block Rector Skill 1 during Begin/Loop, stop only the intercepted attack,
play both reactions/fades/camera, and restore control without test dependencies.
Unsupported skills/characters retain their prior behaviour. No stale ownership,
duplicate impact, broad immunity, save regression or camera fight remains in the
integration matrix. Production enablement is limited to this verified content.
