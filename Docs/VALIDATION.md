# Validation

## Player prefab system grouping (2026-10-05)

- **Compilation:** CheckAssemblyBuild.ps1 passed on the final runtime sources:
  0 errors, 110 warnings. Unity also compiled the Editor fixture, test assemblies
  and the updated defensive-block production authoring command successfully.
- **Edit Mode:** PlayerPrefabGrouping.Tests 5/5,
  CharacterMovementIntegrationTests 17/17, and the DefensiveBlockTests filter
  31/31 runner cases passed. The defensive-block filter includes bridge executions
  of existing cases; these are not 31 distinct scenarios.
- **Play Mode:** PlayerPrefabGrouping.PlayMode.Tests 1/1,
  StateHub.PlayMode.Tests 65/65 and UnifiedWeaponPassives.PlayMode.Tests 1/1 passed.
  The first locomotion run was cancelled because the Editor toolbar imposed time
  scale 0.1; the final full run used normal time. The original toolbar settings
  and time scale 0.1 were restored afterward.
- **Prefab/runtime:** 86 existing non-Transform components retain their local
  file IDs, and 3,019 original non-null serialized references survived native
  component moves. Both referencing scenes, Prototype_Map02 and
  MossyMaterialPreview, were inspected in preview scenes without saving: each
  resolves the grouped Player with eight root components and no missing scripts.
  Tests cover all 18 input callbacks, unbound context resolution, root/nested
  pickup bodies, sibling stat providers with actor isolation, guard geometry,
  production Roma module binding/visibility, status pickup and actor reactivation.
  Existing production movement/animation and actual weapon/passive firing tests
  also passed. This is not a manual playthrough of every party command/map.

Evidence: `../../BuildArtifacts/PlayerPrefabGrouping/AssemblyBuild.log`,
`FinalTests.json`, `Migration.txt` and `SceneReferences.json` in that same
artifact directory. Existing URP shadergraph/Volume Inspector errors remain in
the Editor Console; successful compilation/tests do not certify a clean Console.

The production defensive-block authoring command now reuses the context-bound
guard and its co-located cue, so running it does not recreate them on Player's root.

## Unified weapon and character passives (2026-10-05)

- **Compilation:** required CheckAssemblyBuild.ps1 passed with 0 errors and 110
  warnings. Unity compiled runtime, Editor and dedicated test assemblies.
  Build log: ../../BuildArtifacts/PassiveUnification/build.log.
- **Automated tests:** 73 distinct tests passed: UnifiedWeaponPassives.Tests
  21/21, PassiveComposition.Tests 42/42, WeaponAffixFrameworkTests 6/6,
  UnifiedWeaponPassives.PlayMode.Tests 1/1 and
  PassiveComposition.PlayMode.Tests 3/3.
- **Runtime checks:** the production Player prefab with Roma's installed passive
  was exercised through actual weapon firing. Failed projectile preparation
  preserved Fresh Chamber charges, Momentum and Echo progress. Successful firing
  committed charges and Momentum once, captured their damage on the primary
  projectile, and produced an Echo without those primary-only bonuses. Weapon
  replacement removed its grants while preserving Roma and in-flight damage.
  Existing production Roma/HUD Play Mode checks also passed. Tests cover weapon,
  accessory and character grant coexistence, duplicate grants, persistence,
  root/nested module ownership and lazy module installation for legacy actors.
- **Migration:** all 27 existing affix assets have valid composed recipes.
  Their GUIDs and all pre-existing serialized definition fields match the saved
  baseline; repeated migration did not rewrite recipes. Baseline evidence:
  ../../BuildArtifacts/PassiveUnification/AffixBaseline.json.
- **Idle cost:** 10,000 warmed weapon-preparation probes each for empty and
  static-only configurations measured zero allocated bytes, no passive state
  and no active ticker. This is a narrow idle-path measurement, not a zero-GC
  claim for active combat; active phases, chains and area effects use snapshots.

Console inspection still showed URP VolumeComponentEditor/Bloom Inspector
SerializedObjectNotCreatableException entries and an AutodeskInteractive
shadergraph import error. These were not repaired in this change; compilation
and completed Test Runner results above do not certify a clean Editor Console.
Extended combat stress/profiling and every gameplay scene remain unverified.
Controlled runtime tests use production prefabs and isolated fixtures.

## Editable passive HUD placement (2026-10-05)

`GameplayHUD/PassiveHUD` is now a direct prefab child. The builder migrates the
previous nested host while preserving its screen rectangle; subsequent runs keep
the authored root placement. The position is independent of the HP aim layout.
Unity compiled the Editor changes successfully; no default-assembly sources
changed, so CheckAssemblyBuild.ps1 was not required for this placement change.
The authoring assertion passed both directly and through the dedicated
PassiveComposition.Tests wrapper (2/2 executions). The production Roma HUD Play
Mode test passed 1/1, including live binding, visible meshes, screen bounds and
disable/re-enable. The refreshed runtime screenshot was visually reviewed.
Documentation in PARTY_HUD.md and PREFABS_AND_AUTHORING.md reflects the new path.

## Roma passive HUD (2026-10-05)

- **Compilation:** required CheckAssemblyBuild.ps1 passed with 0 errors and 110
  warnings. Unity compiled runtime, Editor fixtures and dedicated test assemblies.
  Output: ../BuildArtifacts/PassiveHud/AssemblyBuild.log.
- **Automated tests:** 52 distinct tests passed: 38 existing PassiveComposition
  Edit Mode cases, 4 PassiveHud cases, 7 GameplayHud integration/layout/spawn
  regression cases, and 3 PassiveComposition Play Mode cases. The initial Edit
  run exposed a test lifecycle issue (Edit Mode does not invoke ordinary
  MonoBehaviour activation callbacks); the corrected HUD fixture passed 4/4.
  Visual review then found missing CanvasRenderer components on custom rings;
  the fix adds RequireComponent and prefab repair, and the final production HUD
  Play Mode test checks nonempty meshes for all four rings.
- **Runtime checks:** production Player prefab loads Roma's actual installed
  grant into the production GameplayHUD prefab. Verified 48/80 Stamina,
  Momentum 32, next-attack +64%, low-Stamina Damage +20%, visible ring geometry,
  screen bounds at 1920x1080, reservation cancellation and HUD disable/re-enable.
  Edit tests cover duplicate grants/owners, upgrade refresh preserving views,
  unequip, no UI for absent prefab, no accumulator allocation from display reads,
  actual partial Perfect Dash refund, and old-lifetime cleanup. Existing aim
  layout regressions passed at 1920x1080 and 1280x720.

Evidence: ../BuildArtifacts/PassiveHud/Validation.json and
../BuildArtifacts/PassiveHud/RomaPassiveHUD-runtime.png. The screenshot is from
the isolated Play Mode fixture using production prefabs. Extended manual combat,
all aspect ratios, and visual polish/balance are not certified by these checks.
Unity MCP intermittently timed out around domain reload; focus/auto-tick recovered
the connection. Existing URP shadergraph/Volume Inspector errors remain unrelated
to this feature; test results above came from completed Unity Test Runner runs.

## Production Roma Stamina / Momentum installation (2026-10-04)

The production definition is Assets/Character/Roma/Passive/Passive.Roma_StaminaMomentum.asset.
ChaDef.Roma selects it in the appended roma.passive.stamina-momentum slot after
the existing Ultimate. The definition, recipe and four fragment subassets have
production ownership; Unity dependency inspection reports no Assets/Tests references.
The original test fixture asset remains independent. Initial tuning matches the
four values documented in SYSTEMS/PASSIVES.md.

- **Compilation:** Unity compiled the added Editor fixture and Play Mode test
  wrapper; scriptCompilationFailed is false. No default Assembly-CSharp source
  changed in this installation, so the standalone assembly build is not required.
- **Automated tests:** ProductionRomaLoadsStaminaMomentumFromCharacterSlots passed
  1/1 in Play Mode. It instantiates the production Player prefab with the actual
  Roma definition and verifies automatic slot loading, Perfect Dash refund/dedup,
  conversion, missing-stamina damage and Momentum. No SetRuntimePassives test
  injection is used. PlayerSlideTests.StaminaRoma also passed 1/1.
- **Runtime/authoring:** two configured Roma slots; loadout validation has no
  issues. Player prefab bindings resolve through the existing context. Unity
  reserialization also materializes current schema defaults on ChaDef.Roma.
  Manual HUD/VFX presentation and gameplay balance remain unreviewed.

Evidence: ../BuildArtifacts/PassiveOverhaul/ProductionRomaInstall.json.


## Composed passive overhaul (2026-10-04)

### Compilation

- Required CheckAssemblyBuild.ps1: **0 errors, 110 warnings**. The warning set
  includes legacy serialized custom-behavior fields intentionally retained for
  migration/reference compatibility. Output is outside Assets:
  ../BuildArtifacts/PassiveOverhaul/AssemblyBuild.log.
- Unity compiled runtime, Editor fixtures and both dedicated test assemblies.
  Final EditorUtility.scriptCompilationFailed is false.
- Console still reports existing URP AutodeskInteractive shadergraph/Volume
  Inspector errors. Some MCP requests timed out around domain reload; retrying
  after enabling session auto-tick recovered the connection. These are not counted
  as test passes or compiler failures.
- Scoped C# / Markdown git diff whitespace check passed. Unity-generated YAML
  retains Unity's empty-field trailing spaces.

### Automated tests

| Unity Test Runner suite | Mode | Passed |
| --- | --- | ---: |
| PassiveComposition.Tests | Edit | 38/38 |
| PassiveComposition.PlayMode.Tests | Play | 1/1 |
| StateHub.PlayMode.Tests | Play | 65/65 |
| MeleeSkillIntegrationTests | Edit | 48/48 |
| PartyFollowUpCoreSmokeTests | Edit | 26/26 |
| ProjectileLifecycleSmokeTests | Edit | 19/19 |
| WeaponAffixFrameworkTests | Edit | 6/6 |
| **Total** | | **203/203** |

The dedicated passive fixture covers empty/stat-only owners, independent actor and
grant state, local/bound fragment variables, numeric overrides, validation/cycles,
count/cooldown refresh, actual stamina spend and refunds, one Perfect Dash refund
per execution, noncompounding/shared conversion source, conversion-only cache
revision changes, the 70% boundary, attack reservation cancellation/commit,
post-reservation earnings, unequip, FIFO/dedup/chain limits, status-success branches,
area-result quantities, world-slow recipient timing, lifetime cleanup and migration.
It also exercises real normal-shot preparation and real melee HitStart/contact
processing across multiple targets and passive removal.

Play Mode exercises frame-to-frame spending, committed Momentum and timed-buff
cleanup after removing the final grant. StateHub regressions include production
Player/Roma gradual Slide stamina and Dash behavior. Existing skill/upgrade smoke
checks run inside the passive suite.

Runner evidence:
- ../BuildArtifacts/PassiveOverhaul/TestResults.json
- ../BuildArtifacts/PassiveOverhaul/PlayModeResults.json

### Migration and runtime / authoring inspection

BaselineAssets.txt records all ten original definitions; Migration.json records
10 migrated with no errors. Definition GUIDs, names, external references and
upgrade trees remain in place. A final Unity MCP dry run sees **11 definitions,
0 pending, 0 errors**: ten migrated assets plus the opt-in Roma example.

The example has exactly four fragment subassets. Unity AssetDatabase dependency
inspection finds no CharacterStats referencing it. Production Roma was not given
the example automatically.

Unity MCP inspected Assets/Prefab/Player/Player.prefab and the defensive-block test
Player prefab: both resolve PassiveController/StatsHub on the root and
StaminaSystem/CombatEventBus in GamePlayStats_System through context bindings.
Synthetic fixtures additionally cover nested StatsHub and root passive providers.

These checks do not constitute a manual visual playthrough of every character,
pickup arc, HUD transition or slow projectile/shotgun configuration. Prepared
snapshot semantics and pooled projectile lifecycle are automated checks; production
VFX/presentation and balance still require normal playtesting. Lobby stat preview
does not evaluate composed runtime recipes and remains a documented limitation.

### Test maintenance

The dedicated assemblies bridge to typed Editor fixtures because this project
keeps gameplay in Assembly-CSharp. Run the assemblies by the exact names above.
The Play Mode wrapper invokes the real-frame fixture without introducing a player
build dependency on UnityEditor or test-only components. Test helpers create and
clean up their own objects and do not install the example on production characters.


## Distance-based Slide stamina (2026-10-04)

### Compilation

- Required `CheckAssemblyBuild.ps1` passed: **0 errors, 93 warnings**.
  Log: `../BuildArtifacts/SlideStaminaBuild.log`.
- Unity compiled runtime, Editor fixtures and test assembly successfully;
  `EditorUtility.scriptCompilationFailed` is false. Console still contains the
  existing URP shadergraph loading errors. MCP briefly disconnected during reload
  and an asset query timed out; a subsequent focused query succeeded.

### Automated tests

`StateHub.PlayMode.Tests`: **65/65 passed**. Eight added stamina cases verify
per-frame actual planar distance, current-character rate changes with nested peers,
production Player/Roma, collision-constrained distance, exhaustion and regeneration,
empty-stamina Dash handoff rejection, explicit free Slide, and tiny spending amounts.
Existing Crouch, Slide, Dash/Sprint and StateHub/animation runtime tests pass.
Results: `../BuildArtifacts/SlideStaminaPlayMode.json`.

### Runtime and authoring checks

The production Player/Roma fixture measures actual displacement against gradual
stamina loss and verifies release blending stops charging. Unity MCP confirms the
existing Roma CharacterStats asset loads `slideStaminaPerUnit = 2`. UIManager already
subscribes to `OnStaminaChanged`; the existing HUD receives every spending update.
Manual HUD visual smoothness and character-specific balance tuning remain unverified.
Movement and prefab-authoring documentation now describe the new field, zero-cost
option, regeneration pause and exhaustion behavior.

## Roma Dash to Run arm continuity (2026-10-04)

- **Asset/config:** validated the user's replacement `Roma_RolL_Forward.fbx`
  (0.6 seconds and four arm/forearm `twist_2` bones). The replacement already
  removes the earlier 91.7-degree crossfade flip. Set only the Roma profile's
  Sprint normalized start to 0 explicitly, preserving its 0.15-second fade.
  The FBX replacement itself was supplied by the user, not generated by this fix.
- **Compilation:** Unity compiled the new Editor fixture and runner registration;
  `EditorUtility.scriptCompilationFailed` was false and the compile-error Console
  query was empty. No default Assembly-CSharp source changed, so the standalone
  assembly build is not applicable to this update. Existing Inspector
  `SerializedObjectNotCreatableException` messages are separate from compilation.
- **Edit Mode:** 12/12 passed: six actual Roma rig/profile cases (forward/backward
  at 30/60/120 Hz), each discovered directly and through the runner bridge.
  Assertions cover overlapping Dash/Run weights, fade completion, and all
  shoulder/upper-arm/forearm/twist/hand rotations. Maximum sampled step across
  handoff plus the following Run cycle is 43.390/21.779/11.362 degrees at
  30/60/120 Hz respectively, without the former isolated blend flip.
  Evidence: `../BuildArtifacts/RomaDashRunEditMode.json` from the repository parent.
- **Play Mode:** `PlayerSprintTests.RomaPrefab` passed 1/1 via the Unity test
  endpoint, exercising the production Player/Roma Dash-to-Sprint path.
- **Runtime limits:** these are automated real-rig and production-prefab checks,
  not a visual playthrough of every combat pose. The backward FBX still lacks
  the newer twist bones, although its tested handoff passes. Revalidate when it
  or Run is re-exported. Updated `ANIMATION_COMMAND_FLOW.md` with the entry-pose
  and crossfade contract.

## Shared pair-offset evaluation during fades (2026-10-04)

- **Compilation:** required `CheckAssemblyBuild.ps1` passed with 0 errors and
  93 warnings. Log: `P:/Game_RB_Project/BuildArtifacts/PairOffsetBuild.log`.
  Unity compiled the Editor and test changes; the compile-error Console query
  returned no entries. URP Volume Inspector reload exceptions remain separate.
- **Edit Mode:** `CommanBlockRunnerTests` passed **34/34**. Coverage includes
  evaluated mixer child weights, simultaneous base/action fades, interrupted
  three-state fades, absent profiles, order-independent rotation accumulation,
  preview/runtime evaluator parity, capture protection, full-body suppression,
  manual evaluation, pause, disable and rebind cleanup.
- **Play Mode:** `PairOffsetPlayModeTests.RomaRigDuringInterruptedSlideFade`
  passed **1/1** through the Unity MCP `unity` test endpoint. The `unityMCP`
  endpoint lost its callbacks during domain reload and reported initialization
  timeout; its status is not counted as a passing Test Runner result.
- **Actual rig:** instantiated `Assets/Character/Roma/Roma.fbx` with the Roma
  animation profile and existing offset SO. Checked the sampled bone rotation
  plus expected correction for 55 manual graph evaluations, including exit,
  reversal and graph speed 0.2 during a fade. CSV evidence:
  `P:/Game_RB_Project/BuildArtifacts/PairOffsets/RomaEditMode.csv` and
  `P:/Game_RB_Project/BuildArtifacts/PairOffsets/RomaPlayMode.csv`.
  Slide weight grows from 0.11111 to 1 over its authored 0.15-second fade;
  correction grows from about 9.40 to 84.57 degrees. At graph speed 0.2, the
  outgoing weight and correction both decrease proportionally.

The rig checks use real assets with manually evaluated graphs. They are not an
end-to-end movement/input test or a visual review of all clips. Production
prefabs, profiles, offset values and scenes were not edited by this change.

## Dash to Slide handoff (2026-10-04)

Ctrl can now interrupt a grounded, moving Dash immediately and inherit its actual
planar displacement velocity. It does not require waiting for Sprint. Airborne
or wall-blocked Dash admission fails without canceling Dash or queuing a Slide.

### Compilation

- `CheckAssemblyBuild.ps1`: **0 errors, 93 warnings**. Evidence:
  `P:/Game_RB_Project/BuildArtifacts/DashSlideHandoffBuild.log`.
- Unity compiled runtime, Editor and test sources successfully. Console still
  contains the existing URP shadergraph and Volume inspector reload errors.

### Automated tests

- `StateHub.PlayMode.Tests`: **56/56 passed**. Five added handoff cases cover
  same-frame Dash entry, nested controller, production Player/Roma, airborne
  rejection and a Dash stopped against a wall. Existing Crouch, Slide, Sprint,
  StateHub and animation runtime regressions also pass.
- `CrouchSlideClipsAndFallback`: **2/2 passed** in Edit Mode.
- Evidence: `../BuildArtifacts/DashSlideHandoffPlayMode.json` and
  `../BuildArtifacts/DashSlideHandoffAnimation.json`.
- Fixture teardown now also runs through NUnit teardown, preventing a failed
  nested coroutine from leaving physics objects or input settings behind.

### Runtime checks

The production Player/Roma Play Mode fixture verifies exact entry momentum,
unchanged stamina at handoff, removal of Dash invincibility, no delayed Dash
completion/Sprint restart, and immediate `Roma_Slide` playback without root motion.
These are automated runtime checks; unrestricted combat and visual hit-zone
alignment still need manual playtesting. Crouch idle/move clips remain fallback.

Updated movement, animation-flow and prefab-authoring documentation for the new
Dash admission rule, which supersedes the original no-entry-during-Dash plan.

## Roma Slide animation binding (2026-10-04)

- Assigned the new `Roma_Slide.fbx` clip to `Roma_AnimProfile.slide` through Unity.
  Enabled Loop Time; playback speed 1 and fade 0.1 seconds. The source rig remains
  Generic, consistent with Roma's existing animations. Root/trajectory curves are
  stationary and endpoint curve differences are negligible for looping.
- Unity compilation passed after updating the existing Editor fixture to assert
  the real production Slide clip. No default Assembly-CSharp source changed, so
  the standalone assembly build was not required for this asset-binding update.
- `PlayerSlideTests.RomaPrefab` **1/1 passed** in Play Mode: actual Slide clip,
  looping, advancing playback without restarts, aim/fire coexistence, no gameplay
  root motion, and Ctrl left/right input. Evidence:
  `P:/Game_RB_Project/BuildArtifacts/RomaSlideClipPlayMode.json`.
- Sampled the clip on Roma's model in an isolated preview scene; image:
  `P:/Game_RB_Project/BuildArtifacts/RomaSlidePreview.png`. Crouch idle/move clips
  remain unassigned and use fallback. Full motion-to-hit-zone visual alignment
  across combat poses is not covered by this binding check.
- Console retains URP shadergraph/Volume inspector errors around Editor reloads;
  these are not C# compile failures. Updated movement and prefab-authoring docs.

## Crouch and momentum Slide (2026-10-04)

### Compilation

- Required `CheckAssemblyBuild.ps1` passed: **0 errors, 93 existing warnings**.
  The initial sandbox invocation could not read Windows SDK discovery paths;
  the same script passed with authorized SDK access. Final evidence:
  `P:/Game_RB_Project/BuildArtifacts/CrouchSlideBuild.log`.
- Unity compilation completed successfully, including Editor fixtures and the
  dedicated Test assembly. Editor was left stopped and not compiling.

### Automated tests

- **Play Mode 51/51 passed** in `StateHub.PlayMode.Tests`: 25 Slide, 5 Crouch,
  18 existing Sprint, and 3 animation/StateHub lifecycle tests. Covers actual entry
  velocity, camera-only yaw, lateral steering, opposing brake, diagonal speed,
  duration, release blending, successful/failed Dash, walls, pause/UI/focus/disable,
  stun/down/death, ledges, root/nested peers, head clearance and shape ownership.
- **Movement Edit Mode 18/18 passed**: 17 integration tests plus the ownership
  coroutine matrix, which enters Play Mode and exercises 18 ownership scenarios.
- **Animation 2/2 passed**: the Crouch/Slide clip and fallback contract runs both
  directly and through its existing dedicated-assembly bridge. Verifies idle/move/
  slide selection, no repeated clip restart, fallback and no root-motion takeover.
- **StateHub Edit Mode 19/19 passed**, including fault-isolation contracts.
- Evidence is saved in `P:/Game_RB_Project/BuildArtifacts/` as
  `CrouchSlidePlayMode.json`, `CrouchSlideMovementRegression.json`,
  `CrouchSlideAnimationRegression.json`, and `CrouchSlideStateRegression.json`.

### Runtime and prefab checks

- The production Player prefab is bound through Unity's prefab API to the new
  `Inputmaneger` Crouch action. The Roma Play Mode case verifies both Ctrl keys
  through PlayerInput's persistent UnityEvent, aim/fire while sliding, layer-0
  fallback and disabled gameplay root motion. Its temporary input settings bypass
  Editor focus filtering only inside the fixture and are restored afterward.
- Physics fixtures verify low ceilings, safe standing, a walkable step and a
  10-degree ascending ramp using the actual controller and vertical motor. These
  are automated runtime probes, not manual traversal of every production map.
- Console inspection found expected fault-injection logs from passing StateHub
  tests, plus URP shadergraph loading/Volume inspector exceptions during Editor
  reload. These package/editor diagnostics were not changed by this work; the
  Console is not claimed to be empty.
- At this original validation, crouch/slide clips were unassigned. Ordinary
  locomotion is the temporary fallback. Final pose quality, upper-body masking
  with those future clips, and visual/hit-zone alignment remain unverified until
  the real clips are authored. No Jump or slope acceleration was added.

Updated movement, context, animation-flow and prefab-authoring documentation.

## Sprint follows camera yaw with delayed turning (2026-10-04)

### Compilation

- Required `CheckAssemblyBuild.ps1`: **0 errors, 93 warnings**.
- Unity finished compiling runtime, Editor fixture, and test assembly sources;
  `scriptCompilationFailed` is false.
- Evidence: `../BuildArtifacts/SprintCameraSteeringBuild.log`.

### Automated tests and runtime coverage

- **Play Mode 21/21 passed** in `StateHub.PlayMode.Tests`: 18 Sprint cases plus
  StateHub lifecycle/Dash ownership and animation frame-order coverage.
- Camera yaw now steers Sprint with a 0.25-second smoothing time and a
  360-degree/second maximum turn speed. Tests cover gradual turns, 180-degree
  reversal, the shortest turn across +/-180 degrees, rotation locks, root/nested
  peers, unchanged heading under movement-key input, and existing stop conditions.
- The production Player prefab with Roma follows camera turns at 90/180/270
  degrees while retaining the same looping `Roma_Run` playback state. Body facing
  matches travel direction; releasing Sprint restores ordinary locomotion.
- This supersedes the fixed-world-travel behavior recorded in the older entries.
  Runtime coverage is automated; a manual traversal of production stairs/slopes
  remains unverified.
- Updated movement and prefab authoring documentation. Test evidence:
  `../BuildArtifacts/SprintCameraSteeringPlayMode.json`.

## Sprint uses one run clip for every facing direction (2026-10-03)

- Removed the local-forward threshold from live Sprint presentation. Entry and
  updates play the assigned sprint clip in every direction; turning keeps the
  same playback state and time. The directional mixer resumes when Sprint ends.
- Required assembly check: **0 errors, 93 warnings**. Unity compilation completed.
- Targeted Edit Mode: **2/2 passed** (direct and adapter registrations), checking
  all eight movement directions retain Roma_Run and release restores the mixer.
- Production Player/Roma Play Mode: **1/1 passed**, including camera turns at
  90/180/270 degrees, fixed world travel, unchanged run playback, and release.
- Updated animation flow and authoring documentation. Evidence:
  `../BuildArtifacts/SprintSingleClipBuild.log`, `SprintSingleClipEditMode.json`,
  and `SprintSingleClipPlayMode.json`. These targeted checks do not rerun the
  unrelated AvatarMask cleanup failures recorded below.

## Roma Dash to Sprint (2026-10-03)

### Compilation

- Required `CheckAssemblyBuild.ps1`: **0 errors, 93 warnings** after the final
  runtime changes. The sandbox initially denied access to installed SDK metadata;
  the same required script succeeded with approved elevated access.
- Unity compiled the runtime, Editor fixture and discoverable test assembly.
  No generated project or validation tooling was edited.
- Evidence: `../BuildArtifacts/SprintAssemblyBuild.log`.

### Automated tests

- **Play Mode 19/19 passed**: 16 PlayerSprintTests plus the existing StateHub
  lifecycle/Dash and animation frame-order tests. Covers tap, hold, failed/canceled
  dash, rotation with fixed travel, immediate release steering, root/nested peers,
  straight/angled walls, fire, suspension, UI, stun, down/death, disable/re-enable,
  and the production Player prefab loading Roma and its looping sprint clip.
- **Edit Mode StateHub 7/7**, **movement integration 17/17** passed.
- **Animation smoke 114/126 passed**. The new Roma sprint playback/fallback test
  passed through both direct and adapter registrations. Six existing model/profile
  rebind cases fail through both registrations (12 results): the unchanged
  `CharacterAnimationPresentation.DisposePresentationLayers()` assigns a null
  AvatarMask, which the installed Animancer layer mixer rejects. These cases run
  with Sprint disabled and fail before live locomotion updates. That cleanup
  path was not modified as part of Sprint.
- Earlier Sprint runs exposed re-enable latch handling and a wall check that
  included ordinary walking after Sprint stopped. The latch now requires release;
  wall clearance includes controller skin, and the fixture disables ordinary
  movement input before checking that Sprint does not slide around the corner.
- MCP results are saved in `../BuildArtifacts/SprintPlayMode.json`,
  `SprintStateEditMode.json`, `SprintMovementEditMode.json`, and
  `SprintAnimationEditMode.json`. The native callback reused its filename across
  runs; `SprintAnimationEditMode.xml` contains the final **movement** run only.
  Use the explicitly named JSON reports for the other runs.

### Runtime and authoring checks

- Production Player prefab uses the existing Movement_System context binding and
  Shift Dash action. Roma's profile references the imported `Roma_Run` clip with
  Loop Time/Loop Pose and root locks; no additional prefab component is needed.
- The production-prefab Play Mode test verifies Dash -> Sprint -> directional
  locomotion, animation time advancing beyond a full 0.5-second loop, and no
  gameplay root motion during Sprint.
- An additional live Editor probe observed `IsSprinting=true`, world direction
  `(0,0,1)`, looping `Roma_Run`, and `RootMotionActive=false`. A paused visual
  capture used a suspended actor to isolate the pose. Evidence:
  `../BuildArtifacts/SprintProductionRuntime.json` and `RomaSprint.png`.
- The original RectorDefensiveBlock scene is restored after the probe. A visual
  traversal of production stairs/slopes remains unverified. Sprint now keeps the
  single run clip for every facing direction; the directional mixer resumes on exit.
- Existing URP AutodeskInteractive shadergraph/Volume Inspector errors remain
  separate from source compilation and the Sprint test results.

## Ally follow initializer order (2026-10-03)

- Production Test Stage 03 inspection found Ally 2 (`AllyAI`) with null Ctx:
  its initializer ran after the Selector. Ally 1 (`Aires_Behavior`) had valid Ctx
  and was initially within the existing follow distance.
- Repaired and saved `AllyAI` and the same unsafe ordering in `MinigunTerretAI`
  outside Play Mode using the native migration. All seven Subtrees pass the
  context validator, retaining their original node GUIDs. Reopening and saving
  AllyAI through BehaviorDesignerWindow retains initializer 2 before Selector 3.
- Unity compiled the Editor/test sources with no C# errors. Runtime sources
  were unchanged; the preceding assembly build result remains applicable.
- Targeted Play Mode test `LegacyAllyTreesFollowMovingPlayer`: **1/1 passed**
  (two production graphs). It verifies actor-local Ctx and NavMesh movement both
  toward a distant player and after that player changes position, with the
  formation task's player, Ctx and InCombat bindings intact.
- Native Test Runner XML finished at 07:46:50 UTC and is authoritative. The MCP
  job timed out during initialization/domain reload, but the test subsequently
  ran and passed. The first fixture attempt used manual Tick without Manual
  update mode and failed; that fixture was corrected before the passing run.
- Actual MapRun/Test Stage 03 check: both allies bind their own Ctx and Player.
  After Player moves away, Ally 2 moves from (0.90, 0.01, 1.50) to
  (5.71, -0.79, 1.18), and Ally 1 moves from (-0.90, 0.01, 1.50) to
  (6.81, -0.79, 1.18). Moving Player back makes both follow again, to
  (1.50, 0.01, 1.48) and (2.01, 0.01, -1.48), respectively.
- Evidence: `../BuildArtifacts/AllyFollow/PlayMode.xml` and
  `ProductionMovement.json`. Editor play options are restored. Play Mode is
  left running in Test Stage 03 with Ally 2 selected.

## CanAct blocked by initializer order (2026-10-03)

- Production inspection found active M_GR_01 trees with null Ctx and an
  initializer at node 35, after the autonomy Selector. CanAct correctly failed
  because initialization had not run.
- Repair keeps the initializer first under its Sequence and to the left of its
  sibling behavior. Existing node GUIDs and behavior values are retained.
  Migration can repair already migrated graphs; validation also checks horizontal
  placement before Graph Designer can save an unsafe sibling order.
- Unity compiled the changed Editor/test sources with no source errors. Runtime
  Assembly-CSharp sources were unchanged, so the runtime assembly build check
  from the preceding validation remains applicable.
- Targeted **Edit Mode 13/13** and **Play Mode 13/13** passed, zero skipped.
  Edit Mode adds a regression for an existing behavior left of zero, an
  initializer moved to the right, idempotent repair, GUID preservation and
  disabled-node remapping. The native Test Runner XML is authoritative.
- The actual P0 asset was loaded and saved through BehaviorDesignerWindow,
  then deserialized: initializer remains node 2, before the behavior Selector
  at node 3; Owner Context validation passes.
- All 7 Subtrees and 4 inline prefab trees pass validation after repair.
- Production check restarted from GameSetup, loaded the same Test MapRun config
  and entered main_01 (Sewer_Cross). Three P0 enemies received distinct Ctx values
  with matching CombatState owners. All three CanAct reevaluations returned
  Success; Patrol positions changed between observations and NavMesh velocities
  were nonzero, with no recorded navigation failure.
- Editor is left in Play Mode, paused, with a P0 enemy tree selected for inspection.
- Evidence: `../BuildArtifacts/CanActOrder/EditMode.xml`, `PlayMode.xml`,
  `ProductionMovement.json`.

## Shared AI owner context (2026-10-03)

### Compilation

- Required `Assets/Scripts/CheckAssemblyBuild.ps1`: **0 errors, 93 warnings**.
- Unity 6000.0.58f2 compiled the runtime, Editor tools and test assemblies.
  Final `scriptCompilationFailed=false`, `isCompiling=false`; Console has no
  `error CS` entries. The existing URP shadergraph import issue remains separate.

### Automated tests

- **Edit Mode: 134/134 passed, 0 skipped**: P0/context (12), target sensor (26),
  movement integration (17), party formation (7), skill transactions (25),
  special-point reaction (27), stagger (10), and special-point smoke (10).
- **Play Mode: 13/13 passed, 0 skipped**: P0/context (10) and target sensor (3).
  The ownership bridge also executes all 18 existing movement ownership cases.
- Context cases cover missing/wrong bindings without fallback lookup, deferred
  CombatState initialization, repeated cycles, full restart, pause/resume, pooled
  life changes, module replacement and event subscription counts.
- The native nested-Subtree fixture verifies two actors sharing one parent asset
  while receiving their own Ctx in child graphs through variable overrides.
  Its assets are prepared before Play Mode through Test Runner PrebuildSetup.
- Formation checks verify task-bound InCombat, abort cleanup and preservation of
  an external movement suspension. P0 checks retain orbit/burst, reload, stalled
  navigation and recovery coverage.
- The navigation-stuck fixture now waits for game time rather than 300 frames:
  fast Editor runs could exhaust that frame count before the 1.5-second policy.
- Final Play Mode job: `ba0e6d02b5b040c282759886e679a33f`.
  Results are recorded using Unity Test Runner's native ResultsSavingCallbacks;
  XML is authoritative when the MCP job callback is lost during domain reload.

### Runtime and asset checks

- All **7 Subtrees** and **4 inline prefab trees** deserialize with one exposed
  Graph Ctx, default null, complete task bindings and valid initialization order.
  The native project validator also checked **96 scenes** in preview scenes.
- A before/after native deserialization comparison retained every original node
  GUID, type, enabled state, abort setting and task behavior value in all 11
  graphs; only context bindings, explicit formation InCombat and graph indices
  required by inserted initialization nodes are excluded from value comparison.
  Local prefab references are compared by their file IDs rather than transient
  instance IDs or temporary-copy asset GUIDs.
- The 11-prefab binding audit retained the prior sensor/driver references,
  including existing null bindings. Common declarations now belong to
  CharacteContext; central resolution handles unique optional peers.
- Consumer source inspection found no owner hierarchy lookup; remaining lookup
  calls concern targets/player objects or bootstrap resolution.
- Play Mode fixtures exercised runtime behavior in the Editor. A visual
  walkthrough of every production encounter was not performed.
- Existing UnknownTask nodes in legacy EnemyAI/MineAllyAI and the Ally prototype
  remain as before migration; this work does not repair those missing task types.
- The original active scene was preserved and temporary validation assets were
  removed. Test Runner's enter-play-mode setting was restored.
- Evidence: `../BuildArtifacts/OwnerContext/AssemblyBuild.log`, `EditMode.xml`,
  `PlayMode.xml`, `FinalGraphAudit.json`, `BehaviorPreservation.json`,
  `InlineBehaviorPreservation.json`, and `PrefabBindingBaseline.json`.

## P0 shooting AI (2026-10-03)

### Compilation

- Required `CheckAssemblyBuild.ps1`: **0 errors, 93 warnings**. Default runtime
  sources are scanned normally; no generated project or validation-tool changes.
- Unity 6000.0.58f2 compiled runtime, Editor authoring/bridge and isolated test
  assemblies. Final `scriptCompilationFailed=false`; source compilation errors
  from intermediate fixture setup were corrected before the passing runs.
- The pre-existing URP `AutodeskInteractive.shadergraph` import error can still
  appear in Console around asset imports/Play Mode transitions; it is unrelated
  to P0 and is not reported as a source compilation failure.

### Automated tests

- **Edit Mode: 50/50 passed, 0 skipped**: `AICombatP0Tests` (7),
  `AITargetSensorTests` (26), `CharacterMovementIntegrationTests` (17).
  Final MCP job: `e6f4d86d6664491f8fefc1ef2226ef58`.
- **Play Mode: 11/11 passed, 0 skipped**: `AICombatP0RuntimeTests` (8) and
  `AITargetSensorRuntimeTests` (3). The P0 ownership bridge invokes all **18**
  unchanged `CharacterMovementOwnershipTests` scenarios with their original
  SetUp/TearDown while already in Play Mode.
- Final Play Mode MCP job: `c6e3aa8f7e0648dc8a3c63cd48b0b036`. Its completion
  callback was lost on domain reload, so the passing result is taken from native
  Test Runner XML, exported with Unity's existing `ResultsSavingCallbacks` via
  MCP. No project validation tooling was modified. The native XML contains every
  test name/result, including the ownership bridge.
- Earlier runs exposed fixture speed/FirePoint setup errors and an Editor-only
  platform constraint on the new Play Mode asmdef. These were corrected. An
  intermediate ownership matrix attempted EnterPlayMode during a fixture compile
  error and failed its NavMesh setup; the final 18-scenario bridge passed.
- Evidence: `../BuildArtifacts/P0AI/FinalAssemblyBuild.log`,
  `FinalEditMode.xml`, `FinalPlayMode.xml`, and `AuthoringAudit.json`.

### Runtime and authoring checks

The Play Mode fixtures build real NavMesh data and execute actual task lifecycles;
the two-actor fixture runs the saved Subtree through real BehaviorTree components.
Validated:

- Actual orbit followed by burst; LOS blockage stops held fire; CanAct interruption
  releases it; two actors using one Subtree have separate runtime state.
- Deterministic candidate selection, blocked/no firing point, partial/disconnected
  path rejection, invalid/recycled target life, and navigation failure/quota.
- Frozen last observation, one search consumption per observation, real reload
  completion, no firing while reloading and Failure without reserve ammo.
- Pause/movement-lock do not count as stuck; world-slow clocks honor actor
  exemptions; movement and rotation cleanup preserve a newer motor owner.
- Sensor regression covers live target eligibility, Taunt, pooling, memory/LOS,
  prefab references, clock behavior and warm-scan allocation checks.
- Unity MCP inspected `Enemy_M_GR_01 Variant` context/Sensor/agent/health/weapon
  bindings and its Subgraph. The saved graph has 34 nodes, three Graph variables
  and a valid Start connection. Asset GUID remains
  `00121cc81c970b148a4f454934ec4db0`. Fixture peers exercise root and nested layouts.

These are instrumented runtime checks, not a visual playthrough of every authored
combat room or animation asset. Production level geometry and final animation
appearance remain manual acceptance checks; no claim is made for that visual pass.

## Party Follow-Up rename (2026-10-01)

Renamed the event-driven Party Combo system to Party Follow-Up, including types,
input actions/callbacks, authoring menus, HUD labels, asset folders and documentation.
Basic Melee Combo Skills and the legacy Chain Attack keep their existing contracts.

### Compilation

- Required `CheckAssemblyBuild.ps1`: **0 errors, 93 warnings** after updating the
  camera partial's renamed field. Log: `../BuildArtifacts/FollowUpRename/AssemblyBuild.log`.
- Unity recompile reported completion without source errors after the rename and
  Test Runner bridge were added. Subsequent Editor requests timed out.

### Automated tests

`PartyFollowUpRunnerTests` exposes the existing core, placement, presentation,
camera, HUD and saved-upgrade fixtures to Test Runner. The requested Edit Mode
run timed out after 300 seconds without returning results. Cancel/status requests
also timed out; no passing test count is claimed for this migration.

### Assets and runtime checks

- All **39** tracked script/folder asset GUIDs from the pre-rename snapshot still
  resolve on disk. The two Input Action IDs and both authored definition/upgrade
  IDs remain stable; Player and test-prefab UnityEvents name the new callbacks.
- Compared serialized reference lists against the captured originals: 18 of 19
  inspected files were identical. GameplayHUD adds nine GameObject name overrides
  on its nested display prefab; its existing references were retained.
- `FormerlySerializedAs` maps renamed authoring fields; `MovedFrom` records renamed
  Unity types. The upgrade save slot remains `party-combo`.
- A live reference audit and Play Mode check could not complete because Unity's
  main-thread MCP calls timed out. Rerun `PartyFollowUpRunnerTests` and
  **Tools > Validation > Validate Party Follow-Up Skills** once the Editor responds.
- Audit evidence and the original touched files are under
  `../BuildArtifacts/FollowUpRename/`; `ValidateFollowUpRename.cs` in the parent
  BuildArtifacts directory contains the prepared Unity reference audit.

## Shared animation playback and simple state lifecycle (2026-10-01)

`AnimationPlayback` now owns per-clip events, callback/VFX cleanup and timing
options. Named simple states share action/locomotion bases; Skill/Chain retain
phase/request rules. Reload's helper, the separate action admission class and
CommanBlock lease wrapper were removed. Public/serialized authoring, priority,
End Time, Cancel and update order remain as before. The command-flow document
includes a minimal state example.

### Compilation

- Required `CheckAssemblyBuild.ps1`: **0 errors, 93 warnings** after the final
  runtime edits. Evidence: `../BuildArtifacts/AnimationSimplification/FinalBuild.log`.
- Unity runtime, Editor and test assemblies compiled; `scriptCompilationFailed`
  and `isCompiling` were false. No generated project/tooling changes were needed.
- Existing URP `AutodeskInteractive.shadergraph` import errors remain; the
  inconsistent Screen healing prefab import and TimeSlowManager shutdown issue
  are tracked separately. This refactor does not claim to fix them.

### Automated tests

- **Edit Mode: 169/169 passed, zero skipped**. The previous 159 cases plus ten
  new cases ran through `AnimationTransitionRegressionRunnerTests`,
  `CommanBlockRunnerTests` and `SkillCastArchitectureTests`.
  Evidence: `EditMode.xml`, `FinalEditResults.json` (job
  `e8b9e93384134d8788d90c560cf21d4b`).
- **Play Mode: 3/3 passed, zero skipped**: same-frame Live/Crawl intent and
  smoothing, StateHub actor/reload lifecycle and UI/Dash ownership. Evidence:
  native TestRunner `PlayMode.xml`. A temporary Editor result observer exported
  XML across domain reload and was removed afterwards.
- New tests cover slot replay/End Time, repeated Stop, stale Stop when another
  slot reuses the same Animancer state, authored-end reentry, timeline subscriber
  reentry, exception isolation, shared transitions and VFX across actors,
  Skill cutscene-to-main, Dead/StageIntro pose holding, and Dash/Knockback timing.
  Existing pose, binding, priority, cancellation, Reload and temporary-state
  checks remained enabled. Fixtures now call the owning Presentation module or
  actual bound OnEnd instead of removed private forwarding methods.
- Intermediate compile/fixture failures were corrected and rerun. One accidental
  unfiltered run used previously loaded assemblies while source compilation was
  failing and reported failures across unrelated suites; it is not counted as
  validation. The final results above are the scoped, freshly compiled runs.

### Runtime and visual checks

Fresh Unity MCP probes in `RectorDefensiveBlock.unity` after the final runtime edit:

- **Roma:** CommanBlock, replay during fade, shooting and world slow 0.2.
  Across 234 samples, all 114 outgoing fade samples had movement/rotation
  released; final full-body weight was zero. Actor graph speeds reflected their
  world-slow policy. Evidence: `RuntimeRoma.json`.
- **Aires/Feno:** upper/full-body Reload, repeated SpecialReaction Cancel and
  invalidation during a new reaction. Each interruption was delivered once.
  Final callbacks, VFX and pending cleanup were zero; movement/rotation and both
  navigation update flags were restored. Feno was loaded via the actual runtime
  definition/model replacement path. Evidence: `RuntimeAlliesSamples.json`.
- **Rector:** Skill approach/hold and temporary struggle using its MiniStun clip
  because the scene has no authored struggle clip. Cancel retained 19 visible
  outgoing fade samples, then destroyed the temporary playable and cleared
  pending cleanup. Evidence: `RuntimeEnemyStruggle.json`.
- **Fallback/watchdog:** missing SpecialReaction clip completed via its fallback;
  a frozen graph then completed via its watchdog. A frozen Chain cutscene also
  completed once with root motion released. Evidence:
  `RuntimeSpecialFallbackWatchdog.json`, `RuntimeChainWatchdog.json`.
- **Fixed/manual pose:** 107 rendered frames included 51 distinct evaluation
  generations; each applied generation matched. A paused normal graph remained
  at generation 76, manual Evaluate advanced to 77, and resume continued at 78.
  Evidence: `RuntimeAutomaticFixed.json`, `RuntimeFixedEvaluation.json`.
- **Defensive Block:** one successful command/impact, Player/Ally HP 900/1250,
  guard ended, Rector root motion off and navigation position ownership restored.
  Evidence: `RuntimeDefensiveBlockResult.json`.
- **Visual:** inspected automatic Roma command pose, intermediate fade weight
  0.5499, and Idle at weight zero. Evidence: `Captures/RomaLive0.png` through
  `RomaLive2.png`, `RuntimeLiveCaptures.json`. This is representative visual
  sampling, not a review of every authored clip/camera angle.

All evidence in this section is under
`P:/Game_RB_Project/BuildArtifacts/AnimationSimplification/`. The Editor was
returned to stopped Edit Mode, the prior time scale 0.1 restored, and temporary
runtime models/profile substitutions and the result observer removed. No scene,
profile or prefab migration was made for this change. The full long-running
DefensiveBlock matrix and every authored Skill/Chain/cutscene visual remain
outside these representative checks.

## Animation module separation and lifecycle fixes (2026-09-30, validated 2026-10-01)

Binding, presentation, session coordination, timeline/VFX and signal dispatch have
separate internal owners. Driver/Brain run at Update orders 1000/1100. Pair offsets
restore before graph evaluation and apply once per evaluated pose. Explicit
Skill/Utility/Chain cancellation reports one interruption after cleanup.

### Fix found during validation

On the real Roma rig, a second CommanBlock used End Time `1` instead of the
profile's `0.436716437`. Animancer 8.4 assigns transition events when creating a
state; it does not restore them on every replay. Our cleanup correctly detached
callbacks but replay then created an empty event sequence. Presentation now
restores the transition's authored sequence before each action/locomotion binding.
A regression test failed on playback 2 before the fix and passes after it. Separate
coverage checks a repeated locomotion SpecialReaction after cancellation.

Two fixture issues were also corrected without weakening assertions: the moving
bone test now uses distinct clips for its two Pair Offset profiles (null clip keys
previously overwrote one profile); the staged melee guard calls the same
`ResolveActiveGuardOffset` initialization as actual arrival. The latter resolves
the previously documented `-0.55` versus `-1.95` fixture failure.

### Compilation

- Required `CheckAssemblyBuild.ps1` after the replay fix: **0 errors, 93 warnings**.
  Log: `P:/Game_RB_Project/BuildArtifacts/AnimationArchitecture/ReplayFixBuild.log`.
- Unity compiled runtime, Editor and test assemblies. `scriptCompilationFailed`
  was false. Generated projects and the build validation script were not edited.
- Console is **not clean**: the existing URP `AutodeskInteractive.shadergraph`
  import errors remain. A Play Mode shutdown also reported a leftover
  `TimeSlowManager`; this wider shutdown issue was observed but not attributed to
  this animation change or fixed here. No animation source compilation error was
  found. See `FinalRuntimeConsole.json` in the artifact directory.

### Automated tests

- **Edit Mode: 159/159 passed**, zero skipped, after the final runtime change.
  Suites: `AnimationTransitionRegressionRunnerTests`, `CommanBlockRunnerTests`,
  and `SkillCastArchitectureTests`. The regression bridge includes Brain smoke,
  transition priority, root-motion collision, Melee, Block animation ownership,
  and DefensiveBlock fixtures. Evidence: `EditFinal.xml`, `EditResults5.json`.
- **Play Mode: 3/3 passed**, zero skipped, after the final runtime change:
  Live/Crawl same-frame movement and smoothing; StateHub reload/actor lifecycle;
  UI/Dash ownership. Evidence: `PlayFinal.xml`.
- MCP lost its Play Mode job callbacks during domain reload and reported a false
  initialization timeout. A temporary Editor TestRunner callback exported the
  actual native results to XML across reloads. That observer was removed after
  validation; no test assertions were skipped or suppressed.

Coverage includes reentrant cancel, repeated cancel, same-ID replay, stale/authored
callbacks, binding invalidation, destroyed model cleanup, throwing subscribers,
terminal ordering, VFX lifetime, root-motion policy publication, Reload modes,
action overlap, weighted bone rotations, full-body coverage, manual/paused
animation evaluation, temporary-state retirement and importer execution orders.

### Runtime and visual checks

Checked through Unity MCP in `RectorDefensiveBlock.unity` after the replay fix:

- **Roma:** real CommanBlock, replay during fade, shooting after completion and
  world slow `0.2`. Across 152 samples, movement/rotation were already released
  in 23 outgoing fade frames; final full-body weight was zero and root motion
  stayed off. Player graph speed was `1`; Aires, Feno and enemy graphs were `0.2`.
  Evidence: `RuntimeRomaAfterFix.json`.
- **Aires/Feno:** upper/full-body Reload respectively, repeated SpecialReaction
  cancel, then invalidation during a new reaction. Each cancellation was delivered
  once; final callback/VFX/cleanup counts were zero, movement/rotation allowed,
  root motion off and navigation updatePosition/updateRotation restored.
  Feno was loaded through the runtime definition/model path on Ally 2, also
  exercising actual model replacement. Evidence: `RuntimeAlliesSamples.json`.
  These profiles have no CommanBlock clip; their authoring was not changed.
- **Enemy:** actual Rector skill/approach/hold followed by a temporary struggle
  using its existing MiniStun clip (this scene has no authored struggle clip).
  Cancel preserved the frozen outgoing pose as its weight decreased, then
  destroyed the playable and reduced pending cleanup to zero.
  Evidence: `RuntimeEnemyStruggle.json`.
- **Fixed/paused evaluation:** a separate Roma model rig configured Fixed before
  graph creation produced 50 evaluations across 115 rendered frames; applied
  generations always matched, without extra application on unevaluated frames.
  A paused graph kept its generation constant, one manual evaluation advanced it,
  and resume restarted evaluation. Evidence: `RuntimeAutomaticFixed.json` and
  `RuntimeFixedEvaluation.json`. Changing a running graph from Normal to Fixed
  does not test this: Animancer documents that runtime switch as unsupported.
- **Defensive Block integration:** the real skill/command sequence succeeded once;
  Player/Ally HP remained 900/1250, guard ended, enemy root motion ended and
  navigation position ownership returned. Evidence: `RuntimeDefensiveBlockResult.json`.
- **Visual sampling:** inspected Roma from command pose through an intermediate
  fade weight `0.5487` to Idle at weight zero during automatic playback. Images
  `Captures/RomaLive0.png` through `RomaLive2.png` and timing
  `RuntimeLiveCaptures.json` are in the artifact directory. Also inspected the
  scene with Roma/Aires/Feno/enemy visible. These are representative captures,
  not an exhaustive visual review of every authored animation and camera angle.

Root/nested peers were exercised by the Player/Ally model layout and root-level
Rector, plus the automated binding/cleanup cases. No production prefab or profile
migration was made. The scene was returned to stopped Edit Mode; temporary runtime
rigs/model substitutions and the result observer were removed. The pre-existing
runtime time scale `0.1` was restored after checks run at scale `1`.

Remaining scope: the complete long-running DefensiveBlock harness matrix and a
visual review of every Skill/Chain/Melee/cutscene clip were not run. Automated
priority/lifecycle coverage and the specific runtime checks above must not be
reported as an exhaustive gameplay or visual pass. The shutdown Console issue
listed above also remains open.

## Shared animation fades and fixed masks (2026-09-30)

`CharacterAnimBrain.Transitions.cs` owns presentation playback, layer fades and
temporary pose cleanup. CommanBlock now uses a separate full-body action layer,
respects its authored End Time and keeps its mask throughout fade-out. Block
keeps its sampled pose frozen on exit; temporary enemy struggle poses survive
until their outgoing blend reaches zero. Pair offsets follow each contributing
action state and the coverage of the full-body layer.

### Compilation

- Required `CheckAssemblyBuild.ps1`: **0 errors, 93 warnings**. Log:
  `P:/Game_RB_Project/BuildArtifacts/AnimationTransitions/Build.log`.
- Unity compiled the runtime, Editor and test changes;
  `EditorUtility.scriptCompilationFailed` was false.

### Automated tests

- `CommanBlockRunnerTests`: **16/16 passed**. Coverage includes authored End Time,
  actual interpolation of a leg excluded by the upper-body mask, action overlap,
  replay during fade, weighted pair offsets, delayed temporary-state destruction,
  reclaiming a state, disable/rebind cleanup, control lock ownership and Block pose
  preservation.
- `AnimationTransitionRegressionRunnerTests`: **45/46 passed** across the existing
  `CharacterAnimBrainSmokeTests` and `CharacterAnimationTransitionPolicyTests`.
  The priority matrix, root-motion ownership and playback lifecycle checks passed.
  `CancelSkillCastRequestClearsStateWithoutATerminalEvent` failed because it expects
  no signal but receives `Skill:Interrupted:101`. The before snapshot already calls
  `InterruptActiveSkillRequest()` in `CancelSkillCastRequest`; this method was not
  changed by the fade work. The test and the cancellation paragraph in Animation
  Command Flow disagree with the existing implementation and need a separate
  contract decision.
- Results: `P:/Game_RB_Project/BuildArtifacts/AnimationTransitions/TestResults.json`.

### Runtime and prefab checks

- Inspected all 11 production prefab Brain layer indices: locomotion/action are
  0/1, so the new runtime full-body layer uses 2 without a serialized migration.
- In `Assets/Tests/DefensiveBlock/RectorDefensiveBlock.unity`, invoked the live
  Roma Driver's CommanBlock command. The real clip reached authored normalized
  End Time `0.436716437`; full-body weight then fell through intermediate values
  to zero over the configured `0.1` second fade. Both masks stayed unchanged,
  locomotion remained active and `applyRootMotion` remained false.
- Repeated with world slow `0.2`: Roma retained graph speed `1`, the Aires allies
  and enemy used `0.2`, and Roma's outgoing layer still faded to zero. The temporary
  slow handle was released and world scale returned to `1`.
- Captures: `RuntimeRoma.json` and `RuntimeWorldSlow.json` in the artifact folder
  above. The original scene remains clean and in Play Mode as it was before this
  task. No production clips, profiles or prefabs were modified by this change.
- These checks measure live graph behavior and synthetic bone interpolation.
  Full visual review of every character/clip, including Feno, and end-to-end
  combat input sequences remain unverified. Console retains PrefabPalette and
  URP asset/tool errors; it is not globally error-free.

## Animancer 8.4.0 import (2026-09-30)

Imported `Packages/com.kybernetik.animancer` from `origin/v-asset` commit
`d7510a2bc13649a9a6352e744109e1573ac74683`. Three obsolete 8.2.3 files and
their metadata were removed; see [Animation Command Flow](ARCHITECTURE/ANIMATION_COMMAND_FLOW.md#animancer-package).
Backups and SHA-256 hashes of the 17 pre-existing changed files are under
`P:/Game_RB_Project/BuildArtifacts/Animancer840Import`. All 17 hashes were
unchanged after import. Project gameplay sources were not edited by this import.

### Compilation

- Unity 6000.0.58f2 loaded package version `8.4.0` and assembly version
  `8.4.0.39`; `isCompiling` and `EditorUtility.scriptCompilationFailed` were both
  false after refresh. Runtime and Editor assemblies compiled.
- The default-assembly check was not run because this import changed only
  package sources and documentation, not default `Assembly-CSharp` sources.
- Console also reported FBX import errors, missing dictionary keys
  `Noemi_Body`, `Feno_Body`, and `Milano_Binoculars`, and URP
  `AutodeskInteractive.shadergraph` load errors. The URP errors were present
  before import. The FBX errors require separate investigation; they are not
  evidence of successful runtime validation.

### Automated tests

Requested Edit Mode suites: `SkillManagerRefactorTests`,
`CommanBlockRunnerTests`, `SkillCastArchitectureTests`, and
`SkillPlacementTimingTests`. The first job timed out before starting tests.
A retry with a 120-second initialization timeout encountered MCP command
timeouts and a disconnected Unity session. No completed results were obtained.
These suites still need to be run successfully in Unity Test Runner.

### Runtime checks

Full Play Mode gameplay and visual animation playback were not verified.
The Editor successfully loaded `AnimancerComponent` from the new assembly.

## CharacterSkillManager responsibility split (2026-09-30)

`CharacterSkillManager` now delegates loadout resolution and runtime-entry caches
to `CharacterSkillLoadoutRuntime`. `SkillChargeRegistry` owns shared pools;
`SkillCastAnimationBinding` owns pending playback subscriptions. Cast orchestration
and its public request/result types live in their own files. Existing component
APIs and serialized fields retain their names.

### Compilation

- Required `CheckAssemblyBuild.ps1`: **0 errors, 93 warnings**. Windows SDK access
  needed the same approved escalation as earlier checks. Final log:
  `P:/Game_RB_Project/BuildArtifacts/SkillManagerRefactorBuild.log`.
- Unity compile, including Editor helpers and dedicated tests: **0 errors**.

### Automated tests

| Suite | Result |
| --- | --- |
| `SkillManagerRefactorTests` | **12/12** |
| `SkillCastArchitectureTests` | **17/17** |
| `SkillCastTransactionSmokeTests` | **25/25** |
| `SkillGemDefinitionTests` | **40/40** |
| `MeleeSkillIntegrationTests` | **23/24** |
| `SkillPlacementTimingTests` | **13/13** |
| `TargetedDeliverySmokeTests` | **21/21** |
| `CompositeSkillPayloadEditorTests` | **9/9** |
| `SkillChargeAndBarrierSmokeTests` | **26/26** |
| `HelperSkillLoadoutSmokeTests` | **14/14** |
| `HelperRuntimeExecutionSmokeTests` | **2/2** |
| `HelperBaseStatsLoadoutSmokeTests` | **14/14** |
| `CombatLoadoutRunLockTests` | **2/2** |
| `SkillLoadoutUiTests` | **8/9** |
| `PartyFollowUpCoreSmokeTests` | **11/11** |

Total: **237/239**. Full results:
`P:/Game_RB_Project/BuildArtifacts/SkillManagerRefactorTests.json`.
Final suite summaries, including the rerun after fixture cleanup:
`P:/Game_RB_Project/BuildArtifacts/SkillManagerRefactorSummary.json`.

The two failures are outside the extracted code:

- The previously recorded Defensive Block Contact test still expects Z `-0.55`
  and receives `-1.95`.
- `SkillLoadoutUiTests.UnmappedCharactersDoNotInventCombatOptions("Aires")`
  expects zero Active/Ultimate options; the current Aires asset supplies two.
  This test reads the asset directly through `SkillLoadoutDescriptorFactory`,
  without the manager/loadout runtime. Its fixture, factory and asset were not
  changed by this refactor.

The new suite verifies shared charges after slot reassignment and save reload,
Helper variant cancellation and cooldown preservation (same/different execution
definitions), stale request signals after slot/disable/death cancellation,
root/nested modules, and external playback retaining its request and owner.

### Play Mode and prefab checks

- **6/6 Play Mode smoke checks passed** in an empty temporary scene: StandardSkill
  shared charges, nested modules, Helper variant switching during wind-up, external
  playback used by Helper/Chain callers, stale-signal cancellation, and BasicMelee
  combo advance/interruption. These checks evaluate real Animancer graphs explicitly.
  They do not cover a full level, input/HUD interaction, or an end-to-end Chain sequence.
- The opt-in Editor harness schedules checks across domain reload through
  `SkillManagerRefactorTestHarness.SchedulePlayModeSmoke()`, captures logged errors
  as failures, writes `P:/Game_RB_Project/BuildArtifacts/SkillManagerRefactorPlayMode.txt`,
  and exits Play Mode. Use an empty scene when running this harness.
- The initial smoke run exposed fixture teardown destroying the animation profile
  before disabling its actor. The fixture now disables the actor first; the final
  smoke run has no test exceptions. Console retains the existing URP
  AutodeskInteractive shadergraph load errors.
- Unity asset inspection confirms `CharacterSkillManager` script references and
  `autonomousSlots` / `chainAttackSkill` serialized fields on Player, Ally Stryker,
  Ally Helper and Enemy Base prefabs. Root/nested resolution is also covered by
  the fixtures. No production prefab or skill asset migration was performed.
- The original `Assets/Tests/DefensiveBlock/RectorDefensiveBlock.unity` scene was
  restored, clean, with the Editor stopped after the smoke run.

## Skill cast architecture repairs (2026-09-30)

This verification supersedes the test counts and Inspector labels in the earlier
architecture follow-up below. See [SKILL_SYSTEM.md](SYSTEMS/SKILL_SYSTEM.md) for
the current reservation and validation contracts.

- Failed cast startup, release callbacks, and payload exceptions settle reserved
  charge and energy. Observer exceptions are logged individually so later
  observers and resource accounting still run. A committed cast remains committed.
- Reservations capture one read-only upgrade snapshot and defensive stats copies;
  changing selected upgrades during wind-up affects the next cast.
- `DeliveryRelease`, including inside Composite payloads, must occur strictly after
  StandardSkill Cast Point. BasicMelee may use marker time zero.
- Batch validation, Skill Tree validation, and the Inspector resolve execution
  modes from asset bindings. Assets used in both modes are checked in both.
  The Inspector header now labels its selected mode **Preview As / Preview Status**.

### Compilation

- Required `CheckAssemblyBuild.ps1`: **0 errors, 93 warnings**. The check passed
  with approved Windows SDK access; output is in
  `P:/Game_RB_Project/BuildArtifacts/SkillCastArchitectureBuild.log`.
- Unity recompile, including Editor helpers and the dedicated test assembly:
  **0 compiler errors**.

### Automated tests

| Suite | Result |
| --- | --- |
| `SkillCastArchitectureTests` | **17/17** |
| `SkillGemDefinitionTests` | **40/40** |
| `SkillCastTransactionSmokeTests` | **25/25** |
| `TargetedDeliverySmokeTests` | **21/21** |
| `CompositeSkillPayloadEditorTests` | **9/9** |
| `SkillPlacementTimingTests` | **13/13** |
| `MeleeSkillIntegrationTests` | **23/24** |

Total: **148/149**. The remaining failure is the same existing
`ContactMeleeInterceptsAtCloseRangeWithoutTimedApproach` case described below
(expected Z `-0.55`, actual `-1.95`). Defensive Block production code and its
assertion were not changed.

New coverage includes exceptions before and after commit, real energy-pool
observers, immediate and animated casts, direct Cast cleanup, upgrade changes
during wind-up, stats mutation, direct/Composite Delivery timing, and assets bound
through Combo or animation profiles with optional StandardSkill usage.

### Runtime and asset checks

- Integration tests exercise actual Animancer graphs in Edit Mode. Full scene
  gameplay in Play Mode and manual Inspector interaction remain unverified.
- A fresh usage-aware Unity AssetDatabase audit of `Assets/Data` passes **31/37**
  definitions, including all **7 Combo roots**, both production Composites, and
  `Milano.Skill_3` under the Delivery timing rule. The same six incomplete assets
  listed in the earlier section remain invalid; no new asset failures appeared.
- The active `RectorDefensiveBlock` test scene is stopped and clean. Temporary
  test assets were removed by fixtures; production assets were not migrated.

## SkillGemDefinition architecture follow-up (2026-09-30)

This follow-up supersedes the timing and validation entry-point details in the
earlier review below. The current contract is documented in
[SKILL_SYSTEM.md](SYSTEMS/SKILL_SYSTEM.md#validation-contract).

- Shared `SkillValidationIssue` diagnostics carry severity, code, owning skill,
  Combo step, payload location, and upgrade gate. Inspector, asset validation,
  Skill Tree authoring, melee validation, and cast startup use the common rules.
- StandardSkill requires Taunt markers strictly after Cast Point. BasicMelee
  executes before playback and accepts marker time 0. Standalone Inspector
  **Validate As** previews either execution kind without serializing a runtime mode.
- Explicit execution/Combo accessors prevent casting stale Combo root payloads.
  Facing, placement and continuation consumers resolve through the skill's root.
  Composite placement rejects competing or gated providers unless explicitly
  configured on the root. Existing serialized policy fields remain intact.
- Cast startup validates before animation/resource reservation. Resource reservation
  remains an independent accounting operation. Invalid definitions that previously
  produced only partial effects now fail startup; the asset findings below matter
  for gameplay and should be corrected with intentional authoring values.

### Compilation

- Required `CheckAssemblyBuild.ps1`: **0 errors, 93 warnings**. The sandbox could
  not read the Windows SDK directory; the same check passed with approved access.
- Unity recompile, including Editor and the dedicated test assembly: **0 compiler
  errors**. Console still reports the existing URP AutodeskInteractive shadergraph
  load error; no package/shader changes were made in this work.

### Automated tests

| Suite | Result |
| --- | --- |
| `SkillGemDefinitionTests` | **34/34** |
| `CompositeSkillPayloadEditorTests` | **9/9** |
| `TargetedDeliverySmokeTests` | **21/21** |
| `SkillCastTransactionSmokeTests` | **25/25** |
| `SkillPlacementTimingTests` | **13/13** |
| `MeleeSkillIntegrationTests` | **23/24** |

The definition suite includes the existing node-authoring and unified-validation
smoke suites, covering preservation of root policies during conversion and
distinct attribution of identical errors on gated and ungated payloads. It also
checks real asset ownership, repeated markers, cyclic malformed composites,
placement order independence, and rejection before resource reservation.

The remaining legacy failure is unchanged from the earlier review:
`ContactMeleeInterceptsAtCloseRangeWithoutTimedApproach` expects root-motion Z
`-0.55`, receives `-1.95`; its guard fixture does not initialize the guard forward
offset. No production Defensive Block implementation or assertion was changed.

### Runtime and asset checks

- Edit Mode integration tests evaluate actual Animancer graphs. BasicMelee starts
  through `MeleeController` and the cast orchestrator, arms a Taunt listener, and
  applies the status at marker **0** and **0.2**, before authored Cast Point **0.4**.
  StandardSkill also delivers a marker after Cast Point when both are crossed
  within one evaluated frame.
- Unity AssetDatabase audit under `Assets/Data`: **31/37** definitions have no
  blocking issues. Both production Composites (Aires Skill 3 and Feno Minigun
  Terret) and all **7 Combo roots** pass, including referenced child ownership.
- Six existing definitions have incomplete authoring; their data was not guessed
  or rewritten. With the new cast-start validation, these are rejected until fixed:

| Asset (under `Assets/Data`) | Blocking configuration |
| --- | --- |
| `PartyFollowUp/Test/Test.Feno.FollowUpTauntExecution.asset` | Missing `TauntApply` at this audit; repaired in the 2026-10-01 Ally Combo camera investigation below |
| `Skills/Aires/Aires_Active.asset` | Missing `TauntApply` |
| `Skills/Aires/Aires_Skill_2.asset` | Morph status entry 0 has no Status Effect |
| `Skills/Enemies/Mons.GR004/Skill_1_M_GR04.asset` | Missing `HitStart` and `HitEnd` |
| `Skills/Feno/Feno.Skill_Ulatimate.asset` | Missing `TauntApply` |
| `Skills/Roma/Skill.Def.Roma_Ultimate.asset` | Missing `HitStart` and `HitEnd` |

Full gameplay in the open scene and manual Inspector interaction remain unverified;
the animation checks above run in Edit Mode. Temporary test assets are removed by
their fixtures. No production asset migration is required or performed.

## SkillGemDefinition validation and Combo authoring (2026-09-30)

- `CollectExecutionValidationIssues` now checks payload configuration and required
  timeline markers for both normal skills and `ValidateMelee` steps. Invalid
  steps are rejected before melee playback, with the step number/ID in the error.
- Taunt requires each `TauntApply` marker strictly after Cast Point. Composites
  propagate the requirement; hitbox events keep their existing timing rules.
  Combo Inspector summaries and the embedded-payload validator support roots
  with no payload and validate the referenced execution skills.
- Compilation: required `CheckAssemblyBuild.ps1` passed with **0 errors,
  93 warnings**. Unity compiled the runtime, Editor, and dedicated test assembly
  without compiler errors. The first sandboxed build could not read the Windows
  SDK directory; the approved retry completed successfully. Console also contained
  unrelated URP `AutodeskInteractive.shadergraph` load errors.
- Automated tests through Unity MCP: `SkillGemDefinitionTests` **16/16 passed**
  in `SkillGemDefinition.Tests`; `SkillPlacementTimingTests` **13/13 passed**;
  `CompositeSkillPayloadEditorTests` **9/9 passed**. New runtime assertions reuse
  the existing typed rig helpers in `MeleeSkillIntegrationTests` through reflection
  because asmdef tests cannot reference default-assembly gameplay types directly.
- Existing melee regression cases: **23/24 passed**. The remaining failure is
  `ContactMeleeInterceptsAtCloseRangeWithoutTimedApproach` at the guard motion
  assertion: expected Z `-0.55`, actual `-1.95`. This check reaches guard movement
  after the combo starts; its fixture manually sets the guard active/arrived but
  does not initialize `activeGuardForwardOffset`. No guard implementation or
  expectation was changed as part of this task.
- Runtime checks used evaluated Animancer graphs in Edit Mode: invalid combo
  steps cannot enter playback; a real Taunt payload applies its status to an enemy
  both across separate evaluated frames and when one frame crosses Cast Point
  `0.4` and TauntApply `0.4001`. The graph must be evaluated once at zero delta
  after creating the state so its initial time is applied before measuring frames.
  Full gameplay in Play Mode and a visual Inspector interaction pass remain
  unverified; the checks above exercise isolated fixtures and Inspector state data.
- Asset checks: all seven existing `Assets/Data/Combat/ComboSkills` roots passed
  `ValidateMelee`. Aires Skill 3 and a representative Combo root also passed the
  Editor validator, including their embedded ownership graphs. No skill assets
  were migrated or edited.

## Per-skill party command placement modes (2026-09-30)

- `SkillGemDefinition` exposes Placement Mode, No Target Placement, Target
  Distance and Player Distance under Gameplay > Party Command Placement.
  Existing assets retain Player Target / In Front Of Player / 3 m defaults.
  Player-relative modes ignore selected enemy handles, capture Player pose and
  forward, prefer a valid current pose, and otherwise use the existing fade/cast path.
- `CheckAssemblyBuild.ps1`: **0 errors, 93 warnings**;
  `../BuildArtifacts/PartyPlacementModesBuild.log`. Unity compiled successfully.
  Serialized fields and defaults were inspected on the production Aires skill asset.
- Unity Test Runner Edit Mode: **37/37 passed**, job
  `84faf6f847034eb0ac37021e13ff19f9`. Added mode cases with/without selected enemies,
  both fallbacks, per-skill distance/facing, frozen aim after Player movement,
  in-region casts without fade, blocked ring alternatives and complete rejection.
  HUD Active/Ultimate and Helper-style explicit execution retain their existing
  cost/cleanup coverage with the new modes.
- Boss Rush Play Mode, using temporary skill copies and held AI/target selection:
  Aires Around Player warped to a 3 m radius, committed and jumped 3.348 m;
  Feno In Front Of Player warped 3 m forward and committed. Both released aim.
  The real Helper manager accepted an Around Player command at 2 m, committed,
  reported success and hid the Helper. Player had a selected enemy throughout;
  player-relative plans carried no enemy handle. Helper used a temporary copy of
  Feno's turret skill to validate summon placement independently of Milano's
  healing-recipient requirement. Evidence: `../BuildArtifacts/PartyPlacementModesLive.txt`.
- Runtime probes call execution APIs, not physical keys. They do not verify every
  payload/character combination. No production skill asset was reassigned to a new
  mode. Console retains existing AutodeskInteractive shadergraph load errors.

## Fade around player-commanded ally warps (2026-09-30)

- Field ally commands now wait for the existing visibility controller to fade out,
  warp while hidden, and fade in before starting the skill. In-range commands skip
  the transition; Helper summon appearance is unchanged. Pending commands reserve
  the member/placement, reject repeated requests, and restore visibility/ownership
  when the actor or target becomes invalid.
- `CheckAssemblyBuild.ps1`: **0 errors, 93 warnings**;
  `../BuildArtifacts/PartyCommandFadeBuild.log`. Unity compiled successfully.
- Unity Test Runner Edit Mode: **25/25 passed**, job
  `cfadceea4ad44c02b53d1fb014a84ec4`. Both HUD columns and both skill kinds verify
  departure/arrival ordering, no skill charge before arrival, and repeat rejection.
  Coverage also includes target loss during either fade, disable cleanup, in-range
  casts without fades, and preserving the Helper's ongoing summon appearance.
- Live `RectorDefensiveBlock` Play Mode used the real HUD column 2 method with Aires
  and a captured enemy target. Frame samples showed dither increasing to 1 at the
  original pose, relocation while hidden, then dither decreasing to 0. The actual
  cast started at **0.362 s**, after arrival completed; the skill committed, jumped
  **3.314 m**, and released its aim override. Existing scene controls held AI still;
  target selection was temporarily held and restored. Play Mode was stopped after
  the probe, returning to the initial Editor mode without saving scene changes.
  Evidence: `../BuildArtifacts/PartyCommandFadeLive.txt` and `PartyFade-*.png` in
  the same directory. The before/fade-out/hidden-arrival/fade-in captures were
  visually inspected. Physical keyboard presses and every character model were
  not exercised by this probe.
- Console retains existing AutodeskInteractive shadergraph/import warnings and
  fixture warnings from models without VFX; the live Aires model has Dither enabled.

## Keep an in-range ally at its current position (2026-09-29)

- Active player-directed casters first validate their current pose. They keep
  exact XYZ and only turn when the sampled placement point reaches the target
  without overshoot, the target line is clear, and body/trajectory/NavMesh/
  reservation checks pass. Otherwise the existing warp search runs. No-target
  commands and inactive helper summons retain their existing placement behavior.
- `CheckAssemblyBuild.ps1`: **0 errors, 93 warnings**;
  `../BuildArtifacts/PartyCommandInRangeBuild.log`. Unity compiled successfully.
- Unity Test Runner Edit Mode: **21/21 passed**, job
  `64edc2d0c0354e00b28aefd6742a335b`. New cases cover exact position retention
  including Y and cast facing, out-of-range warp, a wall blocking the target,
  and an inactive helper near the target still using summon placement.
- Shared placement and Combo regression fixtures: **28/28 passed** through
  direct Editor fixture invocation. The first invocation encountered the loaded
  Rector test scene's floor in two origin-based cases. Temporarily disabling
  pre-existing scene colliders during the fixtures removed that interference;
  every collider was restored in `finally`. Report:
  `../BuildArtifacts/InRangePlacementRegression.txt`.
- Live `RectorDefensiveBlock` probe: Aires used the real HUD column 3 route from
  a pose within its sampled skill reach. Immediate position displacement was
  **0 m**, the skill committed, root Y rose **3.316 m**, and aim cleanup completed.
  AI and target selection were held stable for the probe and restored afterward.
  Evidence: `../BuildArtifacts/PartyCommandInRangeLive.txt`. This exercises the
  HUD method directly; physical keyboard input and every skill are not covered.

## Aires jump on player-directed commands (2026-09-29)

- Directed slot and explicit-skill requests incorrectly forced planar root motion,
  which zeroed the animation's Y delta. Both paths now preserve authored vertical
  movement. Placement uses the full sampled trajectory and aligns only XZ at the
  contact point, leaving the starting pose grounded.
- `CheckAssemblyBuild.ps1`: **0 errors, 93 warnings**;
  `../BuildArtifacts/PartyCommandJumpBuild.log`. Unity compiled successfully;
  Console retains the existing AutodeskInteractive shadergraph load errors.
- Unity Test Runner Edit Mode: **17/17 passed**, job
  `a79e1c50d1f246d1a5517d451bf81ebf`. Two new real Animancer graph checks verify
  directed battle-slot and explicit-skill requests start with active root motion
  and `PlanarOnly == false`. First run timed out before initialization; retry passed.
- Live Boss Rush probe called the column 2 HUD path with production Aires skill
  and a committed boss target. Sampled jump peak was 3.129 m. Runtime actor Y
  rose from 0.063 to 3.398 m (3.336 m rise), then landed at Y 0.048. The cast
  committed and its aim override cleared. Evidence:
  `../BuildArtifacts/PartyCommandJumpY.txt`. Probe froze AI for repeatability;
  temporary state was discarded on leaving Play Mode. Other clips and physical
  keyboard input were not exercised by this jump probe.

## Party HUD keys 2/3 use directed placement (2026-09-29)

- Root cause of the remaining live report: keyboard tap/hold calls
  `PartyHudPresenter.Cast`, which still called `TryStartCastSlot` directly.
  The earlier PartyCommandController probes did not cover this HUD route.
  Both HUD ally columns now use directed placement and retain the selected
  Active/Ultimate slot, charges and costs (`useCommandSkill: false`).
- The same scene exposed stationary Feno placement inside the boss's larger
  body at the default 1.5 m stand-off. Placement now includes target/caster body
  clearance. The targeted HUD command subsequently warped Feno from
  (-28.25, 0.03, -0.62) to (-3.49, -0.21, 0.75) and committed its cast.
- `CheckAssemblyBuild.ps1`: **0 errors, 93 warnings**;
  `../BuildArtifacts/PartyHudWarpBuild.log`. Unity compiled without C# errors.
- Unity Test Runner: **15/15 passed**, job
  `da561e0fc3ea4350a8c6cfcde3a44ccd`. Four new tests call the actual HUD
  `Cast` entry point for both columns and both skill kinds, with/without a target,
  and assert movement, selected-slot cooldown and aim cleanup. Another new
  test verifies clearance around a large target for a stationary skill.
- In the user's paused Boss Rush scene, the HUD column 3 call warped Feno
  9.77 m and started its turret skill. Aires initially had 1.58 seconds of
  cooldown remaining. After resuming, column 2 warped Aires and committed
  its skill. Both managers reported commit events. This was a direct HUD call,
  not a physical keyboard press. Evidence: `../BuildArtifacts/PartyHudKeysLive.txt`.
- Existing `PartyHudTests` direct invocation: **8/9 passed**. The unchanged
  prefab-structure assertion expects the authored combo root to be inactive,
  but the current prefab stores it active. Runtime binding still hides absent
  offers and that test passes. No prefab was modified for this fix.
  Report: `../BuildArtifacts/PartyHudRegression.txt`.

## Party command live placement correction (2026-09-29)

- Reproduced failed placement in Play Mode using the production Boss Rush room
  and Aires. The 5 cm NavMesh search rejected valid starts at actor-root height;
  the animated position capsule also penetrated the floor by roughly 1–2 cm.
  Commands now ground their starts within 1 m and use the locomotion controller
  footprint when available. Untargeted lunges start ahead of the Player instead
  of subtracting the clip's travel and appearing behind the Player.
- Final `CheckAssemblyBuild.ps1`: **0 errors, 93 warnings**; log:
  `../BuildArtifacts/PartyCommandGroundingBuild.log`. Unity compiled successfully.
  Console still contains the existing URP AutodeskInteractive shadergraph load errors.
- Unity Test Runner Edit Mode: **10/10 passed**, job
  `2836d2f207bc420fb75c54de5802aa6d`. Includes below-surface actor roots and a
  nested animated collider extending below the floor, with and without a target.
  The first request timed out before initialization; the retry ran all tests.
- Live Play Mode exercised `PartyCommandController.TryExecutePartyCommand`
  with production actors, animations and payloads. AI was held still, party
  actors protected, and the Player's target captured explicitly for repeatability.
  Command definitions were changed only in memory for these probes.
  Aires warped and committed `Aires_Skill_1` against the boss, reducing HP from
  100,000 to 99,667.19. Without a target, Aires started 2.999995 m ahead of the
  Player and committed its skill. Blocked or unsupported poses were rejected.
- Helper explicit commands warped with a target and without one. A runtime-only
  Feno turret skill assignment also exercised the Helper Command Slot path.
  The untargeted turret command committed successfully, hid the helper and
  cleared its aim override. Evidence: `../BuildArtifacts/PartyCommandLiveRuntime.txt`.
- Configuration limitation: the current `ChaDef.Milano` helper has no configured
  manual command entry. `Milano.Skill_3` is targeted healing delivery: it committed
  with a recipient but rejected release without one. The point-target warp does
  not change payload recipient rules. Production skill/character assets were not
  changed to fill this slot. Keyboard/UI input and every character/skill combination
  were not covered by these runtime probes.

## Player-directed party skill placement (2026-09-29)

- `CheckAssemblyBuild.ps1`: **0 errors, 93 warnings**. Log:
  `../BuildArtifacts/PartyCommandSkillBuild.log`. Unity also compiled the new
  runtime and dedicated test assembly successfully. Console reported unrelated
  URP AutodeskInteractive shadergraph load messages during refresh.
- Unity Test Runner, Edit Mode: `PartyCommands.Tests` **8/8 passed**. Covers a
  solid floor, Player-forward fallback, captured target and authored stand-off,
  changed selection, recycled target, blocked/off-NavMesh placement, competing
  aim ownership, one immediate payload execution and cooldown rejection.
  The same final fixture also passed direct invocation; report:
  `../BuildArtifacts/PartyCommandSkillTests.txt`.
- Existing `HelperBaseStatsLoadoutSmokeTests`, `SkillCastTransactionSmokeTests`
  and `PartyFollowUpPlacementTests`: **52/52 passed** through direct Editor fixture
  invocation with setup/teardown. Their Test Runner requests timed out before
  starting; these 52 results are not Test Runner results. Report:
  `../BuildArtifacts/PartyCommandRegression.txt`.
- Inspected Stryker/Helper prefab reference layouts through Unity: the field
  member lives under `ChianSystem`, so the command passes that resolved member
  explicitly. An additional immediate-cast check with a nested field member
  verified placement, successful execution, released reservation and cleared aim.
- Initial targeting-fixture failures were missing Edit Mode lifecycle setup.
  Adding a real floor also exposed conservative fallback-footprint ground
  overlap; the command now uses an above-ground fallback body without extra
  broad-phase padding. The final grounded fixture passes all eight checks.
- Live Play Mode animation, root-motion contact, Helper appear/disappear and
  production damage remain unverified: the checks above run in Edit Mode and
  use an immediate probe payload for execution, not a production animated cast.

Use this document for local C# validation. These rules are project policy.

## Multiple Block Windows (2026-09-20)

- Added optional ordered windows, step bindings and per-window Interrupt Skill /
  Continue Skill outcomes. Legacy profiles keep their original single window.
- Editor checks passed **42/42**: 4 new window/damage/step-suppression checks,
  14 timeline draft/save/Undo/ownership checks, and 24 existing Block checks.
  Report: `../BuildArtifacts/BlockWindowsEditorTests.txt`.
- `CheckAssemblyBuild.ps1`: **0 errors, 80 warnings**, in
  `../BuildArtifacts/BlockWindowsBuild.log`.
- Play Mode probes use a cloned Rector Skill/profile, restore guard settings in
  finally, and reposition Rector on NavMesh between attack opportunities to model
  a second incoming attack. They do not redesign or persist Rector's original
  one-way charge. Initial fixture failures included movement below NavMesh and
  waiting real time without allowing enough gameplay frames; the initial report
  is retained as `BlockWindowsPlayProbe.Initial.txt`.
- Final instrumented Play Mode probes passed twice consecutively: first-window
  Continue keeps the same request and restores root motion without enemy knockback;
  second-window Interrupt produces the second success and one knockback. Commit
  and release occur once. Missing the first window preserves the damage already
  taken and still permits a successful second-window block without later HP loss.
  Report: `../BuildArtifacts/BlockWindowsPlayProbe.txt`; reproducible probe code:
  `../BuildArtifacts/BlockWindowsPlayProbe.cs.txt` (run only in the test scene).
- An earlier run under severe Editor stalls aborted an accepted approach on skill
  playback termination. The diagnostic reruns passed with 0.333 s frame deltas;
  this is not a guarantee that an approach survives external interruption or clip
  completion. No success is awarded on abort. Broader frame-rate soak testing and
  the full GameSetup -> Basement -> BossRush route remain unverified this round.
- Exited Play Mode and confirmed the original `GameSetup` scene was restored with
  18 roots and its pre-existing unsaved state. No scene save was performed.

## Defensive Block ready sound (2026-09-19)

- Added optional `GuardSetting.readyCue`; `DefensiveBlockReadyCue` plays it when
  the actionable on-screen flare appears and stamps caster/request/life to avoid
  repeated playback while the same selected attack remains active. Dim and
  offscreen signals do not announce readiness.
- Created `BlockOpen.asset` through the Editor API using
  `RB_Project_BlockOpen_SFX.mp3`, global 2D, Sfx category, non-looping. Confirmed
  the saved reference and preserved the current 0.22 s approach and impact cue.
- `CheckAssemblyBuild.ps1`: **0 errors, 80 warnings** in
  `../BuildArtifacts/DefensiveBlockReadyAudioBuild.log`; scoped diff check passed.
- No runtime audio result is claimed: the regression scene switch was skipped
  because GameSetup was dirty. Brief Play Mode entry was stopped; returned to
  GameSetup in Edit Mode without saving or replacing the dirty scene.

## Defensive Block timed approach prototype (2026-09-18)

- Before implementation, committed the existing immediate-contact Block work as
  `5bc78176` (`Checkpoint immediate defensive block before timed approach`).
  Unrelated material, package, prefab and save changes were excluded.
- Added the timed approach implementation and `ValidateTimedApproach` in the test
  harness. The authored C# defaults are 0.5 s and a 1.6 m attacker stand-off. A zero
  duration selects the previous physical flow and its existing validation suite.
- `CheckAssemblyBuild.ps1` passed with **0 errors, 80 warnings** on the final source:
  `../BuildArtifacts/DefensiveBlockTimedApproachBuild.log`. `git diff --check` passed.
- Unity recovered from its earlier ILPP stall without restarting the Editor. The
  new assembly loaded, and GuardSetting/RectorCharge were explicitly saved through
  the Editor API with 0.5/1.6. No scene or third-party package was changed for this.
- Editor smoke/contact-order/grounding checks passed **24/24**.
- The initial Play Mode run reported 12 endpoint failures while all impacts,
  animation progression, single cost/release, HP and other 25 cases passed. A
  focused probe confirmed zero endpoint error inside the Impact callback: the
  test had sampled on the next coroutine tick, after knockback displacement.
  Corrected the test to sample in that callback and retained the initial report as
  `../BuildArtifacts/DefensiveBlockTimedApproachValidation.Initial.txt`.
- The corrected Play Mode suite passed **37/37**, recorded in
  `../BuildArtifacts/DefensiveBlockTimedApproachValidation.txt`. At 4/6/8 m, early
  and late inputs produced one impact after 0.494–0.543 real seconds for the 0.5 s
  setting and 1.022–1.079 seconds for 1 s. Animation advanced without root motion,
  endpoint checks passed, costs/releases occurred once, and both actors kept HP.
  Player fallback, no-Block damage, late rejection, ten interruption/reset cases,
  a new obstacle, pause/world slow, unrelated damage and 15 FPS also passed.
- Inspected Game View at mid-approach and Impact: Aires is ahead of Player in
  Loop, then Impact/VFX and Rector knockback activate once with HP 1250/1250.
  Captures: `../Temp/DefensiveBlockValidation/TimedApproachMid.png` and
  `../Temp/DefensiveBlockValidation/TimedApproachImpact.png`.
- After the visual trial, verified reaction, reservation and camera all released;
  returned to Edit Mode in the original clean regression scene without saving it.
- Existing Opsive/Burst BC1055 errors remain in the Editor; this scene pauses automatic AI,
  so its results do not establish that the full GameSetup/Basement/BossRush route
  or unrestricted enemy AI is validated.

## Defensive Block immediate-contact restoration (2026-09-18)

- Removed the rejected minimum-impact-delay experiment and restored synchronous
  contact → stop hitboxes/playback → knockback/Impact/HitLag/VFX. Begin can skip
  Loop on contact again. No attacker Contact animation or extra movement scope
  remains, and GuardSetting no longer exposes the experiment's delay field.
- Preserved grounded guard clips, landing support validation, camera/fades, Player
  fallback and the rule rejecting guard after Player has already been hit.
  Rector preparation remains disabled (`windupSeconds = 0`).
- Editor smoke/contact-order/grounding checks passed **24/24**.
- Rollback Play Mode regression passed Player fallback **11/11**, contact ordering
  **3/3**, and continuous motion/normal damage without Block. The first 4 m Begin
  trial missed; a focused rerun passed all three Begin/arrival checks and early
  commands at 4/6/8 m (impact 0.283–0.293 real seconds, one success, no HP loss).
  Preserve the initial failure as evidence rather than treating it as a passing run.
  Reports: `../BuildArtifacts/DefensiveBlockRevertDelayValidation.txt` and
  `DefensiveBlockRevertDelayContactRecheck.txt`.
- Final `CheckAssemblyBuild.ps1`: **0 errors, 80 warnings** in
  `../BuildArtifacts/DefensiveBlockRevertDelayBuild.log`. Returned to Edit Mode in
  the original clean regression scene; no scene save was needed.

## Defensive Block minimum impact interval experiment — reverted (2026-09-18)

The user rejected the feel of this experiment. Runtime now resolves actual guard
contact immediately again. The minimum-delay setting, attacker Contact phase,
movement scope and experiment-only tests were removed. Rector windup stays zero.
The results below describe the reverted experiment, not current behavior.

- `GuardSetting.minimumImpactDelaySeconds = 0.5` measures accepted input to reaction
  in the defender's actor clock. Early real contact stops the matching hitboxes and
  skill motion, then reuses the charge clip as moving Block Contact presentation.
  `RectorCharge.windupSeconds` remains zero. No new animation asset was introduced.
- Initial Play Mode checks passed **18/18**: 0.5/1 s settings at 4/6/8 m, stationary
  root with advancing clip pose, no premature reaction/HP loss, nine contact cleanup
  cases, owner-token preservation, pause/World Slow, unrelated damage, miss and
  15 fps. Measured reactions were 0.534–0.537 s and 1.014–1.028 s after acceptance.
  Evidence: `../BuildArtifacts/DefensiveBlockContactDelayValidation.txt`.
- Final focused checks passed **23/23**: 19 contact-delay checks (adding immediate
  reaction when contact arrives after the minimum), three Begin/arrival checks and
  continuous charge/normal damage without Block. Begin checks extend only a cloned
  guard clip timing so reaction still occurs during Begin despite the minimum delay.
  Evidence: `DefensiveBlockContactDelayFinalValidation.txt` in the same folder.
- Player fallback and contact-order regression passed **14/14**, including 1.501 m
  Player recoil and rejection when Player was damaged before guard contact.
  Evidence: `DefensiveBlockContactDelayRegression.txt` in the same folder.
- Editor smoke/contact-order/grounding checks passed **24/24**.
  `CheckAssemblyBuild.ps1`: **0 errors, 80 warnings**;
  `DefensiveBlockContactDelayFinalBuild.log` records the final source validation.
- Two Play Mode frames 0.15 s apart show different Rector charge poses against the
  same guard position, with no knockback or HP loss while Contact is pending:
  `Temp/DefensiveBlockValidation/ContactDelayA.png` and `ContactDelayB.png`.
  The preview used a temporary 1 s delay; it was restored to 0.5 s afterwards.
  Returned to Edit Mode in `RectorDefensiveBlock.unity` without saving the scene.
- Tests use the regression scene's paused automatic AI. Existing Opsive Burst
  BC1055 errors remain; this is not a new GameSetup → Basement → BossRush validation.

## Defensive Block charge timing investigation (2026-09-18)

- Follow-up: the user rejected holding the skill animation because it breaks
  continuity. Production `RectorCharge.windupSeconds` is now **0**, and the authoring
  tool no longer initializes new profiles to 0.4. The hold implementation remains
  optional; the 0.4 s results below are historical and do not describe current behavior.
  That revision restored continuous skill playback and the original contact timing;
  the subsequent minimum-impact experiment was also reverted as recorded above.
  Follow-up Play Mode checks **2/2 passed**: zero-windup movement/normal damage,
  and one successful Block without Player/Ally HP loss. C# validation passed with
  **0 errors, 80 warnings**. Evidence: `DefensiveBlockContinuousValidation.txt`
  and `DefensiveBlockContinuousBuild.log` in `../BuildArtifacts`. Returned to Edit Mode.
- Early 8 m input intercepted step 0 at about 0.27 real seconds. The recorded
  hitbox swept into the guard at that point; it was not an artificial success timer.
  `Rector_Skill_1` root translation starts at the beginning of the clip, so the
  earlier hypothesis that step 0 was only a harmless preparation phase was incorrect.
- A temporary runtime copy of `RectorCharge` allowing only step 1 failed at both
  4 m and 8 m: zero successful blocks, Player HP loss 99.18, Ally HP loss 148.77.
  Step 0 still dealt damage before the newly restricted interception opportunity.
  The original skill profile and steps 0/1 were restored; the experimental copy
  was destroyed. No timing change was saved to production assets.
- Adding actual Rector windup would change the skill even without Block; delaying
  only the impact presentation would instead require stopping the confirmed charge
  at contact. The user selected a 0.4 s preparation before Rector actually charges.
  `RectorCharge.windupSeconds` now controls this caster-owned preparation through
  the existing AnimDriver hold API; interception still uses both charge steps.
- Evidence: `../BuildArtifacts/BlockChargeTimingBefore.txt` and
  `BlockSecondStepTrial.txt` record the initial experiment.
- Final implementation: **12/12** windup Play Mode checks passed. Early and late
  commands at 4/6/8 m kept Rector stationary and payload unreleased during preparation,
  then intercepted once with no HP loss. Measured impact times were 0.59–0.71 s
  after skill start at the test frame rate. Without Block, the delayed charge still
  dealt normal damage. Global pause, World Slow and actor exemption behaved correctly;
  reset/disable/death during preparation cleared the hold and defender reservation.
- Player fallback regression **11/11**, editor smoke/grounding checks **24/24**.
  `CheckAssemblyBuild.ps1`: **0 errors, 80 warnings**. Reports:
  `DefensiveBlockWindupValidation.txt`, `DefensiveBlockWindupFallback.txt`,
  `DefensiveBlockWindupBuild.log` and the intermediate `BlockChargeWindupAfter.txt`.
  Initial first-cast probe failures during scene startup were not counted as passing;
  the durable suite waits for initial party presentation before beginning trials.
- Visually checked the ready flare while `IsPreparingCharge` and `IsReady` were true
  and Rector remained at its starting position: `Temp/DefensiveBlockValidation/RectorWindupCue.png`.
  Cleared preview pause and returned to Edit Mode in the original test scene without saving it.

## Defensive Block airborne guard pose (2026-09-18)

- Reproduced in `RectorDefensiveBlock`: actor root stayed at Y 0.083 while Begin
  raised both feet and Loop held the lower toe near Y 0.51. Sampling `Aires_Block`
  confirmed its old 0.00-to-0.35 interval includes the jump, not a grounded guard.
- Added `beginStartNormalized` and authored only `AiresBlockAnimation.asset` through
  the Editor API: Begin now samples 0.55-to-0.65, Loop holds 0.65. No source FBX,
  impact clip, GuardSetting values or scene were saved/changed.
- Also reproduced acceptance over a disabled physical floor with stale NavMesh.
  The Block placement validator now requires nearby solid world support before
  acceptance and before committing the warp. Real-scene probes passed both missing
  floor rejection and cancellation/reservation cleanup when the floor disappeared
  during fade-out. Temporary floor collider changes were restored.
- Play Mode 4/6/8 m companion trials passed: lower toe stayed at or below Y 0.132
  during arrived Begin/Loop, one successful impact, no Player/Ally HP loss, cleanup.
- Player fallback regression passed **11/11** with the shared clip interval, including
  1.501 m recoil and wall/cleanup cases. Visually inspected the grounded Loop in
  `Temp/DefensiveBlockValidation/GroundedBlockLoop.png`. Returned the Editor to the
  original Play Mode test scene, unpaused with a fresh 8 m trial; no scene save.
- `DefensiveBlockGroundingTests.Run()` passed its two regression tests (sampled
  clip interval and nearby-solid-floor requirements); existing smoke checks 22/22
  passed. `CheckAssemblyBuild.ps1`: **0 errors, 104 warnings**.
- Console still reports Burst BC1055 from Opsive Behavior Designer's
  `BeforeTraversalCore.Reevaluate`; no package files were changed. The paused-AI
  Block harness passed, but this does not validate automatic AI traversal or the
  full production scene route.
- Evidence: `../BuildArtifacts/BlockFeetBefore.txt`, `BlockClipSamples.txt`,
  `BlockUnsupportedBefore.txt`, `BlockGroundAfter.txt`, `BlockGroundInvalidation.txt`
  `BlockGroundFallbackRegression.txt` and `DefensiveBlockGroundingBuild.log`.

## Player Defensive Block fallback and telegraph (2026-09-17)

- Follow-up: removed the Space key label and its runtime overlay Canvas. Readiness
  now uses only the existing bright/dim flare; Block input is unchanged. C# validation
  passed with **0 errors, 80 warnings** (`DefensiveBlockRemovePromptBuild.log`).
  Play Mode was not rerun for this presentation removal; the prompt screenshots below
  describe the earlier version.
- `CheckAssemblyBuild.ps1`: **0 errors, 80 warnings**. Existing production/contact
  smoke checks passed **22/22** after generalizing the guard receiver.
- Final self-guard Play Mode checks: **11/11 passed**, including 4/6/8 m, zero HP
  loss, Rector knockback, measured Player recoil **1.501 m**, wall-limited recoil,
  unavailable-receiver telegraph, timeout/disable/down/death/control-loss cleanup,
  preservation of another owner's control token, and rejection after prior damage.
- The first recoil probe exposed zero movement despite successful interception.
  Self recoil now uses the locomotion CharacterController footprint and moves
  through `CharacterController.Move`, avoiding the animated capsule's floor overlap
  and the vertical motor overwriting raw Transform displacement. The final tests
  explicitly assert both distance on clear ground and collision stopping.
- Additional Roma/Feno and companion integration checks: **23/25 passed**. Both
  Player models played Impact with zero damage and one successful interception.
  Companion priority, Player fallback for reserved/unsupported companions, Begin
  impact, previous-hit rejection and shared cleanup passed. The same two previously
  recorded clock checks still failed (0.7 s exempt reaction deadline with 0.5 s
  HitLag, and standalone Player knockback travel); they were not changed in this task.
- Visually inspected ready and unavailable telegraphs: the ready prompt uses an
  opaque dark panel for contrast; unavailable attacks retain the dim flare without
  a Space prompt. Captured Roma/Feno self impact with the reused prototype clips.
  Those clips and the shared close camera remain prototypes, not new per-character
  animation authoring. No full Basement-to-Boss-Rush route was rerun.
- Authored only the Player prefab binding through Unity's prefab API, reusing
  GuardSetting. No settings asset or scene was saved; Play Mode roster overrides
  and preview pause/control tokens were restored.

Evidence in `../BuildArtifacts/`: `DefensiveBlockPlayerFallbackBuild.log`,
`DefensiveBlockPlayerFallbackPlayMode.txt`, `DefensiveBlockFallbackRegression.txt`,
`DefensiveBlockPlayerFallbackRecoilFinal.txt`, `BlockReadyPrompt.png`,
`BlockUnavailableTelegraph.png`, `RomaSelfGuard.png`, and `FenoSelfGuard.png`.

## Defensive Block contact ordering (2026-09-17)

- `CheckAssemblyBuild.ps1`: **0 errors, 80 warnings**. Unity compilation and the
  existing 17 smoke checks passed; **5 contact-order smoke tests** passed after
  adding relative swept contact fractions, initial-overlap precedence and
  execution/request/caster-life/victim-life isolation.
- Final production-fixture Play Mode run: **48/51 passed**. All new contact-order
  cases passed: Player damaged before command, damage during departure, and Player
  moving ahead of an arrived guard. Commands cannot retroactively cancel damage
  or produce a later successful Block. Range 4/6/8, Begin impact, 15 FPS, no-block
  damage, request isolation and the wall test also passed.
- The first implementation conservatively rejected every initial-overlap tie and
  failed 4 m/Begin-4 m. The final rule lets a ready guard ahead of Player catch a
  wide hitbox on activation; an earlier applied hit always vetoes it. Both cases
  passed in the final run.
- Three existing checks did not pass in the final aggregate run: camera return
  after ControlLoss during Impact, exempt reaction completion at 0.7 s, and Player
  knockback travelling over 0.9 m. Focused telemetry confirmed camera return in
  0.474 s with its reservation released. With the authored 0.5 s HitLag, exempt
  Rector knockback ended by 0.846 s and guard Exit by 0.956 s while World Slow stayed
  at 0.25; the fixed 0.7 s expectation is too short for these settings. The Player
  probe stopped by 0.106 s after only 0.079 m, with timeScale=1 and World Slow=0.1;
  its early stop remains unisolated and is not claimed as passing clock coverage.
- Ran the fixture through a runtime-only scene load from GameSetup, preserving
  its unsaved Edit Mode state. No scene or settings asset was saved. This is not a
  repeat of the full Basement-to-Boss-Rush acceptance route below.

Evidence in `../BuildArtifacts/`: `DefensiveBlockContactOrderBuild.log`,
`DefensiveBlockContactOrderPlayMode.txt` (first run),
`DefensiveBlockContactOrderPlayModeFinal.txt`, `DefensiveBlockContactClockProbe.txt`,
and `DefensiveBlockContactReactionProbe.txt`.

## StateHub control and reload regression tests

Run `StateHubControlTests` in the `StateHub.Tests` Edit Mode assembly after
changing StateHub shooting admission, reload interruption, or control tokens.
The fixture checks token acquisition/release before Awake, retained locks after
initialization, explicit fire interrupting a configured reload, rejection without
loaded ammo or interruption permission, other shooting restrictions, held-fire
resumption after autoload, and a reload whose StateHub snapshot has not caught up.
It uses isolated actors and controlled reload state; live reload animation, audio,
projectile delivery, and input timing still need a Play Mode check.

Validation on 2026-09-30: the canonical assembly check passed with 0 errors and
93 warnings. Unity compiled the gameplay and test assembly without C# errors.
The Test Runner job `563c165e92fb4d6bae5252b2e53c83b9` timed out before starting;
all 14 fixture cases then passed by direct Editor invocation with setup/teardown.
Those are assertion results, not a successful Test Runner or Play Mode run.

## Canonical Command

Validate default `Assembly-CSharp` sources through:

```powershell
powershell -ExecutionPolicy Bypass -File 'P:\Game_RB_Project\RB_Project\Assets\Scripts\CheckAssemblyBuild.ps1'
```

`BuildAssembly.ps1` in the Unity project root is an allowed wrapper that calls
the same script and forwards the optional `-ArtifactRoot` parameter. Run it
from that directory, for example:

```powershell
powershell -ExecutionPolicy Bypass -File .\BuildAssembly.ps1
```

The script checks default-assembly sources only. For changed Editor, asmdef,
or first-pass sources, also let Unity compile and inspect the Console. Report
compilation, automated tests, and runtime checks separately, including checks
that could not run and why. Compilation alone does not validate runtime behavior
or prefab references.

Do not run `dotnet build` directly against Unity `.csproj` files for gameplay
validation.

## Build Artifact Rule

Do not run any build command that contains either of these paths:

- `Assets\Scripts\_buildbin`
- `Assets\Scripts\_buildobj`

Build artifacts must stay outside `Assets` so Unity does not import generated
assemblies back into the project.

## What The Script Does

Preserve the following requirements when modifying the validation script.

`CheckAssemblyBuild.ps1` uses Unity-generated `Assembly-CSharp.csproj` for:

- references
- define symbols
- analyzers
- compiler settings
- project references

It then builds a temporary scanned project outside `Assets`. The scanned
project generates its `Compile` list from real source files under `Assets`.

The source scan is intentionally scoped:

- Include default-assembly `.cs` files under `Assets`.
- Exclude files under folders with `.asmdef`.
- Exclude `Editor` folders.
- Exclude Unity first-pass roots such as `Assets\Plugins`,
  `Assets\Standard Assets`, and `Assets\Pro Standard Assets`.
- Do not include `Packages/**/*.cs` directly.

Package and asmdef code should remain referenced through Unity-generated
project references or assemblies.

## When To Validate

Run the validation command after editing C# source when the change touches:

- gameplay behavior
- public or serialized APIs
- context reference resolution
- stat, passive, weapon, inventory, save, or AI flows
- shared interfaces or data contracts

Markdown-only documentation changes do not require C# validation.

## Helper proc and rig validation

Run **Tools > RB > Skills > Validate Active Skill Trees** after editing a Helper loadout. The pass
reports missing execution skills, missing stable slot/option ids, duplicate Helper proc ids, and
unresolvable proc variants. Run **Tools > RB > AI > Validate Helper Rig** after editing
`Assets/Prefab/Player/Ally_Helper.prefab`; it checks the context, skill progress, skill manager,
animation driver/brain, and their context bindings.

The EditMode smoke tests cover selected proc resolution, variant snapshot/upgrade-id replacement,
legacy character-progress migration (including mixed legacy/current entries without destructive
default overwrites), and idempotent reload. PlayMode coverage is still required for
animation wind-up switching, activation callbacks, party-health queueing, chain attack interruption,
room transitions, and cinematic holds.

## Player / Ally combat loadout validation

Run **Tools > RB > Validate Player Ally Skill Loadouts** after editing a Stryker
`CharacterStats` loadout. A migrated character must have exactly one configured
`Active` slot and one configured `Ultimate` slot. Passive slots are validated
separately and do not count as cast slots. The validator also reports legacy
slot semantics, duplicate stable IDs, missing skill assets, and Passive/cast
type mismatches.

`CharacterProgressMigrationTests` covers key remapping, paid-cost preservation,
idempotence, collision recovery, and the per-character clone marker.
`CombatLoadoutRunLockTests` covers snapshot stability and release between runs.
The project compilation check does not replace a PlayMode pass for Basement
selection, scene transition, respawn, Player/Ally role swap, party-command CP,
or HUD/input wiring.

## Failure Handling

If validation fails:

1. Fix source-code errors first.
2. Do not edit generated `.csproj` or `.sln` files unless the task explicitly
   requires it.
3. Do not move new classes into unrelated existing files just because Unity has
   not regenerated project files yet.
4. Keep new Unity classes in their intended `.cs` files.
5. If the generated `.csproj` is stale, refresh Unity/reimport/regenerate project
   files instead of changing file ownership.
# Active Skill Tree validation

`SkillUpgradeTreeValidator` (`Assets/Scripts/Editor/ActiveSkill/SkillUpgradeTreeValidator.cs`)
checks `SkillUpgradeTreeDefinition` assets. Beyond blank/duplicate node ids, cost/level ranges,
unsupported `StatType` stat modifiers, prerequisite cycles, and node overlap, it also validates:

- `grantedUpgradeIds`: blank entries, duplicates within one node, and (when the tree has an
  owning `SkillGemDefinition`, resolved via an `AssetDatabase` scan for `upgradeTree`/
  `upgradeTreeOverride` references) ids that don't match anything the owning skill's payload
  declares through `CollectUpgradeIds`. The two directions carry different severities:
  a node granting an id nothing consumes is a **Warning** (the normal state while a tree is
  authored ahead of its payload), while an id the payload declares that no node grants is an
  **Error** (that feature is unreachable in game). A tree with no owning skill yet warns instead
  of erroring, since ids can't be cross-checked in that case.
- The same upgrade id granted by two different nodes is a Warning, because `HasUpgrade` is a set
  membership test and the second node costs a point without changing anything. It is suppressed
  when the two nodes are mutually exclusive — that is how a branch choice offers the same unlock
  down either path.
- `mutuallyExclusiveNodeIds`: blank entries, self-exclusion, missing node ids, a node that both
  requires and excludes the same node, and asymmetric pairs (node A excludes B but B does not
  exclude A back) — the last one is an Error because a one-way lock only misbehaves for players
  who unlock in a specific order.
- The "no gameplay effect" warning no longer fires for a node whose only effect is granting an
  upgrade id (it used to fire on every pure-behavior node once `grantedUpgradeIds` shipped).

Every issue carries a `NodeId` (`null` for tree-level issues). Issues that concern two nodes —
overlap, one-way exclusion, duplicate grants — are emitted once per node with node-specific
wording, so consumers never have to search the message text to work out who an issue belongs to.

Two entry points: `ActiveSkillTreeEditorWindow.ValidateTree()` (open tree, logs to console) and
the project-wide `Tools/RB/Skills/Validate Active Skill Trees` menu. The tree editor window
(`Tools > RB > Skills > Active Skill Tree Editor`) also shows validation issues inline: selecting
a node displays that node's issues as `HelpBox`es below its inspector fields, with a compact count
of remaining issues elsewhere in the tree. Each graph node additionally shows a status badge in its
title bar (`✓` clean, `!` warning, `✕` error) driven by the same `NodeId`. This inline pass is
cached and only recomputes when the tree or the selected node's data changes, so it does not re-run
every repaint.

`SkillUpgradeTreeValidator.cs` and `ActiveSkillTreeEditorWindow.cs` live under `Editor/`, so
`CheckAssemblyBuild.ps1` does not compile them. Verify changes to either file by opening Unity and
running the validate menu / editor window, not by trusting a green `CheckAssemblyBuild.ps1` run.

The tree editor's **Skill Steps** panel edits the owning skill's
`CompositeSkillPayloadDef.steps` directly (add/reorder/remove/`requiredUpgradeId` only; deep
payload fields still go through the skill Inspector). It is gated behind
`SkillUpgradeTreeValidator.FindOwningAssets` resolving to exactly one `SkillGemDefinition` owner
with an embedded composite root, and shares `SkillPayloadAssetUtility.CreateEmbeddedStepPayload` /
`RemoveEmbeddedStepPayload` with the skill's own inspector so a step never ends up unassigned or
orphaned. It tracks its own dirty/save state, separate from the tree asset's — closing the window
or switching trees prompts to save/discard skill step changes independently of tree changes. The
shared payload utility never calls global `AssetDatabase.SaveAssets()`; the invoking window owns
the save boundary, so editing skill steps does not persist unrelated dirty assets.

## Conditional status route regression checks

Conditional status applications live in `ConditionalStatusRoute` fields, and every editor consumer
resolves "who does this land on?" from `[SkillStatusRouteTarget]` via `SkillStatusRouteMetadata`.
`Tools/RB/Skills/Run Status Effect Authoring Smoke Tests` covers the regressions that matter here:

- Routes are discovered from the declaration alone — probe types the resolver has never heard of
  are found, including two routes with the same target on one owner (their route keys must stay
  distinct).
- A behavior-driven target is read from the owning instance, so a `HealAreaSkillPayloadDef` retargeted between
  `Self` and `Allies` moves its route with it.
- A route with no attribute, a target member that does not exist, or one of the wrong type is a
  **blocking** metadata error — never a silent fallback to `Self`.
- `Validate Active Skill Trees` reports those metadata failures as tree-level errors, and route
  resolution also blocks a field whose applications list is not present in its `SerializedObject`
  (for example, a private route field that forgot `[SerializeField]`).
- `UpgradeIdUsageScanner` labels the target from route metadata, so the tree's Gameplay Effects
  summary and the wizard's destination list cannot disagree.
- A recognized step's own bare gate (no sibling `spec`) can describe itself instead of falling back
  to `Enable <Step>`. `HealAreaSkillPayloadDef` is the first case: target mode, the `FinalSkillStats` channel
  behind Heal Power/Area Radius, and any unconditional status it applies. An unrecognized step type
  still falls back to `Enable <Step>` rather than guessing from its class name.
  `RequiredPathPreviewResolver` (`Assets/Scripts/Editor/ActiveSkill/RequiredPathPreviewResolver.cs`)
  backs the node inspector's **Required Path Preview**: it walks `requiredNodeIds` back to the tree
  root (cycle- and missing-id-safe), builds a `SkillUpgradeStatSnapshot` with the runtime formula,
  and calls `SkillInstance.GetFinalStats(null)` so the preview matches gameplay exactly for that one
  path — never labelled "Final", since an optional sibling node can still add more later. `Run
  Active Skill Core Smoke Tests` (see `Docs/SYSTEMS/SKILL_SYSTEM.md`) covers both the scanner
  summary and the resolver's chain aggregation, `mul: 0` zeroing, optional-node exclusion, and
  cycle/missing-prerequisite safety.

The one-off migration from the old per-type `conditionalApplications` schema ran through
`Tools/RB/Skills/Migrate Conditional Status Routes` (with a dry-run counterpart). It copied every
channel field-by-field — upgrade id, effect reference, stacks, modifier override and its list,
duration/tick damage/tick interval overrides and their enable flags — and both the tool and the
legacy fields were deleted once the entry counts matched. No `conditionalApplications` schema
remains in the project; a reappearance means an asset was restored from an old revision.

## Status effect scope validation

`StatusEffectScopeValidator` (`Assets/Scripts/Editor/ActiveSkill/StatusEffectScopeValidator.cs`)
is a separate pass from the tree validator, because the relationships it checks span the whole
project rather than one tree: a unique status leaking into a second skill is invisible from inside
either tree on its own. Run it from `Tools/RB/Skills/Validate Status Effect Scopes`.

- A `StatusEffectDef` with no `StatusScope.*` label is a **Warning** (legacy asset — still usable).
- A `StatusScope.Unique` status referenced by a skill that is not its `StatusOwner.<guid>` is an
  **Error**, as is a `StatusScope.Unique` status with no owner label at all.
- The same `effectId` on two `StatusEffectDef` assets is an **Error**.

Label integrity is checked for every `StatusEffectDef` asset, including statuses that are not
referenced yet, so an orphaned Unique-without-owner or unlabelled legacy asset cannot disappear
from the report merely because no skill uses it.

Usage is discovered by walking every `SkillDefinitionBase` asset *and its sub-assets* for object
references to `StatusEffectDef`, rather than by enumerating known payload fields — a new apply site
is picked up automatically instead of silently escaping the check.

The wizard's own validation (`ActiveSkillStatusEffectAuthoringService.Validate`) runs the same
scope rules plus: a missing owning skill on a shared tree, a blank gate id, a target with no route
in the skill, a destination whose step has been deleted, a duplicate
status + gate id + target + destination, a duplicate `effectId` on a status about to be created,
and a `Multiply` modifier at or below zero (warning). Scope-repair buttons only stage their action
in the request; `Apply` refuses to write while any error is present and performs no writes before
commit, which is what makes **Cancel** safe.

`Tools/RB/Skills/Run Status Effect Authoring Smoke Tests` exercises the flow end to end against
real assets in a temporary folder (`Assets/_StatusAuthoringSmokeTests`, deleted afterwards),
because the interesting cases — asset labels, embedded payload sub-assets, and writing to private
`conditionalApplications` lists — do not exist for in-memory `ScriptableObject`s.

All of these files live under `Editor/`, so `CheckAssemblyBuild.ps1` does not compile them. Verify
changes by running the two menu items above in Unity, not by trusting a green
`CheckAssemblyBuild.ps1` run.

`Tools/RB/Skills/Validate Embedded Payloads` validates a payload ownership graph, not a fixed
sub-asset count. A composite may own its embedded root plus one unique embedded payload per
`PayloadStep`. Null/nested/external/duplicate child references and embedded payloads that are not
reachable from the root are errors. Validation reports existing orphans without deleting them.
Replacing or removing a Composite root deletes its reachable embedded descendants children-first
with Undo support. The same validation pass reports a missing payload-required timeline marker
(for example `TauntApply` or hitbox start/end) as an authoring error; direct `SkillEffectStep`
implementations require no marker because they execute at the cast point.

`CompositeSkillPayloadEditorTests` covers valid multi-payload graphs, orphan/external/duplicate
ownership, recursive replacement/removal and Undo, scoped save behavior, Composite hitbox timeline
authoring, and missing required timeline events. Run it in EditMode after changing Composite
payload authoring or validation.

# Unified Authoring Validation

Node-centric ability authoring (`Assets/Scripts/Editor/ActiveSkill/AbilityAuthoring/`, see
`Docs/SYSTEMS/SKILL_SYSTEM.md` **Node-Centric Ability Authoring**) uses a 3-level severity model —
`PayloadAuthoringSeverity.Error`/`Warning`/`Info` — for descriptor and per-payload authoring issues,
distinct from `SkillUpgradeTreeValidator`'s existing 2-level `SkillUpgradeValidationSeverity`. Error
blocks Create and Save; Warning allows Save only after one explicit confirmation; Info is guidance
only. `NodeCentricPayloadValidator.Validate` bridges the two: an `Error` maps to `Error`, and both
`Warning` and `Info` map to `Warning` — the safe direction, since folding Info into Warning can only
ask for one extra confirmation, never hide something that should have blocked.

## What NodeCentricPayloadValidator checks

Everything here is *in addition to* `SkillUpgradeTreeValidator`'s existing tree/binding rules
(duplicate grants, an id nothing declares, a declared id no node grants — see **Active Skill Tree
validation** above), which already cover most of "every node-owned id resolves to exactly one step
usage." This file adds what that validator cannot see:

- **Registry health** — `PayloadDesignerDescriptorRegistry.GetDiagnostics()` (duplicate descriptors,
  a descriptor mapped to an invalid/abstract type or to `CompositeSkillPayloadDef`) surfaces as
  tree-level errors.
- **Per-payload authoring issues** — for every node-owned `PayloadStep` with a registered descriptor,
  `descriptor.CollectAuthoringIssues` runs and its result is attributed to the exact node that grants
  that step's id (resolved by scanning `grantedUpgradeIds`, not by whichever node happens to be
  selected in the editor).
- **Missing descriptor on a node-owned step** — a payload type with no registered descriptor cannot
  have been authored through the normal wizard; if one is found gated by a real id anyway (Advanced
  mode, or a hand-edited asset), it is a blocking Error.
- **Direct gameplay step** — any `SkillEffectStep` that is not a `PayloadStep` is a blocking Error.
  `PayloadStep` is the only supported orchestration type; a direct-gameplay step type fuses
  orchestration and behavior, which is no longer allowed (the retired `HealAreaStep` was the last
  one — see **Legacy migration** below).
- **Non-normalized granted id** — a Warning, not an Error, since a hand-edited or pre-generator id
  still works at runtime; `AbilityBindingIdGenerator.Normalize` defines the canonical form.
- **Always-active steps are never attributed to a node** — a blank-gated `PayloadStep` is not owned
  by any node (plan section 14.4); its own authoring issues are not surfaced by this validator today
  (no Advanced/skill-level "Always Active Skill Effects" panel exists yet). The Skill Inspector's own
  `SkillGemDefinition`/`CompositeSkillPayloadDef.CollectValidationIssues` still covers it separately.

## Save integration

`ActiveSkillTreeEditorWindow.ComputeUnifiedIssues` merges `SkillUpgradeTreeValidator.Validate` and
`NodeCentricPayloadValidator.Validate` into the one list used by `EnsureIssues` (node badges, inline
issue list), the **Validate** toolbar button, and — new — the **Save** toolbar button.
`ConfirmSaveAgainstValidationIssues` runs before every save: any Error shows a blocking "Cannot Save"
dialog listing them and aborts; any Warning (no Errors) shows one consolidated "Save With Warnings?"
confirmation; a clean tree saves without a prompt. Previously **Save** had no validation gate at all
— this closes that gap.

## Smoke tests

Each menu item is `Tools/RB/Skills/Run <Name> Smoke Tests`, follows the same real-temp-asset pattern
as the Status Effect Authoring smoke tests (own temp folder, deleted on completion, no
`AssetDatabase.SaveAssets()`), and lives under `Editor/`, so `CheckAssemblyBuild.ps1` does not
compile it — verify by running the menu item in Unity, not by trusting a green
`CheckAssemblyBuild.ps1` run:

- **Payload Descriptor** — registry discovery/diagnostics, safe defaults and summary/issue
  generation not throwing on a fresh incomplete draft, and a missing required reference always
  reporting at least one Error.
- **Node Ability Authoring Service** — single-to-composite conversion preserving the original
  payload object and its values, root-owned execution field transfer and child reset, Create's
  auto-convert path, stable-id binding and dedup, a validation-failing draft leaving no side
  effects, Edit committing in place, Duplicate producing a unique object/id, Remove's reference-
  safety guards, and Undo restoring an entire Create in one step.
- **Ability Wizard** — draft construction/safe-defaults for Create, draft population for Edit,
  cleanup on every exit path, and an end-to-end Commit, all driven through reflection since
  `OnGUI`/button clicks cannot be simulated headlessly.
- **Node Ability Cards** — card resolution against a real node/step/payload graph (found, skipped
  when orphaned, deduped), the Duplicate action, and resolution reflecting a removal done through
  the service. (Remove's own confirmation dialog cannot be driven headlessly; its underlying
  mutation is covered by the service test above.)
- **Unified Validation** — a clean ability reporting no issues, a missing required reference
  reported as an Error on the exact granting node, a non-normalized id as a Warning, an
  always-active step never attributed to a node, and the Save gate returning immediately on a
  clean tree (built against an isolated skill/tree, not a shared fixture another test in the same
  run may have deliberately made invalid — see the caution below).

**Caution:** any test that calls a method reachable from `ConfirmSaveAgainstValidationIssues` or
otherwise capable of popping a real `EditorUtility.DisplayDialog` must not run against a fixture a
different test in the same suite has left in an Error state, and must never be exercised through
automated tooling without a human able to dismiss the dialog — a stuck modal blocks the entire Unity
process, not just the test run.

## Legacy migration (completed, tooling removed)

The project's only direct-gameplay step, `HealAreaStep`, was migrated to `HealAreaSkillPayloadDef` +
`PayloadStep` in `Aires_Skill_3.asset` (the only asset that used it) through a one-time
Dry-Run/Apply migration tool, then the tool and the legacy type were both deleted — the same pattern
already used for the earlier Conditional Status Route migration (see **Conditional status route
regression checks** above). Before deletion, every one of these held:

- a project-wide Dry Run reported zero remaining `HealAreaStep` usages
- `Validate Embedded Payloads` and `Validate Active Skill Trees` both passed
- the Active Skill smoke tests passed
- the migrated skill validated cleanly (`SkillUpgradeTreeValidator`: 0 errors;
  `NodeCentricPayloadValidator`: 0 issues; `CompositeSkillPayloadDef.CollectValidationIssues`: 0
  issues) and reloaded from disk with no missing-type warning
- a project-wide text search for `class: HealAreaStep` across `Assets/Data` returned nothing

A `HealAreaStep` reference reappearing in any asset means it was restored from an old revision, not
that migration is still in progress — there is nothing left to migrate, and the type no longer
exists to deserialize into.

# Character animation validation

`CharacterAnimBrainSmokeTests`
(`Assets/Scripts/Editor/Animator/CharacterAnimBrainSmokeTests.cs`) is the
EditMode safety net for `CharacterAnimBrain` playback lifecycle and command
admission. Run it before and after any change to the Brain, its partial state
files, or `CharacterAnimDriver`.

It builds a real Animancer graph — the graph does initialise outside Play Mode —
but **the graph never advances time**. Every assertion is therefore driven by a
cast point of `0` (which the chain poll satisfies on the first tick) or by
invoking the state's own end-of-clip callback the way Animancer would. Coverage:

- skill / utility / chain-cutscene / chain-skill lifecycles and their signal order
- exactly one terminal per request, and none for a caller-requested cancel
- a completion handler starting the next chain in the same frame
- chain playback rejecting every external animation command
- root-motion policy flags for skill playback
- stage intro and hard status handing `applyRootMotion` back on exit
- animator/profile rebind interrupting active playback exactly once
- missing clip / missing profile failing safely with no events
- teardown not replaying terminal events
- the binding fast path: a steady-state tick not re-resolving the hierarchy,
  `InvalidateAnimationBinding()` forcing exactly one full resolve, and an
  Animator or `baseStats.animProfile` swap rebinding on its own
- session lifecycle: two teardown paths racing one request still emitting a
  single terminal, a request never seeing both `Completed` and `Interrupted`, and
  a completed chain being delivered every beat it skipped before its terminal
- root motion: one coherent published policy, the façade never disagreeing with
  it, the policy clearing when playback ends or the Brain is disabled, adapters
  receiving the current policy on registration, and a registered adapter taking
  `Animator.applyRootMotion` over from the Brain

`CharacterAnimationTransitionPolicyTests`
(`Assets/Scripts/Editor/Animator/CharacterAnimationTransitionPolicyTests.cs`)
guards animation priority. Its `ObservedTransitionMatrixMatchesTheAuthoredTable`
test drives a real Brain through every (current mode, requested mode) pair and
compares against a literal table that was captured from the implementation
**before** `CharacterAnimationTransitionPolicy` existed. That table is the
contract, not a restatement of the policy: if a cell changes, gameplay priority
changed. Update the table in the same commit as the policy so the new priority is
the reviewed artefact — never to make a red test go green.

Three cells are asymmetries rather than obvious rules, and are deliberate:

- `FullBodyReload -> Chain` is blocked while `FullBodyReload -> Skill` is allowed.
  A skill calls `StopReloadAction()` first, which clears the reload's exit lock;
  chain playback does not.
- `Skill -> Skill` is blocked but `Skill -> Utility` is allowed. Only skill
  admission consults `IsShootBlockingPlaybackActive`.
- Knockback blocks hard status poses too, not just soft ones.

These files live under `Editor/`, so `CheckAssemblyBuild.ps1` does not compile
them. A green build says nothing about these suites — run them in Unity.

`CheckAssemblyBuild.ps1` also cannot catch a name collision with a `UnityEditor`
type, because it never compiles the editor assembly. A new global-namespace type
shadows a same-named `UnityEditor` type for every editor script that has
`using UnityEditor;` — this is why the transition mode enum is called
`CharacterAnimationMode` and not `AnimationMode`. After adding a top-level type
with a generic name, reimport in Unity and check the Console, not just the build.

Play Mode still owns everything that needs real clip time: skill cast-point
events (they are Animancer events, not polled), the chain playback watchdog,
fade weights, root-motion delta, and prefab wiring. The prefab matrix to exercise
by hand is Player, Ally (NavMesh), an Enemy with a different hierarchy, and a
Summon/turret that uses `inspectorAnimProfile` instead of `baseStats.animProfile`.

## Animation hot-path baseline

`CharacterAnimBrain.Update` calls `TryInitialize()` every frame, which calls
`ResolveReferences()` and `ctx.ResolveReferences()`. Before changing that, record
a baseline so the change can be proved rather than assumed. This is a manual
Unity Profiler pass; no script captures it:

1. Open a MapRun stage and enter Play Mode with the Profiler recording (CPU
   Usage, Hierarchy view, Deep Profile off).
2. Capture at 1, 25, and 100 live actors.
3. Record, per frame: `CharacterAnimBrain.Update`, `TryInitialize`,
   `CharacteContext.ResolveReferences`, `GetComponentInChildren`, and GC Alloc.
4. Repeat the identical capture after the change and compare the same five
   numbers at the same three actor counts.

# Weapon affix validation

Run **Tools > Weapons > Affixes > Validate (Dry Run)** before builds. The build
preprocessor blocks missing behavior assets, duplicate ids, and invalid roll
ranges. `WeaponAffixFrameworkTests` verifies all 27 registered definitions,
endpoint rolls, structured tooltip data, Last Round eligibility, persistent-state
cloning, typed overkill metadata, and the recursion guard.

On 2026-08-03 the focused EditMode suite passed 6/6 and
`CheckAssemblyBuild.ps1` passed with 0 errors. The full EditMode run was blocked
by the pre-existing `PartySpawnUnityTests` scene-open error for
`Assets/Scenes/Map_Play_Pototype/State_1.unity`.

# Summon regression validation

`SummonContractSmokeTests` covers summon/owner attribution across sibling
hierarchies, normal-actor fallback, delayed despawn and presentation isolation,
inactive staging, no-map composite cast semantics, room-transition VFX ownership,
ground/clearance mask overlap, nested oriented-box placement, and horizontal
capsule clearance. Run it with the Unity Test Framework before checking the
Assembly-CSharp build. The focused
PlayMode pass should additionally cover melee kill, weapon projectile kill, DoT
kill after summon destruction, cap eviction with presentation delay, room commit,
rollback, mobile warp failure, and placement/NavMesh failure without consuming a
cap slot.

`CharacterPlacementBaselineTests` captures the pre-migration legacy Chain teleport
contract: the resolver stops at the first accepted candidate and preserves the
authored sweep order (`0`, `+15`, `-15` degrees). This is a baseline only; the
future central resolver may choose a different candidate after its scored result
is explicitly adopted. There is currently no direct EditMode integration coverage
for Chain Attack or Interruption controller placement, so those paths remain a
Phase 3 PlayMode test gap.

`CharacterPlacementResolverTests` covers the Phase 1 core contract without
activating gameplay adapters: wall penetration outranks actor penetration,
disabled planar root motion is evaluated as a static trajectory, the target is
ignored only inside the configured contact window, reservations contribute actor
overlap, required animation fails closed, the trajectory sweep catches a thin
world blocker between samples, a NavMesh start adjustment cannot move the
predicted target impact, Utility-tail/Attack composition keeps one continuous
impact timeline with accumulated yaw, authored order breaks equal scores,
null-safe anchor capture, runtime-policy defaults, and transient summon
reservation deduplication against a physical collider on a sibling of
`SummonedEntityRuntime`.
Run both placement suites in EditMode after the Unity instance holding the project
has been closed or after using the active Editor's Test Runner.

The focused summon regression pass also runs `SummonContractSmokeTests` after
the central summon-clearance delegation. It protects Box/Capsule/Sphere
footprint parity, unsupported-collider fallback, ground-collider exclusion,
nested/rotated footprints, horizontal capsules, least-overlap candidate search,
and the existing owner/runtime contracts. Chain and Interruption controller
scenarios that require live animation playback, utility-tail sequencing, or
PlayMode reservation timing remain integration coverage rather than EditMode
unit coverage.

# Projectile lifecycle validation

Two EditMode suites cover the pooled projectile, plus one Editor menu item.

**`ProjectileLifecycleSmokeTests`** — the runtime contract:

- an acquired projectile stays inactive and its `OnEnable` has not run yet, on reuse **and** on the
  very first `Instantiate`,
- at activation, `OnEnable` observes the final layer, context, direction, depth, split generation,
  and config,
- a despawned projectile goes inactive and is handed back out by the pool,
- reuse as a weapon bullet clears the previous life's AoE, crit, presentation assets, skill source,
  and collision-ignore root,
- world slow scales both travel speed and lifetime accrual (0 = frozen, 0.5 = half),
- a split child is `parent.depth + 1` and `parent.splitGeneration + 1`,
- `SplitOnHitModule` clamps `childCount` and `maxSplitGenerations`,
- a runaway split chain terminates at `Projectile.AbsoluteMaxSplitGeneration`,
- an inherited split budget cannot be widened by a permissive child config, keeps narrowing by
  `min` down the chain, treats `0` as "never split", and survives a spawn that authors no budget,
- `ProjectileSplitGraphAnalyzer` detects a cyclic `childConfig` graph and leaves acyclic ones alone.

**`ProjectileAuthoringValidationTests`** — the authoring contract:

- a second component driving the root Rigidbody is rejected, a presentation-only component is not,
- every gameplay projectile prefab has a single movement/lifetime owner,
- the prefab sweep actually finds prefabs and never reaches vendor folders,
- no shipped split configuration loops back on itself,
- no **new** broken projectile/bullet prefab reference. Known blockers are listed in
  `KnownBrokenProjectileReferences` as a full `path|property|guid` triple, so a second, unrelated
  break in an already-listed prefab is still reported. The test fails both on a new break and on a
  stale entry, so repairing one forces the list to be trimmed.

**Menu item** — **Tools > Validation > Projectile Authoring Report** runs all three authoring
checks and prints the result to the console.

**Known blocker** — `Assets/Prefab/GameEnemy/Enemy_Base.prefab` has `projectilePrefab` pointing at
guid `522002f7fd0905a44ad43b9329339bca`, which no longer exists in the project. Every other
character prefab points at `BulletPlayer_Test_ModulesVer.prefab`. The intended replacement was not
guessed; `WeaponSystem` overwrites the field from `currentWeapon.BulletPrefab` on equip, which is
why the break has not been visible in play.

**Not covered by these suites** — trigger collisions, hit/area VFX, real prefab physics, and
lifetime expiry under a running physics loop remain Play Mode checks. Editor scripts are also not
covered by `CheckAssemblyBuild.ps1`; compile them through a Unity script refresh.

# Map system validation

`CheckAssemblyBuild.ps1` does not compile `Assets/Scripts/Editor`, so map
tooling and map tests are validated by letting Unity compile and then running
the Edit Mode suites in the Test Runner.

Edit Mode suites under `Assets/Scripts/Editor/MapSystem`:

| Suite | Covers |
| --- | --- |
| `MapGeneratorTests` | generator output is a valid graph across seeds |
| `RoomRuntimeContentTests` | the runtime content hierarchy and its clearing rules |
| `MapRunTransitionTests` | the room-transition transaction: commit, rollback, first-entry failure, retry, revisit caching |
| `StageCompletionTests` | the Stage Exit refusal path and the single-commit guarantee |
| `EncounterContentTests` | wave prefab selection over pools with empty slots |
| `BasementBoardPageTests` | Basement board page ownership and the preservation of hand-authored pages |
| `RoomTransitionCleanupTests` | the transition sweep spares party-owned and cached-room content |
| `RoomLifecycleListenerTests` | `IRoomLifecycleListener` drives room-specific behaviour, and only where it applies |
| `MapContentValidationTests` | every run config in the project has no error-level content defect |
| `MapContentValidatorDetectionTests` | each content rule actually fires on broken content |
| `StageProfileTests` | tuning is read from profiles, and a config missing one is rejected |
| `StageProgressSchemaTests` | the save schema version and the Stage Id alias migration |
| `MapGeneratorSweepTests` | 256 seeds per run config, plus an explicit 10,000-seed soak |
| `MapGraphStructureValidatorDetectionTests` | each graph invariant actually fires on a broken graph |
| `StageIntroSmokeTests` | stage intro rig contract |

`MapRunTransitionTests` and `StageCompletionTests` drive `MapRunController`
through `MapRunTestFixture`, which builds an in-memory run config, room
definitions, and room templates, and replaces the party warp with
`MapRunController.PartyWarpOverride`. That seam exists only for tests and is
null during play.

Edit Mode has no `SaveManager` and no `SceneLoaderSystem` singleton, so
`StageCompletionTests` can only cover the refused half of stage completion.
Verify the accepted half in Play Mode: clear the Boss, take the portal once, and
confirm XP and Stage Progress each advance exactly once.

Before touching the Basement board, run **Tools > RB Project > Map > Validate
Basement Board (Dry Run)** and read the report. After applying, re-run
`Apply Test Stage Content` a second time: the scene must show no further diff.

## Map content validation

**Tools > RB Project > Map > Validate Map Content** runs `MapContentValidator`
over every `MapRunConfigSO` in the project. New configs are discovered by asset
type, so a stage added without content cannot slip through unvalidated.

Two severities, and the difference matters:

- **Error** — the run would break, soft-lock, or silently produce wrong content.
  `MapContentValidationTests` fails on any error.
- **Warning** — authoring is degraded but runtime has a working fallback.
  Reported, never fatal.

What it checks, per config:

| Area | Rule | Severity |
| --- | --- | --- |
| Stage identity | two configs share a `Stage Id` | Error |
| Stage identity | `Stage Id` is empty, so the config is not a Test Stage | Warning |
| Config | `MapRunConfigValidator` rejects the config | Error |
| Coverage | a generatable node type has no usable room definition | Error |
| Coverage | a Combat/Elite/Ambush/Trap/Boss type has no usable encounter | Error |
| Encounter | `Boss Encounter` disagrees with `Node Type` | Error |
| Encounter | no waves, or a wave with no usable enemy prefab | Error |
| Encounter | an empty enemy prefab slot inside an otherwise usable wave | Warning |
| Enemy prefab | no `EnemyContext`, no base stats, or no `HealthSystem` | Error |
| Room prefab | no `RoomController` — runtime adds one, but with no authored sockets | Error |
| Room prefab | no `NavMeshSurface`, or a surface with no baked `NavMeshData` | Error |
| Room prefab | a direction in `Exit Mask` has no `RoomExitInteractable` | Error |
| Room prefab | two exit sockets authored in the same direction | Error |
| Room prefab | an exit socket outside the `Exit Mask` | Warning |
| Room prefab | a Combat/Elite/Ambush/Trap/Boss room with no enemy spawn points | Error |
| Room prefab | a masked direction with no entrance spawn point | Warning |
| Room prefab | a Boss room with no Stage Exit spawn point | Warning |
| Room prefab | a Heal room a Test Stage can use, with no `TestStageRecoveryStations` | Error |
| Stage Exit | prefab missing `StageExitInteractable`, `InteractableLink`, a trigger collider, or the `Interactable` layer | Error |

Rooms in this project author their entrance spawn as a `SpawnPoint`-named child
under each exit socket rather than filling `Player Spawn Points By Direction`.
Both satisfy the entrance-spawn rule; the generic `Player Spawn Point` is only
reported when neither exists.

### Generator sweep

`MapGeneratorSweepTests` generates every run config across 256 seeds and runs
both `MapPathValidator` (the runtime gate) and `MapGraphStructureValidator` on
each graph. The structure validator covers what the runtime gate does not:
unique node ids, incoming/outgoing edge symmetry, critical-path endpoints and
adjacency, and the branch-count range.

`MinBranchCount` is a guarantee here, not a preference. `MapGenerator` only logs
a warning when it runs out of branch parents, so a shortfall is caught by the
sweep instead of shipping silently.

`SoakEveryRunConfigAcrossTenThousandSeeds` is marked `Explicit` so it stays out
of the normal run, but it is cheap — about 1.5 s for 50,000 graphs on the
current configs. Run it from the Test Runner after changing the generator, the
room definitions, or the branch limits.

## Stage catalog validation

`StageCatalogValidator` runs as part of **Tools > RB Project > Map > Validate Map Content**.

| Rule | Severity |
| --- | --- |
| a `StageDefinitionSO` with no run config | Error |
| its `Stage Id` disagrees with its run config's `Stage Id` | Error |
| a stage resolves to an empty `Stage Id` | Error |
| two stages share a `Stage Id` | Error |
| a `Legacy Stage Id` is another stage's current id | Error |
| a `Legacy Stage Id` is empty, duplicated, or repeats the current id | Warning |
| a catalog lists an empty slot, or the same stage twice | Error |
| a catalog lists no stages | Warning |

The legacy-id rule is the one worth understanding: a legacy id is adopted on load, so pointing it at
a stage that is still live would silently hand that stage's saved progress to another stage.

## Profiles are required

Every `MapRunConfigSO` must reference a `MapGenerationProfileSO` and a `MapContentPoolSO`, and a
Test Stage must also reference a `StageProgressionProfileSO`. `MapRunConfigValidator` reports a
missing one as an error, so `StartRun` refuses the run and
`MapContentValidationTests` fails the suite.

The one-off migration that moved inline tuning onto profiles has been applied and its tool removed;
the run configs no longer carry inline tuning fields at all. `Tools > RB Project > Map > Apply Test
Stage Content` authors the Test Stage profiles directly, reusing whatever profile a config already
references rather than creating a second one beside it.

## Dialogue authoring

`Tools/Dialogue/Validate Dialogue Authoring` scans every `DialogueSequenceSO`,
`CharacterDialogueAnimationProfileSO`, and `DialogueProfileDatabaseSO` in the project, plus the
`DialogueStage`, `DialogueDirector`, and every `DialogueTrigger` in the open scenes.

| Reported | Why it matters |
|---|---|
| two sequences share a `dialogueId` | they share play-once completion, so finishing one silently locks the other out |
| a play-once trigger whose sequence has no `dialogueId` | completion cannot be persisted, so it replays forever |
| a line spoken by someone outside the cast | that line emphasises nobody |
| two cast entries in the same slot, or more than three entries | the stage has exactly three slots |
| a cast member with no registered pose profile | the actor stands un-posed |
| a profile with no `idlePose` | every unmapped pose leaves the actor un-posed |
| a slot missing its actor anchor, portrait camera, or RawImage; other stage/UI wiring gaps; an active `CloneStaging` root | the slot cannot render, or stripped gameplay components would wake up on the clone |
| `DialoguePresentation` not enabled in Build Settings | the stage cannot load |
| a scene actor with no id or model root, two actors sharing an id, an actor the sequence never stages, or an `npc.` key with no supplying actor | the NPC silently never appears |
| a required (non-optional) cast entry on a party role | the conversation refuses to start whenever that party slot is empty |
| `DialogueLayers.AspFeatureRenderingLayerMask` no longer matching the URP renderer | portraits render flatter than the same character in gameplay |

Run it before committing dialogue content. `CheckAssemblyBuild.ps1` covers the runtime dialogue
scripts but, as always, **not** the editor tooling — the two `Tools/Dialogue/…` menu items have to be
run in Unity to verify.

## Special Shoot Point validation

### Automated (Edit Mode)

Three fixtures under `Assets/Scripts/Editor/SpecialShootPoint/`:

| Suite | Covers |
|---|---|
| `SpecialShootPointSmokeTests` | Shuffle-bag rotation (every enabled anchor consumed before any repeat, no duplicates in one round, clean failure when anchors are insufficient, ineligible anchors never drawn), profile count/HP/reward clamping, registry register/unregister and stale-entry pruning, anchor usability |
| `SpecialPointReactionPrioritySmokeTests` | The animation priority table — `Death/Down > Cutscene > Chain > Special Point Mini Stun > every other reaction` — plus the independent yaw/vertical/environment-safe root-motion shape and proof the legacy two-argument `WithShape` did not move |
| `SpecialPointStaggerTransactionSmokeTests` | The deferred-ChainReady transaction: undeferred stagger unchanged, ChainReady held while a transaction is open, nested deferrals, reward below max → Mini Stun only, reward or regular stagger filling the meter → Mini Stun then ChainReady, pinned meter rejecting gain, cancellation, idempotent release |

These live in `Assembly-CSharp-Editor`, so PlayMode-assembly tests of the
gameplay types are not possible. Run them from the Unity Test Runner.

### Authoring validator

`Tools → RB → Validate Special Shoot Point Authoring` checks the selected
prefabs/scene objects for a missing profile or runtime point prefab, a point
prefab with no `SphereCollider`, fewer usable anchors than the configured default
count, duplicate anchor transforms, non-positive collider radii, and inconsistent
profile clamps.

### Play Mode matrix

Not reachable from Edit Mode; run these in Unity:

- The enemy keeps fighting during Telegraph and Active, and points follow animated
  bones.
- Root, generic Mini Stun, Freeze, Full Stun, skill, melee, reload, and dash are
  all replaced by a successful Special Point Mini Stun. An active Chain Attack is
  **not**.
- The Mini Stun cancels an enemy action that had not reached its release point,
  but already spawned projectiles stay alive.
- AI, Behavior Tree, and NavMesh stay suspended through the Mini Stun and the
  optional ChainReady handoff with no one-frame movement or animation blip.
- Full animation root motion and rotation are visible.
- The reaction cannot pass through a wall, and the end position is recovered to a
  nearby NavMesh point only when it needs to be.
- World Slow stretches Telegraph, Active, cooldown, and the missing-clip fallback
  consistently with gameplay and animation time.
- An in-frame occluded point receives no helper; an off-screen point receives a
  directional marker.
- Success, timeout, and cancellation each have distinct, correct
  presentation and cleanup.
- Repeated rounds reuse pooled objects with no leaked colliders, subscriptions, or
  stale HP.
- Representative layouts with the enemy context and modules on the actor root and
  nested below it both behave.
- Damage routing: direct player weapon and Active Skill hits are accepted; melee,
  AoE, status, ally, helper, and chain damage are rejected. One projectile
  produces one point hit and one enemy-health result per enemy. A head anchor
  takes the Headshot path. Death on the final-point shot produces no Special
  reaction and no ChainReady.

# Party Follow-Up validation

`PartyFollowUpCameraHoldTests` verifies post-release hold, zero-duration return,
repeated/stale release ownership, cancellation, pause and hold expiry. Skill
release timing and world slow remain independent of the camera hold.

`PartyFollowUpPlacementTests` exercises actual camera projection and physics sight
queries: a target blocking the rear pose, partial-occlusion fallback, offscreen
poses, ignoring the caster and sensor triggers, and selecting a visible ring
pose on a temporary NavMesh. It also covers a CharacterController above an
actual MeshCollider floor and rejects destinations embedded in a solid blocker.
Run it with `CharacterPlacementResolverTests` and `CharacterPlacementBaselineTests`
when changing the shared penetration check. Playtest large enemies, crowded actors and walls
with the real body collider layers; adjust `visibilityObstructionLayers` when
authoring custom layers. Inspect both the chosen warp pose and the following
animation, since the static placement visibility score does not predict each
animation frame.

`PartyFollowUpPresentationTests` covers slow ownership during overlapping combos,
priority and cleanup against timed dash/cutscene slows, pause freezing the
world clock and expiry, and curve recovery at the cast point. Run it with
`PartyFollowUpCoreSmokeTests` in Edit Mode. The camera framing/return and actual
animation release still need the Play Mode matrix in the system document.

Run **Tools > Validation > Validate Party Follow-Up Skills** after editing
`PartyFollowUpSkillDef`, `PartyFollowUpExecutionProfile`, or character loadouts. The
validator reports empty/duplicate combo IDs, missing execution assets,
unsupported MVP triggers, invalid offer durations, and reuse of battle, Helper,
or legacy Chain Attack skills.

`PartyFollowUpCoreSmokeTests` covers the serialized `PassiveEventType` number
contract, fact/chain/provenance propagation, supported trigger values, and the
dedicated null-snapshot combo runtime entry. The broader runtime matrix and
prefab wiring checklist live in
[`Docs/SYSTEMS/PARTY_FOLLOW_UP.md`](SYSTEMS/PARTY_FOLLOW_UP.md).

## Defensive Block validation

Run `Tools > RB > Defensive Block > Run Smoke Tests` for guard sweep, rear/miss,
transition ownership and hitbox request-isolation checks. In the isolated Rector
test scene, `DefensiveBlockTestHarness.RunValidation()` exercises 4/6/8/10 m,
no-block baseline and 15 FPS through the same interruption command route. Results
are available in `ValidationReport`. The 28-case suite also checks a wall behind
Rector and disable/death/down/control-loss/reset across Begin/Loop/Impact/Exit.
Run it from the harness component's **Run Play Mode Validation** context menu.
Manually focus Game View and check C then Space, plus recoil framing and VFX.
Compile through `Assets/Scripts/CheckAssemblyBuild.ps1`, with artifacts outside Assets.
See [test instructions](SYSTEMS/DEFENSIVE_BLOCK_TEST.md).

Verified 2026-09-16: five focused smoke tests and all 28 Play Mode cases passed.
The required C# build completed with 0 errors and 79 existing warnings. A focused
Game View Space-input trial also blocked once with both HP values unchanged;
Impact reset released ownership. Reports are in the workspace's
`BuildArtifacts/DefensiveBlockPlayMode.txt` and `DefensiveBlockBuild.log`.
The Editor still reports an unrelated AutodeskInteractive shadergraph import
issue during refresh; the final Play Mode suite emitted no new errors.
### Block-ready cue checks

With auto-block off, focus Rector and press C: the gold flare should expand into
view while Block is admissible. Check that `IsReady` becomes false immediately
out of range, behind a wall, while Aires is reserved, or after accepting Block;
`IsVisible` may remain true only for the short shrinking/fading exit (default
0.12 s). The readiness query must leave Aires unreserved. Disable/reset removes
the cue immediately. Check entry interrupted by exit and reopening during exit
for smooth motion without a full-size flash.

Appear/disappear animation was sampled on the live Play Mode component: entry
at 0.045 s expands to 0.589 width / 0.207 height, hold reaches full size, and
exit at 0.06 s contracts to 0.513 width with 0.25 intensity and `IsReady=false`.
Reopening from the current width, immediate disable, and hiding at exit duration
passed. See `BuildArtifacts/BlockReadyAnimationChecks.txt`; the animation C#
build completed with 0 errors and 79 existing warnings.
The 2026-09-16 Play Mode checks passed ready/hidden/restored/accepted-command
cases; details are in `BuildArtifacts/BlockReadyCueChecks.txt` at workspace root.
After adding the cue, the final regression run passed all 28 cases and five smoke
tests; C# validation had 0 errors (79 existing warnings). The lifecycle probe now
allows five seconds for Editor stalls and reports `reached` separately from
`clean`, so a missed phase is distinguishable from a failed cleanup. The final
report and image are `BuildArtifacts/BlockReadyCueRegression.txt` and
`BuildArtifacts/BlockReadyCue.png` at workspace root.
### Defensive Block warp fade checks

Verify departure stays at the original position until `Visibility.Disappeared`,
then arrival starts fully hidden at the reserved guard point and fades back in.
Cancellation in either half must restore visibility and release both actor and
placement reservations. Put an obstacle at the destination during departure:
the landing must fail safely without moving Aires. Keep the 4/6/8 m and 15 FPS
regressions to ensure presentation does not add an unintended guard startup.

Verified 2026-09-17: all 28 Play Mode cases passed with warp fades, plus five
smoke tests. Live visibility sampling confirmed half-faded departure at the old
position, a fully hidden snap, and half-faded arrival at the reserved point.
Cancel during either fade restored full visibility; inserting a wall during
departure canceled the landing without moving the actor. The final static probe
uses zero inflation and 0.005-unit contact tolerance, avoiding false floor
penetration while still rejecting the wall. Reports:
`BuildArtifacts/DefensiveBlockWarpFadeRegression.txt` and
`BuildArtifacts/DefensiveBlockWarpFadeChecks.txt`. C# build: 0 errors, 79 existing
warnings (`BuildArtifacts/DefensiveBlockWarpFadeBuild.log`).

### Defensive Block camera checks

The smoke runner now includes three camera tests: exact pose/lens return with
entry/hold/exit blending, reset/disable and consecutive Blocks during return,
and no camera acquisition before warp arrival. Play Mode validation also checks
camera release after ordinary trials and the disable/death/down/control-loss/
reset phase matrix. Visually check the low over-shoulder angle during Impact,
then the smooth return to the pre-Block view. Inspector tuning lives on
`Main Camera > DefensiveBlockCameraShot` in the test scene.

Verified 2026-09-17: all eight smoke tests and all 28 Play Mode cases passed;
camera release passed in the ordinary trials and all 20 phase/interruption
combinations. The Impact view was inspected in Play Mode. Reports and preview:
`BuildArtifacts/DefensiveBlockCameraRegression.txt` and
`BuildArtifacts/DefensiveBlockCamera.png`. Required C# validation completed with
0 errors and 79 existing warnings (`BuildArtifacts/DefensiveBlockCameraBuild.log`).

Player framing correction, 2026-09-17: nine smoke tests passed, including the
Player anchor and fixed guard heading after movement. Live 4 m and 8 m Impact
views show Player behind Aires, with Aires between Player and Rector; both
charges blocked once with no HP loss. The 8 m shot restored its original pose
and FOV. See `BuildArtifacts/DefensiveBlockPlayerFraming.png`,
`DefensiveBlockPlayerFramingChecks.txt` and `DefensiveBlockPlayerFramingBuild.log`
(0 errors, 79 existing warnings). The full 28-case suite above predates this
framing-only correction.

### Begin-to-Impact interception

The Play Mode runner now has 31 cases. Two additional 4/8 m trials request Block
at charge normalized time 0.06, reproducing damage during the old post-warp Begin
gap. They require contact during Begin after arrival, direct Impact, one Rector
knockback/block success, no Aires knockback or HP loss, and normal cleanup/camera
return. They also reject a different request ID and a repeated Impact command.
A third case checks that Begin before warp arrival cannot intercept. Existing
geometry smoke tests retain rear/miss/moving-away rejection.
These two trials use a disposable runtime profile with Begin held for 0.4 s,
so variable Editor frame timing cannot advance to Loop before the contact under
test. Ordinary trials still use the authored 0.12 s profile; the asset is unchanged.

Verified 2026-09-17: all 31 Play Mode cases and nine smoke tests passed. Both
forced-Begin contacts reached Impact without Aires knockback, HP loss or duplicate
success; pre-arrival interception was rejected. Build completed with 0 errors
and 79 existing warnings. Reports: `BuildArtifacts/DefensiveBlockBeginImpactRegression.txt`
and `BuildArtifacts/DefensiveBlockBeginImpactBuild.log`.

## Production Defensive Block integration (2026-09-17)

The upgraded `RectorDefensiveBlock.unity` runs production prefabs, original Rector
Skill 1, normal Ctrl Block input, party spawn/binding, HUD and Cinemachine. The
older validation entries above describe historical prototype runs.

Run `Tools > RB > Defensive Block > Run Production Smoke Tests` (17 checks), then
the harness's `Run Play Mode Validation`. The expanded suite includes original
range/contact/lifecycle checks plus party selection, reservation exclusion, unsupported
character definitions, collision/vulnerability, feature disable, actual recoil,
production camera entry/return, caster isolation, character replacement and AI restore.
The no-aim extension checks planar threat prediction, lateral/departing rejection,
contact-time priority and deterministic ties. Play Mode additionally disables targeting,
clears the committed target, checks command/cue agreement and requires one successful
impact without Player/Aires HP loss through the normal interruption command.
Reaction-clock regression cases additionally exercise World Slow plus HitLag,
pause during recoil/knockback, temporary actor exemptions and Player's World Slow
exemption. They use owned slow/pause handles and release them in `finally` blocks.
Lifecycle phase-isolation cases set departure fade to zero at runtime so Editor GC
stalls cannot turn a cleanup test into a late-input test; main trials and Begin-contact
checks retain authored fade timing. Temporary animation profiles are runtime clones.

Reports: `../BuildArtifacts/DefensiveBlockProductionBuild.log`,
`../BuildArtifacts/DefensiveBlockProductionPlayMode.txt` and save-file hashes in
`../BuildArtifacts/DefensiveBlockSaveHashesBefore.json`. Production-asset smoke checks
reject dependencies under `Assets/Tests/DefensiveBlock`; old copied assets are retained
for compatibility but are not used by the upgraded scene.

At the time of this historical run, production logged missing Light/Heavy
assignments from `MeleeHitboxTrigger` and a missing AutodeskInteractive ShaderGraph.
The unified hitbox migration on 2026-09-19 subsequently removed the legacy
hitbox components; that specific missing-assignment log no longer applies.
A player build/full campaign regression is separate from C# and Editor Play Mode
validation of this integration.


Verified production regression results (2026-09-17): **41/41 Play Mode checks**,
**14/14 smoke checks**, and C# validation **0 errors / 79 existing warnings**.
Actual constrained recoil in the 8 m fixture was 1.154 m, stopping before Player.
Existing save files matched their pre-test hashes. The zero-thickness guard edge
was reproduced in `BlockProductionExitProbe.txt` before fixing the guard volume.

Development Player build attempt `build_3474b8afb3e6` finished **Failed**, with no
player output. Standalone compilation reported missing `VHierarchy` references in
`Castinrange.cs` and `Projectile.cs`, and missing `HandleOperationChanged` in
`StatusEffectModifier.cs`. The report also contains the existing AutodeskInteractive
ShaderGraph error and a tooling timeout recorded while BuildPipeline occupied the
main thread. Editor regression results above remain valid; standalone validation
has not passed. These unrelated source files were not changed for this integration.

After the build, the test scene was saved and closed and GameSetup made active.
Build processing changed instance IDs, so the temporary root-ID snapshot could not
restore activation. The 16 originally enabled roots were restored in the Editor
using the checkpoint scene's root states; EventSystem and CameraHolder stay disabled.
The existing AudioListener edit was preserved. GameSetup was saved after explicit
user approval, and temporary root activation overrides were removed. For future additive validation,
store stable GlobalObjectIds and active states before Play Mode or building, and
retain the snapshot until every expected root has been restored.

### Dash / Block keyboard bindings (2026-09-17)

The production `Inputmaneger.inputactions` now binds Dash to `<Keyboard>/shift`
(left or right Shift) and Block to `<Keyboard>/space`, retaining action/binding IDs
and prefab event wiring. The test HUD and authoring docs reflect these controls.
Play Mode callback checks on a clone of the imported asset passed 5/5: each Shift
triggered only Dash, Space only Block, and neither Ctrl triggered either action.
The temporary virtual keyboard and focus settings were removed/restored afterward.
These are binding-event checks, not a new full combat regression run.
C# validation passed with 0 errors / 79 existing warnings. Reports:
`../BuildArtifacts/BlockDashBindingsValidation.txt` and `BlockDashBindingsBuild.log`.

### Defensive Block camera settings SO (2026-09-17)

Camera tuning now lives on the bound defender profile, currently
`Assets/Data/DefensiveBlock/GuardSetting.asset`. Production authoring follows the
character's existing reference so renaming the asset does not create a replacement.
C# validation passed with 0 errors / 79 existing warnings; smoke tests passed 17/17.
Three focused Play Mode cases passed with automatic AI paused and runtime profile
clones: a 45-degree FOV reached the rendered camera; disabling Camera still allowed
one successful block without a shot; changing FOV/position/return timing on the SO
after arrival did not alter the active snapshot or prevent return. All released
the guard reservation. Existing asset tuning and unsaved scene edits were retained.
The initial probe failed three assertions before a fresh controlled run; its report
is retained and was not counted as passing validation.
Evidence: `../BuildArtifacts/DefensiveBlockCameraSettingsBuild.log`,
`DefensiveBlockCameraSettingsPlayMode.txt`, and
`DefensiveBlockCameraSettingsPlayModeRepeat.txt` in the same directory.

### Defensive Block reaction clocks (2026-09-17)

Block recoil/timeout and the shared knockback motor's displacement/recovery now
use the actor clock (`WorldDeltaTime` when `UsesWorldSlow`, otherwise scaled delta).
C# validation passed with 0 errors / 79 existing warnings; smoke tests passed 17/17.
The four new Play Mode checks passed: World Slow + HitLag kept the non-exempt
actors in reaction together; pause held both positions/phases; temporarily exempt
actors completed normally; Player knockback remained exempt from World Slow.

The initial full run passed 46/48; the wall check and ControlLoss-during-Exit check
failed (Exit was not reached). Focused repeats passed 3/3 for each without changing
collision or cleanup code. Wall separation measured 0.020 m. The cause of these
intermittent first-run failures is not established. Evidence is retained in
`../BuildArtifacts/DefensiveBlockClockBuild.log`, `DefensiveBlockClockPlayMode.txt`
and `DefensiveBlockClockFocused.txt` in the same directory.

The final full repeat passed **48/48**, including all four clock checks, with both
world and global scales restored to 1. Report:
`../BuildArtifacts/DefensiveBlockClockPlayModeRepeat.txt`. This validates the
production regression scene; it does not replace the separate live Boss Rush check.

### Defensive Block settings SO (2026-09-17)

`DefensiveBlockActorProfile` now owns impact HitLag, guard center height and VFX
lifetime alongside the existing guard/placement/animation/recoil/fade settings.
Controller tuning copies remain serialized for compatibility but are hidden.
Verified the original Aires definition still references AiresGuard and that the
asset retains HitLag defaults 0.06 s / 0.1 scale after Editor API serialization.
C# validation passed with 0 errors / 79 existing warnings; smoke tests passed 17/17.
Three focused Play Mode impacts used runtime profile/definition clones: scale 0.3,
duration-zero disabled (scale 1), and a constant 0.5 blend curve (scale 0.65).
All passed with one block, no Player/Aires HP loss, reservation released and time
scale restored to 1. Persistent gameplay profiles were not changed by these probes.
Evidence: `../BuildArtifacts/DefensiveBlockSettingsBuild.log` and
`../BuildArtifacts/DefensiveBlockSettingsPlayMode.txt`.

### Defensive Block without aiming (2026-09-17)

The command and cue now select the incoming opted-in attack independently of
Player targeting. C# validation passed with 0 errors / 79 existing warnings;
production smoke tests passed 17/17. The expanded Play Mode suite passed **44/44**
on the final run, including no committed target, cue/command agreement, lateral
rejection, unchanged 4/6/8 m success, 10 m rejection and no-block damage.

The first full run passed 42/44: Down-during-Impact did not reach Impact, and the
active-AI restore assertion failed. Both cases passed three focused repeats each,
then passed in the full rerun without changing lifecycle code. The intermittent
first-run failures are retained as evidence; their cause is not established.
Reports: `../BuildArtifacts/NoAimBlockBuild.log`, `NoAimBlockPlayMode.txt`,
`NoAimBlockFocused.txt`, and `NoAimBlockPlayModeRepeat.txt` in that same directory.
This run used the production regression scene; the full GameSetup/Basement/Boss Rush
route was not rerun and the live-impact limitation recorded below remains separate.

### Basement bootstrap recovery (2026-09-17)

The root-state recovery above re-enabled the standalone TimeSlowManager alongside
the one on System. Reproduced by entering Play Mode from GameSetup: Basement had
no SaveManager, SceneLoaderSystem or EventSystem and all four PartySlot previews
were empty. TimeSlowManager duplicate cleanup destroys its owning GameObject, so
when the shared System instance loses initialization order it removes all services
on that object. The standalone root is now inactive; the System-owned manager stays
active. Basement itself and its slot/UI scripts were not changed.

Verified the same GameSetup-to-Basement transition after the scene correction:
SaveManager and SceneLoaderSystem remain alive, exactly one active EventSystem uses
InputSystemUIInputModule, and all four slots have a selected definition and model
Animator. An EventSystem raycast at Inventory Buttom resolved that button and a
pointer-click event opened its previously closed panel. Exited Play Mode and saved
GameSetup. This correction changes scene activation and docs only; no C# build was
required.

### Live Boss Rush route: Defensive Block (2026-09-17)

**Result: route/bootstrap passed; live Block impact acceptance remains unverified.**
This is a separate run from the isolated 41-case test suite and must not be reported
as a successful production Block impact test.

- Entered Play Mode from GameSetup, reached Basement with SaveManager/EventSystem
  alive and all four saved roster previews: Roma, Aires, Feno and Abbygail.
- Used UI pointer handlers for Mobiliz, NextPage twice, and BOSS RUSH 01. The actual
  StagePlacardButton loaded MapRun with Boss Rush 01 Map Run Config and production
  party actors. Walked with the Move input and pressed F at the door, reaching
  BossTest.DeadEnd.Up through the real room transition.
- Rector spawned from the production prefab with its DefensiveBlockAttack. Its AI
  naturally cast Skills 1, 2 and 3; no skill, cooldown, HP or actor-position overrides
  were used. Skill 1 alone opened the defensive window.
- Test input used temporary paired virtual keyboard/mouse devices. Later probes
  steered the gameplay camera yaw/pitch for target tracking; this is instrumented
  Play Mode coverage, not a manual mouse-control usability test. InputSettings were
  cloned for background input and restored, and test devices removed afterward.
- The second probe pressed Ctrl three times while the ready cue reported both
  ready and visible. All three commands were accepted; two reached arrival and
  activated the Block camera. All finished without impact: **0 successful blocks**.
  One cancelled before arrival. The exact cancellation cause was not captured;
  late/off-axis approaches occurred and need a focused live-session trace.
- Cleanup after these attempts returned the camera and released Aires's reservation.
  No Rector knockback/zero-damage success assertion can be made from this run.
- During a further movement probe, Player went beyond the arena floor and fell to
  Y=-4825.99 while still Alive with HP=1553.306. This prevents further valid guard
  testing and exposes missing arena-edge containment or fall recovery; the exact
  edge and collision cause have not yet been isolated. Feno also died during the run.

Evidence: `../BuildArtifacts/BossRushLiveBlock.txt`,
`../BuildArtifacts/BossRushLiveBlockLane.txt`, and
`../BuildArtifacts/BossRushLiveState.png`. All save-file hashes matched the pre-run
snapshot. Play Mode was stopped; GameSetup is active and clean. No gameplay code or
scene changes were made in this testing pass; no C# rebuild was needed.

## Melee / Skill Consolidation Checks

Run `MeleeSkillIntegrationTests` in Edit Mode alongside
`CharacterAnimationTransitionPolicyTests`, `SkillCastTransactionSmokeTests`, and
`SkillChargeAndBarrierSmokeTests`. Validate migrated assets with
`MeleeSkillMigrationTool.ValidateAll()` and rerun migration to verify idempotency.
Inspect representative concrete prefabs with root and nested modules. Verify
frame-zero hits, buffered advances, repeated last steps, cancellation, multiple
hit windows, resource isolation, live damage stats, and Melee combat metadata.
Use the existing `CheckAssemblyBuild.ps1` workflow for C# compilation; do not
build Unity-generated projects directly.

Validation on 2026-09-19:

- `CheckAssemblyBuild.ps1`: 0 errors, 80 warnings. Unity also compiled the
  Editor assembly without errors.
- 14 Melee integration assertions passed through
  `MeleeSkillIntegrationTests.RunSmokeChecks()`, including evaluated frame-zero
  animation markers, completion, death/down/disable cleanup, buffered chaining,
  reusable runtime hosts, and root/child damage-event routing.
- 25 existing Skill transaction tests and 6 animation policy tests passed via
  direct in-Editor fixture invocation with setup/cleanup. The latter includes
  the existing observed transition matrix.
- All 7 combo assets / 8 execution skills passed migration validation. Clip
  transitions, markers, durations, stagger, and VFX counts were compared with
  retained source data. A repeated migration reported all assets unchanged.
- GRS_02's missing clip was replaced with `Rector_HavyAttack` as authorized.
  Its original asset is backed up outside Assets.
- The regular Test Runner requested saving the dirty GameSetup scene. That
  run was cancelled; assertions were then run in the existing Editor without
  saving or replacing the scene. These results do not claim Play Mode physics,
  visual/audio acceptance, or profiling.

Evidence: `../BuildArtifacts/melee-skill-build.log`,
`../BuildArtifacts/melee-skill-smoke.txt`,
`../BuildArtifacts/melee-skill-transactions.txt`,
`../BuildArtifacts/melee-skill-animation-policy.txt`, and
`../BuildArtifacts/melee-skill-migration.txt`.

## Unified Hitbox Validation (2026-09-19)

Basic Melee and ordinary Skills now build `SkillHitboxLayoutData` through the
same runtime. The old contact sampler and `Use Caster Melee Hitboxes` switch
were removed. The legacy MonoBehaviour type only retains its serialized import
schema for the editor migration; no scene/prefab references remain.

- `CheckAssemblyBuild.ps1`: 0 errors / 80 warnings. Unity compiled runtime and
  Editor scripts successfully.
- 21 `MeleeSkillIntegrationTests` cases passed in the Editor, including immediate
  hit windows, bone-motion overlap sampling, hit deduplication, self/attack-shape
  filtering, cached layouts, actor masks, model replacement, destroyed/missing
  anchors, and bone-local Load/Save round trips with nonuniform scale.
- 57 existing Skill transaction/charge/barrier and animation policy checks passed;
  another 10 defensive-block/contact-order checks passed.
- All 8 execution skills validate. Initial migration removed 10 legacy components
  from 10 concrete/inherited prefabs and created 3 approved starter layouts.
  A repeated migration changed zero assets.
- Compared all 5 original capsule shapes against the pre-migration inventory:
  radius, height, direction, center, local pose, scale and bone anchors match.
  The original target masks match on all 10 affected prefabs.
- Model-local anchor paths resolve across 13 character-definition/combo bindings,
  including the shared Rector player/enemy model. A regression fixture covers
  the separate placeholder Animator on the actor root.

The tests invoke fixtures and lifecycle methods directly in the current Editor,
without saving/replacing the user's dirty scene or entering Play Mode. Physics
queries are exercised with explicitly synchronized test transforms. This is not
full Play Mode, visual timing, animation-balance or performance acceptance.
Tune Roma/Milano's starter boxes in the actual animations next.

Evidence (workspace `BuildArtifacts`): `unified-hitbox-build.log`,
`melee-skill-smoke.txt`, `unified-hitbox-regressions.txt`,
`unified-hitbox-block.txt`, `unified-hitbox-geometry.txt`, and
`unified-hitbox-model-anchors.txt`. Migration output is also written under the
Unity project's `BuildArtifacts/unified-hitbox-migration.txt`.


## Hitbox Timeline Authoring Validation (2026-09-19)

- `SkillHitboxAuthoringTests.RunSmokeChecks()` runs 14 Editor checks and writes
  `../BuildArtifacts/hitbox-authoring-tests.txt`. It uses temporary assets with
  unique names and removes only those assets in teardown. Stop other animation
  previews before running; do not invoke this fixture during Play Mode.
- Coverage: isolated drafts; group rename references and Undo/Redo; shared Composite
  window creation/removal; stable damage/knockback mapping after reordering;
  stateless forward/backward/loop scrub; malformed windows and geometry; missing
  anchors; external source conflicts; anchor/shape and window Undo/Redo; Revert;
  all three shapes with rotated/scaled local transforms saved and reimported;
  VFX marker preservation; unrelated dirty asset remains unsaved; real animation
  preview restores pose and leaves materials unchanged without attack runtimes.
- Real-layout validation instantiates Rector, GR04 and the generic enemy prefab
  using the Roma/Milano default combos, resolves both light/heavy layouts against
  the actual model hierarchy, and destroys the instances without saving a scene.
- Runtime validation: required `CheckAssemblyBuild.ps1` succeeded with **0 errors,
  80 existing warnings**. Editor scripts compiled successfully inside Unity.
  Runtime build report: `../BuildArtifacts/hitbox-authoring-build.txt`.
- Live Editor reload check passed: a disposable Rector preview retained its dirty
  group rename across script reload, while AnimationMode stopped. Discard/close
  then removed the test actor and left AnimationMode off. Report:
  `../BuildArtifacts/hitbox-domain-reload.txt`.
- Manual acceptance: open Hitbox mode on a scene instance, drag each shape handle
  and both interval edges; confirm Undo/Redo; switch a dirty source with each
  Save/Discard/Cancel choice; close a dirty window; reload scripts while a draft
  is dirty; change the live model; enter Play Mode and confirm no preview remains.
  Repeat on Rector and GR04, and inspect the Roma/Milano starter reach.
  These interaction checks complement the automated checks and are not a full
  combat Play Mode regression test.


## Hitbox Quick Setup Validation (2026-09-19)

`SkillHitboxSetupTests.RunSmokeChecks()` passes 6 Editor checks, with its report at
`../BuildArtifacts/hitbox-setup-tests.txt`. Checks cover selecting bones and sibling
modules, rejecting multi-character containers, reusing nested authoring components,
Undo/Redo of automatic component creation, resolving the real model Animator when
an authoring component is above the context, collecting all configured skill
variants and enemy slots without runtime loadout changes, and setup on Rector,
GR04 and the generic actor using the Roma/Milano combo data. Original prefab files
remain unchanged by the scene-instance checks.

The existing 14 Hitbox authoring checks also pass (20 automated checks total).
A separate live Editor check used a disposable copy of the Rector prefab: assigning
it to Setup opened Prefab Mode, resolved the character, prepared authoring inside
that stage and selected an attack. The original prefab stayed unchanged; the test
stage and copy were removed afterwards. See `../BuildArtifacts/hitbox-setup-prefab.txt`.

Unity Editor compilation passed. The required `CheckAssemblyBuild.ps1` passed
with 0 errors and 80 existing warnings (`../BuildArtifacts/hitbox-setup-build.txt`).
This change is Editor-only; no combat Play Mode regression run was performed.


Hierarchy menu / standalone model follow-up: corrected the GameObject menu
priority to 10 so Unity propagates Edit Hitboxes to the Hierarchy context menu.
The actual Rector selection in GameSetup has an Animator and model components,
without CharacteContext. Setup now supports that rig directly, discovers matching
Character Stats by prefab/Avatar references, and deduplicates their attack entries.
The setup fixture now passes 7 checks, including a standalone Rector prefab with
no gameplay context added. Editor compilation and the required runtime build both
passed (0 errors, 80 existing warnings); see
`../BuildArtifacts/hitbox-model-setup-build.txt`.

### Defensive Block timeline timing (2026-09-19)

`DefensiveBlockTimelineTests.RunSmokeChecks()` exercises draft Undo/Redo/Revert,
isolated asset-specific save and round trip, unchanged Skill data and non-timing
profile fields, external profile/binding conflicts, invalid ranges, inclusive
runtime endpoints, main-clip coordinate mapping with a cutscene offset, and the
Rector profile binding. The report is `../BuildArtifacts/block-timeline-tests.txt`.
Six checks passed; Unity Editor compilation and `CheckAssemblyBuild.ps1` passed
(0 errors, 80 existing warnings). This change is Editor-only; no new combat
Play Mode regression run was performed.

The combined regression run passed all 27 checks (6 Block timing, 14 Hitbox
authoring, 7 setup). The open Rector timeline reported `Rector_Skill_1`,
`RectorCharge`, range `0-0.62`, Block track visible, and no unsaved draft. Production
Rector profile and Skill files remained unchanged. Console inspection found no
Block timeline errors; existing Odin `VFX Source` group errors in the untouched
`SetAnimationVfxData` Inspector and a URP shadergraph load error remain separate.

Manual authoring check: open Rector Skill 1 in Animation / VFX, drag both edges of
Block Window, scrub forward/backward and loop, Undo/Redo, then Revert. Confirm that
the window turns green only inside the configured range and the profile remains
unchanged before Save Block. Check Save/Discard/Cancel on source changes and window
close, and use a copied profile to test shared-profile saves without changing
production timing. Cutscene VFX and Basic Melee combo entries must not expose this
as their own defensive window. Preview uses only the existing animation/VFX session.

### Skill-owned Block Profile sub-assets (2026-09-19, superseded by inline settings)

The current Block authoring contract supersedes shared attack profiles: each enabled
Skill owns its `DefensiveBlockAttackProfile` sub-asset. Add Block remains a draft
until Save, foreign profiles are copied, and the Inspector binding is read-only.
Defender profiles remain character-owned. Run
`DefensiveBlockTimelineTests.RunSmokeChecks()` for 12 checks, including Add draft
Undo/Redo/Revert, creation only on Save, one sub-asset per Skill, independent Skill
duplication, legacy/foreign-profile copies, idempotent migration, conflict detection,
and preserving open unsaved timing drafts during ownership migration.

All 33 combined checks passed (12 Block, 14 Hitbox authoring, 7 setup). Unity Editor
compilation and `CheckAssemblyBuild.ps1` passed: 0 errors, 80 existing warnings.
Reports: `../BuildArtifacts/block-timeline-tests.txt`,
`../BuildArtifacts/block-subasset-build.txt`, and
`../BuildArtifacts/block-subasset-migration.txt`.

Rector Skill 1 now binds its embedded `Block Profile`. Migration preserved every
attack setting, left the external RectorCharge and defender GuardSetting files
unchanged, and retained the open timing draft without saving it. The stored window
remains 0–0.62. Unity also serialized the already-default Hitbox anchor fields in
the containing Skill file; no Hitbox placement values were changed. The production
configuration command uses the owned profile so it cannot reintroduce the legacy
external binding. No new combat Play Mode regression run was performed because
runtime execution was unchanged.

Block context-menu follow-up: right-click authoring now exposes Block commands in
the same menu as Hitbox/VFX. Unity Editor compilation passed. Menu inspection in
the live Rector window confirmed the existing-window Open/Close commands and
preserved its unsaved draft; a transient Skill without a profile exposes Add Block
Window Here. No runtime build was repeated for this Editor-only menu wiring.

The upper Block panel was subsequently removed. Save Block, Revert Block, legacy
Embed Profile on Save, and conflict/validation notices now live in the Block
context menu. The timeline row marks pending changes with an asterisk. Unity
Editor compilation passed; this UI-only change does not alter runtime or asset data.

### Hitbox workspace and timeline dragging (2026-09-20)

Hitbox mode now uses a compact character/attack header, a payload/group/shape browser,
and a separate selected-shape inspector. Its browser and inspector scroll independently
while transport and timing stay at the bottom. Setup metadata is under Setup...;
VFX authoring controls and duplicate source summaries are no longer shown in Hitbox
mode. The Animation / VFX window source was compared byte-for-byte with the pre-change
baseline and is unchanged.

All 23 Hitbox checks passed (16 authoring, 7 setup). Added coverage verifies atomic
whole-window movement, unchanged duration, clipping at 0/1, stable damage/knockback
association across shared payloads, Undo/Redo, and timeline coordinate mapping.
Unity Editor compilation and CheckAssemblyBuild.ps1 passed (0 errors, 80 existing
warnings); the build report is `../BuildArtifacts/hitbox-ui-build.txt`.

Actual MouseDown/MouseDrag/MouseUp events were sent to a temporary editor window
at 850x670. Continuous scrubbing reached both intermediate and final times, body
drag moved both endpoints, edge drag resized the interval, and mouse capture was
released. The source Skill JSON remained unchanged; the test draft was discarded,
preview stopped, and the temporary window closed. Results are in
`../BuildArtifacts/hitbox-ui-input.txt`. The final wide workspace capture is
`../BuildArtifacts/hitbox-ui-redesign.png`. No scene or combat asset was saved by
the UI check, and no combat Play Mode behavior was changed.

### Hit management in the Hitbox timeline (2026-09-20)

Right-click an empty position to add a Hit; right-click its bar to duplicate,
delete or assign groups. The selected Hit exposes timing, damage and knockback;
More settings opens policy/curve/reaction fields in the right inspector.
Only a focused timeline accepts Delete/Backspace, and never during text editing.
Composite duplication clones each affected payload's own complete step by stable
marker ID and selects a non-overlapping free interval. No available gap or invalid
timing disables duplication. Shared timing changes still require valid groups in
every affected payload before Save.

All 27 checks passed (20 authoring, 7 setup), including per-payload duplicate data,
Undo/Redo, no-space rejection, incomplete timing rejection, add/group assignment as
one undo action, safe Delete focus, and save/reload of duplicated step settings.
Reports: `../BuildArtifacts/hitbox-authoring-tests.txt` and
`../BuildArtifacts/hitbox-setup-tests.txt`. Unity Editor compilation passed;
CheckAssemblyBuild.ps1 passed with 0 errors and 80 existing warnings
(`../BuildArtifacts/hit-management-build.txt`).

A temporary 1000x760 window received real MouseDown/MouseDrag/MouseUp and Delete
events: duplicate selection, duration-preserving drag, focused removal and scrubbing
passed. Source payload/clip JSON remained unchanged. The draft was discarded,
preview stopped and window closed. See `../BuildArtifacts/hit-management-ui-input.txt`
and `../BuildArtifacts/hit-management-ui.png`. The main Animation / VFX source file
still matches its pre-change SHA256. No production asset or scene was saved, and
runtime combat behavior was not changed.

### Dragging bones into Hitbox attachment (2026-09-20)

The Bone dropdown also accepts one Hierarchy GameObject or Transform. Drops must
resolve beneath the current Follow root to the exact supplied transform; foreign
actors, persistent prefab assets, multiple objects and ambiguous paths are rejected.
Assignment reuses the existing world-pose-preserving anchor change and draft Undo.
All 22 authoring checks passed, including two new bone-drop checks for hierarchy
resolution, rejection, unchanged source data, pose preservation and Undo/Redo/Revert.
Unity Editor compilation passed. CheckAssemblyBuild.ps1 passed with 0 errors and
80 existing warnings (`../BuildArtifacts/hitbox-bone-drop-build.txt`). Authoring
results are in `../BuildArtifacts/hitbox-authoring-tests.txt`.

### Visual Bone Picker (2026-09-20)

The Hitbox Bone control now opens a picker with a projected skeleton from the
existing model pose, searchable names/paths, optional unweighted transforms,
orbit/pan/zoom, named views and explicit confirmation. Browsing or cancelling
does not edit the draft. Confirming uses the same validated anchor assignment
as Hierarchy drops, including pose preservation and Undo. The picker owns no
preview GameObjects, colliders, materials or animation playback. Source/model
invalidation, assembly reload and entering Play Mode close it.

All 24 authoring checks passed, including skeleton ancestor/socket filtering,
foreign-bone exclusion, root-relative projection, confirmation versus cancellation
and stale-draft rejection. Unity Editor compilation passed. CheckAssemblyBuild.ps1
passed with 0 errors and 80 existing warnings (`../BuildArtifacts/hitbox-bone-picker-build.txt`).
A live GR04 picker received mouse input: selecting head.x from the diagram, wheel
zoom and right-button orbit passed, while the Hitbox draft JSON remained unchanged.
Results: `../BuildArtifacts/hitbox-bone-picker-input.txt`; screenshot:
`../BuildArtifacts/hitbox-bone-picker.png`. No production asset or scene was saved.

### Simplified Bone Picker layout (2026-09-20)

Fingers and their children are excluded from both the picker list and diagram.
Weapon attachment roots are separate side buttons; meshes nested beneath them
do not become extra weapon buttons. Weapon coordinates no longer affect framing.
The default body diagram uses balanced display proportions for common body-bone
names, with twist nodes positioned along the limbs. Model pose restores actual
body coordinates, and unrecognized rigs fall back to actual coordinates.
All selections retain their original Transform/path; no rig pose or asset changes.

All 25 authoring checks passed. The two layout/collection checks passed again after
the final twist-position and weapon-root refinement. Editor compilation and
CheckAssemblyBuild.ps1 passed (0 errors, 80 existing warnings;
`../BuildArtifacts/hitbox-bone-layout-build.txt`). The live GR04 diagram was visually
checked and its detached Weapon.L button selected the original transform without
changing the draft. Input report: `../BuildArtifacts/hitbox-bone-layout-input.txt`;
screenshot: `../BuildArtifacts/hitbox-bone-picker-clean.png`.

### Bone overlap selection guard (2026-09-20)

The picker retains every joint/segment candidate under the pointer. Ambiguous
clicks (including double-clicks) open a bone-name/path chooser rather than applying
the first Transform. The selected branch draws last and labels avoid overlapping.
Targeted checks passed for coincident joints/overlapping branches, deterministic
hit candidates, preserved diagram identities, confirmation/cancellation and stale
draft rejection (`../BuildArtifacts/hitbox-bone-overlap-tests.txt`). Unity Editor
compilation and CheckAssemblyBuild.ps1 passed (0 errors, 80 existing warnings;
`../BuildArtifacts/hitbox-bone-overlap-build.txt`).

### Separate twist geometry (2026-09-20)

Twist branches now use detached parallel display segments beside the limb, with
both ends offset by 30 screen points per lane. Additional twist branches receive
separate lanes. Rendered segments and pointer hit testing share the exact same
start/end data. This changes display geometry only, preserving original transforms
and attachment paths.

Three focused checks passed: separate main/twist/extra-twist hit targets at zoom
0.25, 1, 4 and 12; existing ambiguous-hit detection; and diagram/source preservation
(`../BuildArtifacts/hitbox-twist-lanes-tests.txt`). Unity Editor compilation and
CheckAssemblyBuild.ps1 passed (0 errors, 80 existing warnings;
`../BuildArtifacts/hitbox-twist-lanes-build.txt`). The GR04 picker was visually
inspected, then actual mouse clicks selected arm_twist.r and forearm_stretch.r
directly without an overlap chooser or draft change. See
`../BuildArtifacts/hitbox-twist-lanes-input.txt` and
`../BuildArtifacts/hitbox-twist-lanes.png`.

### Weapon anchors follow the picker view (2026-09-20)

Weapon buttons now project from diagram-space side anchors through the same
orbit, scale and pan as the body. They clip at the canvas boundary and do not
stick to window edges. Projected weapon buttons that overlap use the existing
name/path chooser. No attachment paths or transforms change during navigation.

Three focused checks passed for weapon pan/zoom/orbit, separate twist picking,
and draft confirmation/cancellation (`../BuildArtifacts/hitbox-weapon-anchor-tests.txt`).
Actual GR04 mouse clicks selected Weapon.L after panning at zoom 0.8 and 1.2,
with unchanged draft data (`../BuildArtifacts/hitbox-weapon-anchor-input.txt`).
Unity Editor compilation and CheckAssemblyBuild.ps1 passed with 0 errors and
80 existing warnings (`../BuildArtifacts/hitbox-weapon-anchor-build.txt`).
### Filtered timeline source picker (2026-09-20)

Source Asset uses a searchable picker instead of Unity's unrestricted
ScriptableObject selector. Animation/VFX lists Skill, Melee Combo, Animation
Profile and Cutscene; Hitbox lists Skill and Melee Combo. Skills referenced by a
combo step with an Entry ID are hidden from the picker in both modes; select the
combo and use Entry instead. Filtering uses asset references, not names or paths.
Standalone skills and skills without a selectable owning step remain visible.
The same type filter rejects unsupported drag-and-drop assignments.
Selection still passes through the existing source-change draft guard.

Unity Editor compilation passed. A live asset inventory check found 46 supported
Animation/VFX sources and 38 Hitbox sources, including Rector_Skill_1, and rejected
an unrelated CharacterStats asset (`../BuildArtifacts/source-picker-checks.txt`).
CheckAssemblyBuild.ps1 passed with 0 errors and 80 existing warnings
(`../BuildArtifacts/source-picker-build.txt`).

After hiding combo execution skills, the live inventory contains 38 Animation/VFX
and 30 Hitbox sources (8 referenced skills hidden). Both modes retain all combos
and the standalone Rector_Skill_1; every hidden step is reachable through Entry.
Combo JSON is unchanged by discovery (`../BuildArtifacts/source-picker-combo-checks.txt`).
Unity Editor compilation and CheckAssemblyBuild.ps1 passed with 0 errors and 80
existing runtime warnings (`../BuildArtifacts/source-picker-combo-build.txt`).

### Timeline row visibility (2026-09-20)

Animation/VFX keeps required source lanes and hides unused optional rows. VFX
considers both cues and markers, including a combined cutscene reference;
Block follows its enabled draft/profile; unmapped and unknown events keep Other
Events visible. Animation and empty-space context menus can add hidden content.
Missing track lookup returns -1 instead of misrouting to the last visible row.

Focused checks pass for adding/removing VFX and unknown markers, an empty skill,
required Hitbox visibility, Block draft Add/Revert, background menu availability,
and Rector Heavy's Animation/Hitbox/Chain Window/VFX rows with unchanged combo
data (`../BuildArtifacts/timeline-visible-rows-tests.txt`). Unity Editor compilation
and CheckAssemblyBuild.ps1 pass (0 errors, 80 existing runtime warnings;
`../BuildArtifacts/timeline-visible-rows-build.txt`).
An inventory check also confirms that every existing marker maps to a visible
row across 57 source entries, including 4 combined cutscene entries.

### Combo Skills migration (2026-09-20)

Seven legacy combos (eight execution steps) now have root SkillGemDefinition
assets under `Assets/Data/Combat/ComboSkills`. Four Animation Profiles (Rector,
GR04, Roma, Milano) use the new basic-attack Skill slots. Migration preserves all
execution references, step IDs, chain windows and buffer rules; rerunning it does
not rewrite destination combo data. See `../BuildArtifacts/skill-combo-migration-initial.txt`
and `../BuildArtifacts/skill-combo-migration-checks.txt`. GameSetup's authoring
source selection was updated in memory; the dirty scene was not saved.

Runtime checks: 24 passed, including Basic Melee's opt-in Block binding to the
actual hitbox runtime, interruption/buffer cleanup, continuation into a new step,
stale Block request rejection, stable IDs, nested-combo rejection, costs, damage,
input buffering and root/nested module layouts (`../BuildArtifacts/melee-skill-smoke.txt`).
Seven character setup checks passed (`../BuildArtifacts/skill-combo-setup-tests.txt`).
Three authoring checks passed: correct root versus leaf saves and conflict rejection,
Rector Heavy timeline lanes, and representative character hitbox anchors/layouts
(`../BuildArtifacts/skill-combo-authoring-tests.txt`).

Unity Editor compilation and CheckAssemblyBuild.ps1 passed with 0 errors and 80
existing runtime warnings (`../BuildArtifacts/skill-combo-build.txt`). The new
combo root is sequenced through Basic Attack slots; paid multi-step Active Skill
sequencing is not introduced by this migration.
The live Rector Heavy window resolves through SkillComboVfxTimelineSource and
exposes Block/Add Block Window Here in its background menu
(`../BuildArtifacts/skill-combo-ui-check.txt`). The final Timeline view was
captured and visually checked (`../BuildArtifacts/skill-combo-timeline.png`).

### Legacy melee cleanup (2026-09-20)

Removed seven retired MeleeComboSO assets and their meta files after validating
all root/step references, stable IDs, chain windows and buffer rules against the
seven replacement Combo Skills. Removed the old type, import-only hitbox schema,
legacy timeline adapter and four one-time migration tools. Integration fixtures
now construct Skills directly; the obsolete migration-only assertion was retired.

The backup at `../.codex-temp/melee-legacy-cleanup-20260920` contains the retired
assets/scripts and meta files, profile snapshots, modified test/schema sources,
and SHA-256 manifests. The seven Animation Profiles were reserialized individually
to remove legacy slots (four previously populated profiles and three with only
null legacy slots). No scene save or project-wide SaveAssets was invoked.
The reference scan found no retired asset/script GUID references in serialized
assets, scenes or prefabs. All 15 retained combo/step asset files are byte-identical
to the pre-cleanup snapshot, including embedded payloads.

Validation: 23 melee runtime checks, seven character setup checks and 31 Hitbox
authoring checks passed. The seven Combo Skills/eight execution steps also pass
payload, required timeline and VFX validation. Unity Editor compilation passed;
CheckAssemblyBuild.ps1 passed with 0 errors and 80 existing warnings after running
with SDK access outside the sandbox. Reports:
`../BuildArtifacts/melee-legacy-cleanup{,-validation,-preserved,-runtime-tests,-setup-tests,-authoring-tests,-build}.txt`.

Separate Editor observations: the Console still reports existing Odin group
configuration errors for SetAnimationVfxData's VFX Source group and a URP shader
load error; these are not C# compilation failures. A broad dependency hash scan
also observed Rector_MAt.mat's head-direction properties changing while the live
Editor was open. That unrelated material change was left intact; the byte-for-byte
preservation claim above is specifically for combat Skill assets.

### Timeline Save All Changes (2026-09-20)

Both Timeline tabs now share Save All Changes below their source/attack selector.
The coordinator preflights pending Hitbox, Block and hierarchy VFX data before
writing the selected entry. The VFX snapshot is read without rebuilding hierarchy;
an unloaded entry preserves its stored cues. Dirty status includes VFX hierarchy
edits. Close/navigation confirmation uses the combined save, while Load / Sync
and Hitbox Revert remain scoped to their named sections. Timeline marker and
Chain Window edits retain their existing save-on-release behavior.

Five focused checks passed in `../BuildArtifacts/timeline-save-all-tests.txt`:
combined persistence and repeated-save stability, VFX dirty indication, unchanged
other combo Step/root files, invalid Block/VFX and stale Hitbox rejection before
any writes, existing shape round-trip and conflict guards. Test assets are
temporary and removed afterward. No live character assets or scenes were saved
to exercise the button. Profile/Cutscene VFX sources now also save only their
owner asset instead of invoking project-wide SaveAssets.

Unity Editor compilation passed. CheckAssemblyBuild.ps1 passed with 0 errors and
80 existing warnings (`../BuildArtifacts/timeline-save-all-build.txt`).

The live Rector Heavy Timeline was captured and visually checked with the new
save bar and Saved indicator (../BuildArtifacts/timeline-save-all-ui.png).

### Inline Skill Block settings and Contact mode (2026-09-20)

- Attack settings now serialize directly on `SkillGemDefinition.blockSettings` as
  `SkillDefensiveBlockSettings`. The runtime `defensiveBlock` accessor returns null
  when Can Be Blocked is disabled. Receiver configuration remains in
  `DefensiveBlockActorProfile`; approach duration is now attack-owned.
- Migrated the two opted-in Skills, preserving every old attack field. Rector
  Heavy uses Contact; Rector Skill 1 uses Timed Approach with the saved 0.22 s
  duration. Removed both embedded profiles, the orphan `RectorCharge.asset`, the
  old attack-profile type and the sub-asset ownership helper. Backup and original
  JSON: `../.codex-temp/block-inline-20260920`.
- `CheckAssemblyBuild.ps1`: 0 errors, 80 existing warnings. Unity Editor script
  recompilation completed without errors.
- Timeline draft tests: 14 passed, covering Undo/Redo, Revert, enable/disable,
  mode/duration/stand-off, independent copied Skills, source conflicts, invalid
  windows and invalid Timed Approach values. Multi-window runtime tests: 4 passed.
- Melee integration: the 23 existing checks passed. The new close-range Contact
  test stages an accepted guard, dispatches HitStart through Animancer, intercepts
  the live hitbox once, preserves player HP and interrupts the melee combo. Timed
  Approach still requires movement planning. This is an Edit Mode runtime rig,
  not a claim that the complete interactive Play Mode scene was replayed.
- Production checks: 8 passed plus 9 existing geometry/animation/camera checks.
  The old production assertion assumed step 2 was always excluded; it now accepts
  the existing authored multi-window bindings and checks invalid step sentinels.
- Source comparison confirms unchanged non-Block data on both Skills and unchanged
  receiver data except removal of the migrated duration. Unity also materialized
  the previously absent disabled/empty Combo defaults on Rector Skill 1.
- Reports: `../BuildArtifacts/block-inline-migration.txt`,
  `block-migration-preservation.txt`, `block-timeline-tests.txt`,
  `block-inline-validation.txt` and `block-inline-runtime-build.log`.
- Save All integration: both final Edit Mode checks passed, including saving the
  mode/duration/stand-off together with Hitbox and VFX, leaving other assets unsaved,
  and rejecting invalid/conflicting drafts before writes. See
  `../BuildArtifacts/block-inline-saveall-final.txt`. An intervening run entered
  Play Mode and rejected Editor-only scene dirty calls; these two checks were
  rerun after returning to Edit Mode. No scene was saved.

### Timeline Space transport and bidirectional scrubbing (2026-09-20)

Unity Editor compilation passed. Three regression checks passed for signed drag
motion and endpoint clamping, crossing backward/forward between the main clip
and cutscene, and cancellation on focus loss/source stop. Live EditorWindow input
checks passed for Space tap/repeat/Play/Pause, left/right mouse drag, Space-first
release without absolute-scrub fallthrough, and the text-entry shortcut guard.
The check restored the original playhead after testing. Report:
`../BuildArtifacts/timeline-space-controls.txt`.
Only Editor scripts changed; the runtime-only CheckAssemblyBuild scan was not
rerun because it excludes these files. Preview uses the existing deferred sample
path; no new runtime playback or asset-saving operation was introduced.

### Space scrub without a mouse click (2026-09-20)

The Timeline now requests MouseMove events. Hold Space and move the pointer left
or right; no mouse button is required. A 3-pixel threshold distinguishes pointer
jitter from intentional scrubbing. Repeated Space keydowns retain the initial
pointer/playhead origin; release remains paused after movement. Text-entry and
focus guards remain in place. The earlier click-drag instructions are superseded.
Unity Editor compilation and all three shortcut regression checks passed. Live
input checks sent no mouse clicks and passed backward/forward movement, repeat,
release, tap/jitter Play-Pause and text editing guards. Original playhead restored.
Report: `../BuildArtifacts/timeline-space-no-click.txt`. Editor-only change; the
runtime assembly scan does not include the modified scripts.

### Shared Enemy Base Block component (2026-09-20)

Moved Rector's added `DefensiveBlockAttack` to `Enemy_Base.prefab` with
`PrefabUtility.ApplyAddedComponent` and bound the Base Context reference. Verified
the Base and all four dependent enemy variants: exactly one enabled component,
Context bound, and inherited on each variant. Existing Skill settings and windows
were not changed. No scene was saved; interactive Block behavior was not replayed.
No C# source changed, so the runtime assembly build was not needed.
Report: `../BuildArtifacts/enemy-base-block-validation.txt`.
Original prefabs and metadata: `../.codex-temp/enemy-base-block-20260920`.

### Selectable Block test scene and Roma Player (2026-09-20)

- `RectorDefensiveBlock.unity` now contains four Enemy choices (Rector, GR04 and
  the two regular enemy variants), with 5/3/3/3 execution Skills respectively.
  The scene roster overrides Player to Roma and retains Aires in the other slots.
- Unity loaded the new runtime/Editor scripts. Two focused Editor checks passed:
  switching Enemy selects its own Skill and rejects foreign/invalid selections;
  catalog generation includes melee steps and Skills, excludes combo containers,
  and preserves scene-authored custom choices.
- Reloaded the saved test scene and verified the Roma reference, catalog and
  nonempty Skill references. The active GameSetup scene and its dirty state were
  preserved. The upgrade changed only the requested test scene, not production
  Skill/prefab/party configuration assets.
- `CheckAssemblyBuild.ps1`: 0 errors, 80 existing warnings. The initial sandboxed
  build could not read the Windows SDK; rerunning the same script with access to
  the installed SDK passed. Report: `../BuildArtifacts/block-test-selector-build.log`.
- Selection/scene report: `../BuildArtifacts/block-test-selector-validation.txt`.
  Original test scene: `../.codex-temp/block-test-selector/RectorDefensiveBlock.before.unity`.
- Interactive Play Mode combat and Game View visual verification were not run:
  GameSetup had unsaved edits. Open the test scene after preserving those edits,
  select Rector/GR04 and several Skills, cast, change selection mid-cast, and verify
  fresh actors/HP and release of old Block/camera state. Test Block-off Skills too.
  The old full Rector regression suite has fixed trial expectations and is not a
  general pass/fail suite for arbitrary selected Enemy/Skill pairs.

### Block test panel cursor access (2026-09-20)

The scene-local harness now starts in mouse/menu mode. F1 toggles gameplay control;
Cast Skill returns to gameplay. The harness runs after the camera, releases the
cursor while its panel is open, and suspends only the Player action map so panel
clicks do not also fire, aim, move or turn the camera. It clears held input and
restores the previously active map on resume/reset/disable without enabling a map
that was already inactive. No production camera or Player input code changed.
`CheckAssemblyBuild.ps1` passed with 0 errors and 80 existing warnings; see
`../BuildArtifacts/block-test-cursor-build.log`. Full interactive Play Mode cursor
verification remains outstanding while the open GameSetup scene has unsaved edits.
Unity Editor loaded the change. The action-map suspension/restoration check and
both existing selection/catalog checks passed. The input fixture explicitly binds
its action map because Edit Mode does not run PlayerInput.OnEnable. Report:
`../BuildArtifacts/block-test-cursor-validation.txt`.

### Aires airborne Block recoil (2026-09-20)

Reproduced in the live selectable fixture with Roma, Aires, GR04 Heavy Contact at
1.5 m. Before the fix, Aires' root jumped from 0.0833 m to 1.3083 m at Impact;
feet reached approximately 2.57 m. Temporarily disabling only the companion CC
kept its root at 0.0833 m while the airborne animation remained, isolating two causes.
The full Generic recoil source clip lifts both feet above one metre. Its import
settings were restored byte-for-byte after testing an inapplicable Feet-root option.

Companion recoil now uses the already-swept NavMesh position directly; self guard
retains CharacterController.Move. The animation profile supports an impact range
with compatible 0–1 defaults. Aires is authored to 0.55–0.68, preserving durations,
Block windows, damage and HitLag. Existing production settings are preserved by
Configure; newly created Aires animation profiles receive the grounded range.

Three Editor checks passed: Begin feet, Impact supporting foot across 61 samples,
and nearby solid landing support. `CheckAssemblyBuild.ps1` passed with 0 errors and
80 existing warnings (`../BuildArtifacts/block-grounding-build.log`). Source traces:
`block-aires-height-before.csv` and `block-aires-no-cc.csv` under `../BuildArtifacts`.
Original profile/import metadata: `../.codex-temp/block-grounding`.

Live replay of Rector Skill 1 Timed Approach at 8 m passed: Block impact occurred,
root height stayed at 0.0833 m and the highest supporting-foot position was approximately 0.23 m.
GR04 Contact replay at 1.5 m and 3 m did not reach Impact: it reported
`Missed: Player contacted before guard`. This is a remaining contact-order/eligibility
investigation, not a passing end-to-end Contact regression. The recoil movement
change occurs only after Impact, and the Begin/Loop pose was not changed.
Reports: `../BuildArtifacts/block-grounding-playmode.txt`,
`block-grounding-close-contact.txt`, and `block-aires-height-after.csv`.
The original Enemy/Skill/distance/auto-block selection is restored after replay and
the test scene is left playing with the mouse released. No scene was saved.

### Rector Heavy close Contact guard (2026-09-20)

Reproduced on the selectable Play Mode fixture: Rector Heavy at 1.5 m and 2 m
accepted the guard, but its root-motion lunge passed Aires before HitStart.
The frontal interception check rejected the attack and Player lost 9.43396 HP;
4 m succeeded. Traces: `../BuildArtifacts/block-close-before.txt` and
`block-close-before-detail.txt`.

Contact guards now cap their forward offset at the attacker's position on arrival
and hold that offset for the session. Both root-motion adapters ask the actor's
context-owned DefensiveBlockAttack to constrain forward movement at a ready guard.
This applies only to the accepted Contact window and releases on resolution,
cancellation, or window closure. Pending departure, Timed Approach, already-passed
actors, retreat, and lateral misses are not clamped. Actual hitbox interception
and already-applied damage rejection still decide success; no asset timing changed.

Validation passed:

- `CheckAssemblyBuild.ps1`: 0 errors, 80 existing warnings; Unity Editor compiled.
- Seven contact-order/geometry checks and all 24 Melee Skill integration cases.
  The integration case checks ready/pending guard, Timed Approach bypass, and release.
- Live Rector Heavy at 1, 1.5, 2, and 4 m: one Block success and zero Player HP loss.
- Live Heavy at 1.5 m without Block: zero successes and 9.43396 HP loss.
- Live Rector Skill 1 Timed Approach at 8 m: one success and zero Player HP loss.

Console also reported two URP AutodeskInteractive shadergraph load errors during
asset refresh. They are outside the changed Block code; the console is not clean.

Reports: `../BuildArtifacts/block-close-build.log`, `block-close-report.txt`,
`block-close-detail.txt`, `block-close-controls.txt`, and `melee-skill-smoke.txt`.
The original Enemy/Skill/distance/auto setting was restored with the mouse released.
No scene or gameplay asset was saved. GR04 Contact was not replayed in this fix;
the earlier GR04 limitation is not marked as resolved by the Rector results.

### Close-range Timed Approach (2026-09-21)

Rector Skill 1 at 1.5 m was rejected because the authored 1.6 m stand-off in front
of a guard placed at least 0.8 m ahead of Player required the attacker to move
backwards. The planner now retains the current attacker position for a close
frontal start. The existing motor still owns the full authored duration, animation,
hitbox suppression, cancellation and grounded endpoint validation. Facing away or
already being behind the guard remains invalid. No Skill or profile asset changed.

`CheckAssemblyBuild.ps1` passed with 0 errors and 80 existing warnings, and Unity
Editor compilation completed. Seven Contact geometry/order checks and all 24
Melee integration checks also passed. The persistent Timed Approach harness now
includes 1.5/2 m starts and the production 0.22 s duration, comparing displacement
at Impact before knockback; its full duration/distance matrix was not run here.

Focused production Play Mode replay passed at 1.5, 2, 4 and 8 m: one Impact,
zero Player/Ally HP loss, exact planned endpoint, and released guard reservation.
Close starts moved 0 m before Impact; 4/8 m starts moved approximately 1.56/3.88 m.
Game-clock acceptance-to-Impact times were 0.224–0.230 s for the authored 0.22 s
duration (completion on the next frame). The initial external observer measured
wall time between Editor callbacks and under-reported one trial at 0.199 s; the
replay uses frame game time to match the runtime clock instead.

Planning checks confirmed close acceptance plus rejection for missing ground,
facing away, and an attacker behind the guard. Reset before Impact produced no
success and released the reservation; without Block the original attack still
damaged Player. Reports: `../BuildArtifacts/timed-close-build.log`,
`timed-close-playmode.txt`, `timed-close-initial-playmode.txt`, and `melee-skill-smoke.txt`.
The original fixture selection is restored after replay; no scene was saved.
Console still contains the URP AutodeskInteractive shadergraph load errors noted
above; these were not changed by this fix.

### Block camera jump while walking backwards (2026-09-21)

Reproduced with Rector Skill 1 at 8 m using the real PlayerMovementCC move input.
The Block shot's sphere cast treated Player as an obstacle when Player walked into
the ray from the snapshotted shot origin. At near-full shot weight, the maximum
observed camera displacement between frames was 2.769 m versus 0.026 m while still.
A temporary runtime mask experiment excluding Player reduced it to 0.029 m; the
mask was restored before implementing the fix.

The Block camera now filters its own Player controller and descendant colliders
from all sphere-cast hits, then chooses the nearest remaining obstacle. It uses
a reusable buffer and an all-hits fallback if that buffer fills, so ignored Player
colliders cannot hide a wall. The shared follow/aim collision mask is unchanged.

Validation:
- `CheckAssemblyBuild.ps1`: 0 errors, 80 existing warnings; Unity Editor compiled.
- Three new Editor obstacle checks passed: root/nested Player exclusion, nearest
  solid wall behind Player on the same layer (including trigger filtering), and
  a wall behind more than 16 Player colliders. Existing shot-owner cleanup passed.
- Live stationary/backwards movement trials both blocked successfully. After the
  fix, maximum near-full-weight camera steps were 0.029/0.047 m respectively, with
  zero Player obstacle hits. The original runtime mask remained -1 throughout.

Reports: `../BuildArtifacts/block-camera-before.txt`, `block-camera-before.csv`,
`block-camera-exclude-player.txt`, `block-camera-after.txt`, `block-camera-after.csv`,
and `block-camera-build.log`. Run the focused checks via
**Tools > RB > Defensive Block > Run Camera Obstacle Tests**.
The fixture selection was restored, move input cleared and mouse released. No
scene or gameplay asset was saved. The existing URP AutodeskInteractive shadergraph
load errors remain unrelated to this camera fix.

### Opt-in real-play Block diagnostics (2026-09-21)

Follow-up fix for the MapRun capture: companion placement resolves the enabled
context CharacterController instead of Aires' animated `root.x/Position` collider.
`DefensiveBlockGroundingTests.Run` passes all 4 checks, including animation-independent
footprint selection, a clear floor, wall rejection, missing-floor/trigger rejection,
and compatibility fallback. The landing-support reflection test now resolves its
two-argument overload explicitly after diagnostics added a second overload.
Play Mode checks with production Roma/Aires/Rector in the regression scene pass
Rector Skill 1 and Heavy at both 1.5 m and 5.4 m: Aires chosen, one confirmed impact,
zero Player HP loss, zero change in Ally root Y, and guard cleanup complete in all
4 trials. Results: `P:/Game_RB_Project/BuildArtifacts/block-stable-body-playmode.txt`.
Runtime build: 0 errors / 80 existing warnings; Editor compilation passed. The
dirty GameSetup scene is retained, and no prefab/profile/scene asset is saved.
The user's next MapRun session should confirm the reported location-specific case.

Placement trace follow-up: command/snapshot logs now report the actual static
guard candidate checks (NavMesh snap/footprint, landing-floor query, collider
identity and penetration, reservation scoring, and final world tolerance).
`CharacterPlacementResolverTests.PlacementTraceNamesObstructionWithoutChangingResult`
and `PlacementTraceIdentifiesMissingNavMeshWithoutChangingResult` passed in the
Editor, comparing traced and ordinary resolution. `CheckAssemblyBuild.ps1` passed
with 0 errors / 80 existing warnings; Unity Editor compilation also passed.
Real-game reproduction in MapRun is left to the user's next capture. No Block
eligibility thresholds, profiles, prefabs, or scenes were changed for this trace.

`InterruptionCommandController.logDefensiveBlock` defaults off. Enabled commands
capture the enemy cast/window and companion readiness before selection; guard and
attack lifecycle events correlate by attack instance and request ID. A capture-only
context-menu/panel action does not issue a command. The test harness owns the same
toggle across Player resets. Console records are also appended to a per-play-session
file under `Application.persistentDataPath/Diagnostics`; file failures leave Console
logging active. No gameplay eligibility rule, prefab default or combat setting changed.

`CheckAssemblyBuild.ps1` passed with 0 errors and 80 existing warnings; Unity Editor
compilation passed. The runtime smoke replay verified disabled capture creates no
session, closed-window rejection, a deliberately reserved companion snapshot, one
successful Timed Approach and cancellation before Impact. The resulting file
contained acceptance, arrival, approach, confirmed Impact, successful/unsuccessful
cleanup and execution reset records. One hundred readiness polls plus a disabled
write appended no bytes. Final presentation changes distinguish capture snapshots
from attempts and identify closed attack gates before describing timed planning.

Reports: `../BuildArtifacts/block-diagnostics-build.log` and
`../BuildArtifacts/block-diagnostics-smoke.txt` (includes the generated session path).
The user's dirty `GameSetup` scene was preserved. The test scene was loaded only
in Play Mode via `LoadSceneInPlayMode`, then Play Mode stopped to restore GameSetup;
no scene or gameplay asset was saved. The pre-existing URP shadergraph load errors
were not changed by this work.

## Close-range Weapon Aim and Hurtbox Sweeps (2026-09-21)

`Tools > RB > Third Person > Run Aim Regression Checks` runs 14 Edit Mode
checks from `ThirdPersonAimRegressionChecks`: camera and muzzle filtering of
movement capsules/unmapped actor colliders, exact arm convergence, world cover,
legacy actors without hit zones, and exact forward near targets. Both root and
nested `CharacterColliderRefs` layouts are covered. Temporary objects/scenes are
removed without saving the current scene.

For an actual projectile check, enter the Rector defensive-block test scene,
stand close to the enemy, aim at its arm, and pause Play Mode. Run
`Tools > RB > Third Person > Run Live Aim Shot Checks`. This spawns ten
zero-spread weapon projectiles through `WeaponProjectileSpawner`, drives their
real physics and impact code, and requires damage plus normal bullet despawn at
both the equipped weapon speed and 200 units/second. Use the normal non-piercing
weapon and a test enemy with more than 100 HP. The check restores enemy HP and
physics simulation mode; it does not spend weapon ammo. Physics steps and impact
callbacks run in Play Mode, not simulated Edit Mode.

The reported pose reproduced two failures before the fix: the camera selected
`CharacterPosition` instead of a damageable hit zone (zero damage), and a correctly
aimed 50-unit/second bullet skipped `HitZone_Arm.R` between 0.02-second physics
steps (0/5 impacts). After the fix all ten arm shots damaged and despawned.
Additional live checks passed for cover before the target (zero damage), cover
behind it (target damaged), piercing without duplicate damage in the same step,
and reuse of the same pooled projectile. `CheckAssemblyBuild.ps1` passed with
0 errors and 80 warnings; Unity Editor compilation passed. Reports are in
`../BuildArtifacts/aim-hit-zone-build.log` and
`../BuildArtifacts/aim-hit-zone-regression-report.md`.

## Independent skill placement timing (2026-09-24)

`SkillPlacementTimingTests` covers Legacy Chain/Interruption compatibility,
Custom timing independence from Cast Point, shared HitStart selection, independent
missing-marker fallback, invalid normalized times, and diagnostic pose evaluation
when a rendered frame crosses the placement beat after a rotated Utility lead-in.
Run this fixture in EditMode when the Editor test runner is responsive.

Validation for this change: `CheckAssemblyBuild.ps1` passed with 0 errors and
93 warnings; full output is `../BuildArtifacts/placement-timing-build.log`.
Unity Editor compilation succeeded and the Editor assembly contains the new test
fixture. The Pipeline async test runner stalled for `SkillPlacementTimingTests`;
its cancel command also timed out, so no test-pass result is claimed. No Play Mode
combat validation was performed. The task's pending test replay request was
removed after verifying its filter, to avoid replaying it on a later domain reload.

Before adopting explicit timing on production skills, verify a slash with Cast
Point before Placement Point, a placement point before Cast Point, a multi-hit
clip selecting its first HitStart, and an interrupted cast. Check that the
payload/damage window/Chain continuation timing stays unchanged while S changes.

### Placement Point timeline authoring (2026-09-24)

The main-skill Animation / VFX timeline now exposes Placement Point separately
from Cast Point, including set-at-playhead, marker drag, and row context actions.
The placement tests also cover lane binding independence, one-step Undo/Redo of
mode and time, fallback labels, and combined main/cutscene source isolation.
The existing optional-row test includes the new row.

Validation: Unity Editor compilation passed. Invoking the fixture methods
directly in the live Editor (including all parameterized cases), plus
`TimelineHidesEmptyOptionalRowsAndKeepsUnmatchedEvents`, passed **14 cases** with
**0 failures**. This bypassed the previously stalled async Test Runner; report:
`../BuildArtifacts/placement-timeline-tests.txt`. `CheckAssemblyBuild.ps1` passed
with **0 errors, 93 warnings**; log: `../BuildArtifacts/placement-timeline-build.log`.
The open Aires_Skill_1 timeline was confirmed to expose Cast Point, Placement
Point, and Hitbox lanes. No Play Mode combat validation was performed for this
editor change.

### Party Follow-Up root-motion placement (2026-09-24)

`PartyFollowUpPlacementTests` has 10 passing Edit Mode cases, invoked directly in
the live Editor. Coverage includes existing camera/static-warp behavior, moving
Placement Point while preserving A (including yaw), rejecting a wall between S
and A, missing trajectory data, and overlapping trajectory reservations until
release. Report: `../BuildArtifacts/party-combo-placement-tests.txt`.

`AiresAuthoredClipAlignsAtSavedPlacementPoint` is an explicit Play Mode smoke
check on the same fixture (call SetUp/TearDown around it). It samples the real
Aires prefab rig and `Aires_Skill_1` through the runtime sampler, then verifies
the resolved start plus sampled displacement reaches the requested A at the
saved normalized time 0.523154. It passed; report:
`../BuildArtifacts/aires-placement-playmode.txt`. This validates sampling and
placement geometry, not a full HUD-triggered combat execution. The Editor was
returned to Edit Mode without saving the open scene.

Unity Editor compilation passed. `CheckAssemblyBuild.ps1` passed with 0 errors
and 93 warnings; log: `../BuildArtifacts/party-combo-placement-build.log`.
Aires references its dedicated AlignPlacementPointToEventTarget profile; Feno
still references its existing PlaceNearEventTarget profile. During gameplay
playtesting, check the LateUpdate placement-drift diagnostic and confirm damage,
moving-target behavior, and cancellation after commit with the authored rig.

### Party Follow-Up jump height (2026-09-24)

Placement-aligned Party Follow-Ups now sample and play full XYZ root motion. The
legacy `TryGet` sampler stays planar; `TryGetWithVerticalMotion` caches separate
samples. `PreserveVerticalMotion` keeps the authored jump relative to a grounded
takeoff: contact alignment constrains XZ, airborne poses skip ground/NavMesh
support checks, and body/ceiling sweeps and reservations remain three-dimensional.
Takeoff and landing still require support. Playback passes the Y-suppression flag
as false, and the placement diagnostic includes vertical error.

Validation: 12 `PartyFollowUpPlacementTests` cases passed, including a jump higher
than the NavMesh sampling radius, a ceiling collision, and an unsupported landing.
Five focused shared-resolver regressions passed. The static-trajectory regression
initially collided with the open GameSetup scene's Plane at the test origin; it
passed when the existing origin colliders were temporarily disabled inside a
try/finally and immediately restored. Reports:
`../BuildArtifacts/party-combo-vertical-tests.txt` and
`../BuildArtifacts/party-combo-vertical-regression.txt`.

The explicit Aires Play Mode smoke check passed using the real rig/clip: peak
root Y was 3.128841m and Y at Placement Point 0.523154 was 0.4960847m. It also
verified that a subsequent planar sample keeps Y zero in a separate cache entry.
Report: `../BuildArtifacts/aires-vertical-playmode.txt`. This checks sampling and
placement geometry; full HUD-triggered combat playback still needs playtesting.
The Editor was returned to Edit Mode without saving the scene.

Unity Editor compilation and `CheckAssemblyBuild.ps1` passed (0 errors,
93 warnings); log: `../BuildArtifacts/party-combo-vertical-build.log`.

### Aires Combo rejected in Boss Rush

Reproduced through GameSetup -> Basement's Boss Rush 01 stage placard -> MapRun
boss room. A controlled break-entry combat event offered Aires, then the real
`PartyHudPresenter.TryUseFirstFollowUp()` path rejected it with `PlacementFailed`.
The boss's CharacterPosition capsule intersected the sampled trajectory after
the short Placement Point contact window (maximum penetration 0.5488744m).
Report: `../BuildArtifacts/aires-bossrush-penetrations.txt`.

Placement now honors the execution skill's character-collision bypass across
the whole root-motion trajectory, using the CC adapter mask or the standard
Player/Enemy/Ally mask for NavMesh actors. World obstruction, support and Combo
reservations remain checked. All 13 placement cases passed, including a body
crossing after contact, the identical path blocked by a world collider, and
an overlapping reservation. Report: `../BuildArtifacts/aires-bossrush-combo-tests.txt`.

Repeated the same stage/HUD path with the real loaded Aires and boss, positioning
the boss on valid central arena NavMesh and injecting only the break-entry event.
The game was running normally before invoking HUD input: Offered -> Starting ->
Executing -> Committed, root motion active with planar suppression false, observed
root rise 3.2429m, and execution released afterward. Report:
`../BuildArtifacts/aires-bossrush-hud-final.txt`. This validates the HUD execution
route, not physical keyboard input or earning break by dealing damage. Attempts
launched while the Editor was paused hit the unscaled start timeout on resume;
the final check waited for normal frame timing before input. The placement pose
diagnostic reported 0.1243m position drift and zero yaw drift at normalized 0.525.

Unity compilation and `CheckAssemblyBuild.ps1` passed (0 errors, 93 warnings).
Build log: `../BuildArtifacts/aires-bossrush-combo-build.log`. Exited Play Mode,
restored the original play-start scene setting, and left the editing scene unsaved.
## Shared character movement validation (2026-09-29)

The movement refactor is covered by these Unity Test Runner fixtures:

- `RootMotionCollisionTests`: 16 cases covering controller and body sweeps, nested
  bodies, walls, floor contact, corners, character exclusions, mask restoration,
  actual displacement and repeated motor resolution.
- `CharacterMovementIntegrationTests`: 17 cases covering walk/controller
  equivalence at walk and dash step sizes, knockback distance and mask retention,
  gravity/floor collision, missing-body authoring, and temporary instances of
  Player, Ally Helper, Ally Stryker and Enemy E GR 01 Variant prefabs. The cleanup
  adds checks for lookup/status reads without component creation, live replacement
  bodies, and preservation of an explicit actor root across context refreshes.
- `CharacterMovementOwnershipTests.NavigationAndAdapterLifecycleMatrix`: one
  Editor coroutine test that enters Play Mode in a temporary empty scene. It runs
  18 scenarios with fresh NavMesh data and actors: all six release orders for three
  owners, original stopped/axis settings, stop-only pauses, deferred autonomy,
  disabled/off-mesh restoration, initially disabled agents, pooling/stale tokens,
  adapter rebind, actual root-motion/knockback/airborne overlap, runtime component
  creation, and planar/full-Y/suppressed-Y adapter lifetime behaviour.
- `DefensiveBlockTests.NestedKnockbackMotorMovesContextRoot`: existing regression,
  now with an authored collision body instead of relying on unchecked movement.

Run each fixture by name from the Editor Test Runner. The navigation matrix must
enter Play Mode; a NavMeshAgent does not join navigation simulation in Edit Mode.
Preserve unsaved scenes before starting and handle any Save dialog that blocks
the runner. Tests create temporary objects and do not save prefab changes.

All 35 reported tests passed, including the 18-scenario lifecycle matrix. The
required `Assets/Scripts/CheckAssemblyBuild.ps1` passed with 93 existing warnings
and 0 errors. This verifies collision/movement and navigation lifecycle behaviour;
it does not replace visual playtesting of every authored animation and map.

Legacy removal was validated with the same 35 tests and required build (93
warnings, 0 errors; `../BuildArtifacts/movement-legacy-build.log`). Collision
fixtures now call the two-argument `Move`; navigation fixtures bind their agent
once and call `PlaceOnNavMesh(position)`. The visual controller no longer binds
drivers to the vertical motor.

The nine prefabs containing `CharacterVerticalMotor` were saved through Unity to
remove its four unused driver/brain fields. Unrelated serialization changes were
restored from pre-migration backups; each prefab differs from that backup by only
the four removed entries. After reimport, Unity inspection found no stale fields
or overrides in the nine prefabs and five inheriting actor prefabs (four enemy
variants and `MinigunTerret_Summon`). The project asset text scan also found no
overrides for these field names or serialized calls to the removed binding API.
Movement integration tests were rerun against the migrated assets.

## StateHub owner and read-only API migration (2026-09-30)

Run the StateHub.Tests assembly in Edit Mode and StateHub.PlayMode.Tests in Play
Mode. The discoverable asmdef fixtures delegate to typed Editor fixtures because
gameplay remains in Assembly-CSharp. StateHubTestResults.Arm() optionally captures
Unity Test Runner callbacks across domain reload and writes official runner XML
to ../BuildArtifacts/StateHub-Runner.xml; this is not direct assertion execution.

Coverage includes the action policy and reasons, downed movement, Skill-only
locks with basic melee, tokens before Awake and independent source channels,
read-only views, query purity, explicit root/child references, idempotent life
commands, and faulting presentation subscribers. Runtime fixtures exercise
Auto/Semi/Burst reload interruption, immediate Semi fire, empty/config/stun/menu
restrictions, held-fire resumption, cancellation from a reload callback, actor
disable/enable, successful/failed FullBody dash interruption, UpperBody reload,
two UI screens plus a dialogue-style pause token, and destroyed target cleanup.

Compilation: CheckAssemblyBuild.ps1 passed with 0 errors and 93 warnings; log:
../BuildArtifacts/StateHub-Final-build.log. Unity also compiled Editor and test
assemblies. Hot Reload briefly reported the newly added notification type missing
before the full Unity asset refresh; full compilation resolved it.

Edit Mode: 18/18 passed (job e96c495439df4a69af4931499a543474).
Play Mode: 2/2 passed (job 66e19408611c45369774d52b9fb76b17), with official
runner XML exported to ../BuildArtifacts/StateHub-Runner.xml.

Static migration checks found no state-view TryChange/SetInitial/Tick callers or
external CurrentId assignments under Assets. Only StateMachine's private setter
is assigned inside StateMachine. Source/doc diff whitespace checks pass; Unity
prefab serialization retains its normal empty-field trailing spaces.

Debug migration changed 16 prefab assets. A second run reported Changed 0 assets,
Skipped 0. Serialized object/reference comparison found no original nonzero
references missing or changed; report:
../BuildArtifacts/StateHub-serialized-references.json. Legacy fields are retained.

In the RectorDefensiveBlock scene, Player, Ally and Enemy owner snapshots, fresh
fire reload cancellation, stopped reload audio handles, and stun blocking were
checked using temporary runtime gun copies. Original gun assets retain their
shootInterruptsReload=false values. Player and Enemy basic melee started/stopped.
Helper hide/show retained HP, life and an external control token. No scene was
saved by these probes.

Authoring limitations: Aires_AnimProfile has no Light melee skill or fallback
combo, so that action is rejected by its owner. MinigunTerret_Summon has no death
clip; its animation callback logs an error. Notification isolation prevents that
error from aborting life commit. These profiles were not assigned new animations.
Visual transitions and perceived audio quality still need human playtesting.

Earlier MCP Test Runner attempts intermittently timed out during domain reload.
Those attempts are not counted as passes, and direct assertion calls are not
substituted for Play Mode acceptance.

Final live probes: Player Dash start/cancel and Player/Ally Down, crawl admission
and Revive passed. Ally 1 has no DashSetting and correctly rejects Dash in its
owner; that authored Dash path remains unverified. A properly initialized turret
summon reached Dead/Despawning with one Died, CharacterDead and DespawnRequested
event despite the missing death clip, then disappeared after its despawn delay.
Enemy Die called twice yielded one Died event, 1,000 XP and three pickup objects
with no increase on the second call. This scene lacks XP/drop managers, so the
probe supplied temporary managers and an unpersisted attacker LevelSystem.
Report: ../BuildArtifacts/StateHub-runtime-acceptance.json. Exited Play Mode.

## Ally Combo camera investigation (2026-10-01)

### Repair

Feno's `Test.Feno.FollowUpTauntExecution` was rejected before presentation because
its Taunt payload requires `TauntApply` and its Skill Clip had no events. Added
that marker at normalized 0.40, after the existing Cast Point 0.35. Other clip,
cast, camera and slow settings are unchanged. Unity also serialized current
field defaults while saving this asset. Added two asset regression cases to
`PartyFollowUpCoreSmokeTests` for the Feno and Aires execution definitions.

### Compilation

Unity compiled the Editor test change; `EditorUtility.scriptCompilationFailed`
was false. No default Assembly-CSharp source changed, so the assembly build
script was not required. Console still contains the pre-existing URP
`AutodeskInteractive.shadergraph` import errors; this repair does not address them.

### Automated tests

Unity Edit Mode: **26 passed, 0 failed**, covering `PartyFollowUpCoreSmokeTests`,
`PartyFollowUpCameraHoldTests` and `PartyFollowUpPresentationTests`, including both new
asset cases. Result: `BuildArtifacts/ComboCamera/TestResult.json` in the workspace
parent directory. No new Play Mode Test Runner fixture was added or run.

### Runtime checks and limits

- In the RectorDefensiveBlock Play Mode harness, the real executor accepted and
  committed Feno's Combo after repair. `TauntApply` dispatched once on replay.
  FOV moved between 90 and 86; world scale moved between 0.11 and 1.
- Aires also accepted and committed through the real executor, with FOV 90 to 86
  and world scale 0.11 to 1. Its reported missing zoom was not reproduced here.
- A runtime instance of the production CameraHolder prefab accepted direct Combo
  focus. Both the camera FOV and its rendered projection matrix changed and
  returned, so its physical-camera setting did not prevent zoom.
- Both profiles currently reduce FOV by only four degrees and skip camera focus
  beyond a player-to-ally distance of twelve metres. These are existing settings,
  not a confirmed explanation for the user's Aires report; they were not changed.
- This was measured runtime behavior in a harness, not a visual confirmation in
  the user's combat scenario or a full HUD-triggered Combo test. Those checks
  remain open. A cold first Aires probe stalled and cancelled; the warmed probes
  committed successfully. Do not use the cold probe as proof of a production bug.
- Runtime party overrides and probe objects were discarded on leaving Play Mode.
  Restored the original clean `Assets/Scenes/GameSetup.unity` scene.

Probe results are under `BuildArtifacts/ComboCamera`: `ActualFenoAfter.json`,
`ActualFenoTimeline.json`, `ActualAiresAfter.json` and `ProductionCamera.json`.

## AI target sensor architecture (2026-10-03)

### Compilation

`CheckAssemblyBuild.ps1` passed after the final runtime source changes with
**0 errors and 93 warnings**. Log: `../BuildArtifacts/AISensor-Build.log`.
Unity compiled the new Editor and test assemblies. The Console still reports
the existing URP `AutodeskInteractive.shadergraph` asset-loading error; no C#
compilation errors were reported after the final import.

### Automated tests

- `AITargetSensor.Tests`: **26 passed, 0 failed** on the final runtime sources.
  Covers registry discovery without colliders and above 64 actors, context
  ownership, shared parents, invalidation, pooled lives, memory, threat,
  selection locks and thresholds, deterministic ties, real LOS under Taunt,
  source refresh/fallback, and snapshot/event reentrancy.
- `SkillGemDefinition.Tests`: **69 passed, 0 failed** after the final changes,
  including the existing Taunt timeline and skill/cast regression cases.
- `AITargetSensor.PlayMode.Tests`: final revision passed **3/3**, 0 failed,
  0 skipped through Unity Test Runner on 2026-10-03 at 01:50 UTC. These tests exercised
  Enemy/Ally/Summon perception, wall/grace behavior, the production firing guard,
  source hide/re-enable, pause/hit-lag/world slow/exemption, authored prefab
  references and scan allocations. The added Behavior Tree blackboard, Down
  and additive-room unload assertions also passed with the final scoring and
  ownership changes. Results and Console evidence:
  `../BuildArtifacts/AISensor-PlayMode-Final.json`.
- An earlier attempt (job `dd41243c4ec645468d395f1480c7e06e`) timed out after
  domain reload. Once the Editor responded, the final run used Unity Pipeline
  MCP and completed successfully; the timed-out attempt is not counted.

### Prefabs and runtime measurements

Unity MCP inspected all **10 sensor prefabs** for owning context, actor root
layer and aim references. Their obstacle mask is now
`Default | Ground | Ground Y | Terrain` (151553), with triggers ignored.
Seven prefab assets required writes; three inherit the migrated settings.
All **11 scenes** were inspected through preview scenes: the **7 sensor
instances** inherit the corrected mask and needed no scene overrides.
The menu `Tools/RB/AI/Validate Target Sensors` checks missing/ambiguous owners,
empty LOS masks and incompatible actor/target layers. The final execution
reported **0 issues**.

Final passing Play Mode run, after warm-up, ten scans per sensor:

| Sensors | Total scans | Total CPU time | Managed allocation |
| --- | --- | --- | --- |
| 10 | 100 | 0.452 ms | 0 bytes |
| 50 | 500 | 5.482 ms | 0 bytes |
| 100 | 1,000 | 19.198 ms | 0 bytes |

These are synchronous Editor measurements from the instrumented fixture, not
a production frame-time budget. The
authored-prefab fixture uses real Enemy, Ally and turret prefab references
with autonomous behaviors suspended. Full unrestricted combat, visual aiming
and helper/room transitions in the actual gameplay scene still require
manual playtesting. The fixture validates the production firing guard and
blackboard event path, rather than an unrestricted Behavior Tree combat loop.

Console limitations: the turret reports a missing `ThirdPersonAimBoneMap`
(visual upper-body aim disabled), and the prefab probe reports missing
`ZLZ_CharacterVFX` for rendered visibility. A missing-script warning also comes
from the vFolders asset scan. These authoring/presentation warnings and the
existing URP import error did not fail the sensor tests; they were not repaired
by this validation run. No source code changed in the final rerun, so the
previous assembly-build result remains applicable. Unity reports no compilation
failure and has exited Play Mode; `RectorDefensiveBlock.unity` is clean.
