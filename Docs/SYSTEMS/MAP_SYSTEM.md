# Map System

The runtime map is generated as a `MapGraph`. Each `MapNode` owns one lazily
created room instance for the lifetime of the current run. A room is created
the first time its node is entered, cached by `MapNode.Id`, and disabled when
the party travels away. Returning to a visited node re-enables the same room
instance instead of instantiating its prefab again.

Exit directions are resolved against the configured room definitions during
generation. The generator keeps its preferred layout when possible and chooses
a compatible alternative when a room type only supports specific door shapes,
such as a straight two-door Shop room.

All cached rooms share the configured `roomSpawnAnchor`. Only the current room
may be active. Disabling a room removes its baked `NavMeshSurface` data through
the surface lifecycle; enabling the destination room adds that baked data
again before the party is warped. Room transitions never rebuild NavMesh data
at runtime.

## Stage Data Layout

A `MapRunConfigSO` owns stage identity and nothing else. All of its tuning lives on profile
assets it points at:

| Asset | Serves |
| --- | --- |
| `MapGenerationProfileSO` | seed policy, critical path length, branch limits, exits per node, pity rule, node weights |
| `MapContentPoolSO` | the room and encounter definitions a stage draws from — a biome |
| `StageProgressionProfileSO` | level band, runs to clear, enemy level tiers, XP split, Stage Exit prefab |
| `StageDefinitionSO` | one stage as the player meets it: id, display name, run config, board order |
| `StageCatalogSO` | the ordered list of stages the Basement board offers |

Profiles are **required**, not optional. A config needs a generation profile and a content pool,
and a Test Stage additionally needs a progression profile; `MapRunConfigValidator` rejects a config
that is missing one, and `StartRun` refuses to generate. The properties still return safe defaults
rather than throwing, so the error can be reported without a crash first.

There is deliberately no inline fallback. A value has exactly one home, which means editing a number
always has an effect — the earlier arrangement, where a config kept inline fields that a profile
silently overrode, made Inspector edits look like they worked when they did nothing.

`Stage Id` deliberately stays on the config rather than moving into a shared profile: it is the key
progress is saved under, so it is per-stage identity and must never be shared. It must also be
unique and, once a build has shipped, immutable. When a stage has to be renamed, put the old id in
`Legacy Stage Ids` — on the config, and on the `StageDefinitionSO` if one exists. Loading the stage
then adopts and rewrites the old entry instead of starting the player from zero.

`slot_<n>_stage_progress.json` carries a `schemaVersion`. A file written before the field existed
reads back as version `0`, which is how an old save is recognised; it is normalised on the next
load.

## Controller Structure

`MapRunController` is the scene-facing facade. It owns the serialized setup and the run lifecycle
and delegates the work to four collaborators:

| Collaborator | Owns |
| --- | --- |
| `MapRunSession` | the generated graph, the current node and room, and whether travel is legal |
| `RoomRuntimeCache` | room instances, their activation, and their NavMesh data |
| `PartyRoomTransitionService` | moving the party into a room and putting it back |
| `StageRunProgressionService` | Test Stage progress, enemy level, XP pools, and completion |

Every method the shop, summon, room, exit, and enemy systems call still lives on the controller, and
every serialized field keeps its name, so scenes and prefabs are unaffected.

`PartyRoomTransitionService` takes the party from `PartyRuntime`: its `Root` is the warp root and
its `Actors` are the members to move. Nothing searches the scene for characters or matches object
names any more, so an object called "Party" in a scene has no effect on where the party warps.

## Room Lifecycle Hook

Behaviour that belongs to one kind of room rather than to rooms in general is authored as a
component on the room prefab implementing `IRoomLifecycleListener`. `RoomController` discovers those
components and calls `OnRoomInitialized`, `OnRoomBegan`, and `OnRoomCleared` at the matching points,
so the generic controller never branches on stage or node type. A listener that throws is logged and
skipped rather than taking the room down with it.

`TestStageRecoveryStations` is the first listener: it configures a Heal room's authored heal and
ammo stations into the Test Stage recovery contract. It lives on the Heal room prefab, not inside
`RoomController`.

## Runtime Room Ownership

`RoomController` creates a `RuntimeContent` hierarchy automatically. Room
prefabs do not need to author or serialize these transforms:

- `Persistent` owns normal `ItemPickup` instances. Uncollected drops are hidden
  with their room and return when that node is revisited.
- `Encounter` owns spawned enemies and corpses. It is cleared when the party
  leaves a completed encounter room.
- `Temporary` is available for room-scoped objects that must not survive a
  room transition.

Collected item pickups are destroyed normally and therefore do not return.
`SkillPickup`, active projectiles, legacy bullets, and transient world or enemy
VFX are cleared during travel. VFX parented under a player or companion context
is preserved so status presentation can survive the transition.

The transition sweep has to be scene-wide, because pooled projectiles live under a
`DontDestroyOnLoad` root rather than inside the room. What keeps it safe is
`RoomTransitionCleanupScope`: anything under the party root or under **any** cached room instance is
left alone. Without that scope the sweep also destroys the uncollected drops waiting inside a cached
room, which is the whole point of caching it. Clearing the outgoing room's own encounter and
temporary content is a separate, explicit step.

## Travel And Rollback

Rooms whose definition requires clearing cannot be left while their current
`RoomController` is uncleared. `MapRunController` enforces this independently
of exit visuals and interaction locks.

Every committed warp re-aligns the gameplay camera behind the player through
`GameplayCameraController.SnapYawToPlayer()`. The controller samples its yaw from
the player only when the player reference changes, which happens at party spawn —
before the party has been warped and turned to face into the room. Rooms are also
instantiated with a per-node yaw, so without this call the camera keeps pointing
the way the previous room faced.

A transition is a transaction. The previous room is only hidden while the warp
is attempted: its encounter keeps running, its spawned enemies and its temporary
content stay alive, and world projectiles and VFX are left alone. Nothing is torn
down until the party is known to stand in the destination room.

On a successful warp the controller commits in one step: the previous encounter
is stopped, the previous room's encounter and temporary content are cleared,
transient world objects are swept, item-drop ownership moves to the destination,
the node is marked visited, and `RoomTransitionCommitted` is raised.

If any required party member cannot be placed on the new room NavMesh, the
destination is disabled, the previous room is re-enabled, and the previous party
pose and item-drop ownership are restored. The previous room is restored rather
than re-initialized, so a locked-down room stays locked. Node reveal and visit
state are untouched, and `RoomTransitionRolledBack` is raised.

When the *first* room of a run cannot be entered there is nothing to roll back
to. The controller then stays roomless — `CurrentNode`, `CurrentRoom`, and
`HasActiveRoom` all report nothing, `BeginRoom` is never called, and no commit
event fires — so the run can be resumed with `TryEnterStartRoom()` or ended with
`AbortRun()`.

Starting a new run destroys every cached room and clears all runtime ownership.
The cache is in-memory only; saving and loading a run across application
sessions is outside the current map runtime contract.

## Stage Intro

The Start node of a run can play a group-shot intro before gameplay begins.
The order inside `MapRunController.EnterNode` is:

1. activate the destination room and warp the party to its room spawn;
2. commit the graph state and raise `RoomTransitionCommitted`;
3. if this is the Start node and the intro has not been attempted yet, look for
   a `StageIntroRig` under the room instance and call `StageIntroRig.TryPlay`;
4. `RoomController.BeginRoom` and `NotifyMapChanged` run only after the intro
   reports completion.

`isTransitioning` stays true for the whole intro, so the party cannot travel out
of the Start room while it plays and no encounter can begin behind it.

The intro is attempted at most once per `StartRun()`. Walking back into the
Start room later in the same run does not replay it. `StartRun()` resets the
attempt flag for the next run.

Everything about the intro is fail-open. A missing rig, a missing or zero-length
Camera Clip, a missing/duplicated actor marker, a missing camera rig, a busy
`CutsceneDirector`, or a missing `PartyRuntime` all make `TryPlay` return false,
and the room starts immediately with no intro. Production intros therefore stay
disabled until an author supplies a real camera AnimationClip.

While the intro runs the rig:

- hides the HUD, opens a full-black overlay, and shows letterbox bars;
- warps `Player`, `PartySlot1`, `PartySlot2`, and `Helper` onto their markers;
- plays each character's `CharacterAnimProfileSO.stageIntroClip` as an exclusive,
  root-motion-free locomotion state (locomotion idle when no clip is authored);
  characters sharing an anim profile share the pose;
- runs the Camera Clip on the rig's camera rig — that clip's length is the
  master duration, so a shorter character clip holds its last frame;
- deactivates the player's `PlayerInput` and offers a hold-to-skip on the
  `Player/Interract` binding (0.75s, unscaled). A button that was already held
  when the intro started is ignored until it is released once;
- disables companion `BehaviorTree`, `NavMeshAgent`, and `AgentMoveDriver`, and
  takes an owner-scoped `StateHub` control-block token so releasing it can never
  clear another system's external block.

`Time.timeScale` is never changed. On completion, skip, disable, scene unload,
or an exception, the rig restores every captured pose and component state,
returns the camera, re-shows the HUD, and invokes its completion callback
exactly once.

## Test Stages

The Basement Mobiliz board contains a bounded second page with three Stage
Placards, and a third page holding BOSS RUSH 01. Selecting one immediately
passes its `MapRunConfigSO` through the persistent `SceneLoaderSystem` and loads
the shared `MapRun` scene. There are no character-level entrance gates, and
every Test Stage remains replayable.

The authored Test Stage configs are under `Assets/Data/Map/TestStages`:

| Stage | Character range | Target clears | Enemy Level tiers | XP budget / run |
| --- | --- | ---: | --- | ---: |
| TEST STAGE 01 | Lv1-11 | 2 | 5, 10 | 1,625 |
| TEST STAGE 02 | Lv11-20 | 3 | 13, 17, 20 | 2,900 |
| TEST STAGE 03 | Lv20-30 | 5 | 22, 24, 26, 28, 30 | 5,940 |
| BOSS RUSH 01 | Lv1-11 | 1 | 10 | 3,250 |

### Direct-to-Boss stages

`criticalPathNodeCount` accepts a minimum of `2`, which produces a critical path
of exactly `Start -> Boss` with no intermediate room. BOSS RUSH 01 uses that
shape. A direct-to-boss config must also set `minBranchCount` and
`maxBranchCount` to `0`, because a two-node path has no node that is eligible to
parent a branch, and `MapRunConfigValidator.ValidateBranchCapacity` rejects any
non-zero minimum.

`MapPathValidator` enforces the "blue room before the Boss" rule only when the
config sets `forceBlueBeforeBoss`. `MapPathValidator.Validate` therefore takes
the `MapRunConfigSO` alongside the graph; passing `null` keeps the rule enforced.
A direct-to-boss config must clear `forceBlueBeforeBoss`, but it still has to
keep a blue room definition with at least two exits in `roomDefinitions`, because
`MapRunConfigValidator` checks `PitySystem.ForcedBlueType` unconditionally even
when no blue node can ever be generated.

The Test Stage balance assumes the following fixed encounter composition:

| Stage | Normal enemies | Elite enemies | Boss |
| --- | ---: | ---: | ---: |
| TEST STAGE 01 | 4 | 0 | 1 |
| TEST STAGE 02 | 4 | 2 | 1 |
| TEST STAGE 03 | 4 | 4 | 1 |

The forced blue node immediately before the Boss is a `Heal` node. A Test Stage
Heal room uses two independent one-use party stations authored in its prefab:

- `Heal Point` restores 50% of maximum HP to each living party member. It does
  not revive downed/dead actors and does not force a full heal.
- `Ammo Point` fills each party member's reserve ammo to its current maximum.
  It does not refill the loaded magazine.

`RoomDefinition.Heal` uses the straight two-exit `Heal.Up.Down` room with
encounter start, exit locking, and clear requirements disabled. Its maximum
exit count is two, so the generator does not attach a branch to the forced Heal
node and rotates the room to match its critical-path entrance and exit. During
room initialization, `RoomController` configures the authored stations for
party-wide behavior and creates simple runtime fallbacks only when a station is
missing from the room prefab.

HP and loaded/reserve ammo otherwise carry between rooms. Shop remains a branch
dead-end node rather than the forced pre-Boss recovery node.

### Test Stage Character Combat Scale

| Character | Role | HP / HP-Lv | Damage / Damage-Lv | Armor / Armor-Lv | Crit / Crit Damage | Stamina | Speed |
| --- | --- | --- | --- | --- | --- | ---: | ---: |
| Roma | Assault | 900 / 32 | 14 / 0.55 | 6 / 0.25 | 10% / 1.6x | 80 | 4.7 |
| Aires | Tank | 1,250 / 42 | 8 / 0.40 | 22 / 0.55 | 5% / 1.5x | 100 | 4.2 |
| Feno | Support/HMG | 1,100 / 36 | 9 / 0.45 | 14 / 0.40 | 5% / 1.5x | 90 | 4.4 |
| Milano | Marksman/Support | 950 / 33 | 11 / 0.50 | 8 / 0.30 | 12% / 1.75x | 80 | 4.5 |
| Dorothy, Noemi, Roger, Abbygail | Generalist placeholder | 1,000 / 35 | 10 / 0.50 | 10 / 0.35 | 7% / 1.5x | 80 | 4.5 |

All characters use 100 maximum Energy with no level growth. Stamina, critical
chance, critical multiplier, Energy, and movement speed also have no level
growth. Incomplete characters intentionally keep the generalist placeholder
scale until their own weapons and skills are designed.

The tuning target for a level-appropriate, familiar party is 25-45 seconds per
normal combat room, 8-15 seconds per Elite, and 60-90 seconds for a Boss, with a
70-80% first-attempt clear rate. Expected damage contribution is roughly
35-40% from the controlled character, 20-22% from each field ally, and 15-20%
from the helper. Losing one member should slow the fight materially; a viable
solo Boss clear may take roughly two to three times longer.

`Stage Progress Count` is stored per stable Stage ID and save slot in
`slot_<n>_stage_progress.json`. It advances only when the Boss is cleared and
the player uses the Stage Exit to return to Basement. It is capped at the
config's target clear count; farming after the cap continues at the final
Enemy Level tier. Use `SaveManager > Reset Test Stage Progress` from the
component context menu for test resets.

Every run resolves a fresh random seed and records it on `MapGraph.ResolvedSeed`
and in the `MapRunController` lifecycle log. Directly playing the MapRun scene
uses its serialized config as a legacy/editor fallback and logs that no
Basement selection was supplied.

## Test Stage Enemy Level And XP

`EnemyLevelSystem` belongs to the spawned enemy and is assigned by the active
Test Stage. Enemy Variant prefabs own only canonical Lv1 base stats and per-level
growth. `StatsHub` evaluates scaled character stats as
`Base + Scaling * (Level - 1)` for both players and enemies.

Successful-run XP comes from the Level Table range divided by the target clear
count. The runtime allocates 60% across regular enemies, 20% across Boss enemies,
and 20% as the Stage Completion Bonus. Remainders are distributed across enemy
spawns so clearing all authored enemies and taking the exit yields the exact
per-run budget. Each award is granted in full to all four deployed party actors;
it is not split and does not depend on last hit. Kill XP persists if the attempt
is abandoned, while the completion bonus and Stage Progress Count require the
Stage Exit.

Legacy `MapRunConfigSO` assets with no Stage ID retain the old behavior: enemy
prefabs are not level-overridden, XP uses `EnemyHealth.xpReward` and last-hit
ownership, and no Stage Progress or completion bonus is recorded.

### Test Stage Enemy Combat Scale

Test Stage enemies use `Enemy_SMG_GR01` instead of the player/drop
`SMG_GR01`. The enemy weapon contributes 10 damage, fires every 0.20 seconds,
has no critical chance, and uses infinite reserve ammo. Canonical enemy
`CharacterStats` remain separate:

| Enemy | Base HP | HP/Lv | Base Damage | Damage/Lv | Base Armor | Armor/Lv |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| M1 | 380 | 7 | 15 | 3 | 3 | 0.25 |
| M2 | 550 | 10 | 22 | 3.5 | 7 | 0.35 |
| Elite | 1,010 | 16 | 40 | 5 | 15 | 0.50 |
| Boss | 6,330 | 100 | 80 | 7 | 25 | 0.75 |

The M1 HP curve targets 15 non-critical body shots at close SMG range for a
Lv7 Roma against the Stage 01 Lv5 M1: 408 HP versus about 28.17 damage after
armor per shot. Critical hits can reduce the observed count.
The M2, Elite, and Boss HP curves are reduced on the same M1-derived scale so
their previous durability hierarchy remains intact across later enemy levels.

Their Lv1 basic projectile damage before mitigation is therefore 25, 32, 50,
and 90 respectively. Enemy critical chance and growth are zero.

## Test Stage Encounters And Exit

Each stage has separate Combat and Boss encounter assets. A wave marked
`Wait For Wave Clear` must be completely defeated before the next wave begins.
Only the four Variant prefabs in `Assets/Prefab/GameEnemy` are used by these
encounters; `Enemy_Base.prefab` is not a Test Stage spawn candidate.

Combat room prefabs expose four enemy spawn points. The Boss room exposes one
Boss spawn point and a dedicated `StageExitSpawnPoint`. Clearing the Boss
reveals the cyan `Stage Exit Cyan` portal; returning is manual rather than
automatic. If a room lacks the dedicated exit socket, runtime falls back to
its first loot spawn point.

Taking the portal calls `MapRunController.TryCompleteStageRunAndReturn()`, which
returns whether the completion was accepted. It is refused — granting nothing,
saving nothing, and consuming nothing — when `SaveManager` or `SceneLoaderSystem`
is unavailable, and `StageExitInteractable` only marks itself used on an accepted
completion, so a refused portal can be used again. An accepted completion runs
exactly once per run: completion XP, stage progress, and the return to Basement
all happen behind the same commit flag. `CompleteStageRunAndReturn()` is kept as
a void wrapper for existing callers.

Before shipping a stage, run **Tools > RB Project > Map > Validate Map Content**.
It walks every run config in the project down to enemy prefabs, room prefabs,
and the portal prefab; `Docs/VALIDATION.md` lists the rules and their severities.

A wave whose enemy pool is entirely empty slots, and an encounter with no waves
at all, are content defects. `EncounterDirector` reports them as errors naming
the encounter asset and the map node, and completes the room anyway so the run
cannot soft-lock behind exits that never unlock. Wave prefab selection skips
empty slots, so a pool holding one usable prefab among many empty ones always
spawns that prefab.

## Performance Validation

The current run configuration produces a small graph, so every visited room is
kept until the run ends. Profile both PC and mobile after visiting every node:

- revisiting nodes must not create additional room instances;
- only one room and one set of baked NavMesh data may be active;
- memory should stabilize after all reachable rooms have been visited;
- repeated backtracking should not produce growing GC allocations.

## Transient Summon World

`MapRunController.GetOrCreateSummonWorldRoot()` owns the transient hierarchy for
summons. A committed room transition despawns stationary summons and warps
mobile summons around their caster. Failed party movement rolls back without
committing the transition. `ResetRoomCache()` cleans the summon hierarchy with
`RunEnded` lifecycle notifications before destroying it, so summons never become
part of the persistent party roster or room cache.

Summon-owned pooled VFX are protected by the context capability
`PreservesOwnedVfxDuringRoomTransition`. Rollback leaves summon state and VFX
untouched; commit applies the normal stationary despawn or mobile warp policy.
Cap eviction enters the configured despawn delay and does not hard-destroy the
presentation hierarchy.
