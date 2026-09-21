# Validation

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

## Canonical Command

Run C# validation only through:

```powershell
powershell -ExecutionPolicy Bypass -File 'P:\Game_RB_Project\RB_Project\Assets\Scripts\CheckAssemblyBuild.ps1'
```

Do not run `dotnet build` directly against Unity `.csproj` files for gameplay
validation.

## Build Artifact Rule

Do not run any build command that contains either of these paths:

- `Assets\Scripts\_buildbin`
- `Assets\Scripts\_buildobj`

Build artifacts must stay outside `Assets` so Unity does not import generated
assemblies back into the project.

## What The Script Does

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

# Party Combo validation

`PartyComboCameraHoldTests` verifies post-release hold, zero-duration return,
repeated/stale release ownership, cancellation, pause and hold expiry. Skill
release timing and world slow remain independent of the camera hold.

`PartyComboPlacementTests` exercises actual camera projection and physics sight
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

`PartyComboPresentationTests` covers slow ownership during overlapping combos,
priority and cleanup against timed dash/cutscene slows, pause freezing the
world clock and expiry, and curve recovery at the cast point. Run it with
`PartyComboCoreSmokeTests` in Edit Mode. The camera framing/return and actual
animation release still need the Play Mode matrix in the system document.

Run **Tools > Validation > Validate Party Combo Skills** after editing
`PartyComboSkillDef`, `PartyComboExecutionProfile`, or character loadouts. The
validator reports empty/duplicate combo IDs, missing execution assets,
unsupported MVP triggers, invalid offer durations, and reuse of battle, Helper,
or legacy Chain Attack skills.

`PartyComboCoreSmokeTests` covers the serialized `PassiveEventType` number
contract, fact/chain/provenance propagation, supported trigger values, and the
dedicated null-snapshot combo runtime entry. The broader runtime matrix and
prefab wiring checklist live in
[`Docs/SYSTEMS/PARTY_COMBO.md`](SYSTEMS/PARTY_COMBO.md).

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
