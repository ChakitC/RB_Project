# Party Spawning

## Ownership

`PartySpawnPoint` owns scene-local party creation. `PartySpawnConfigSO` owns the
actor prefab, role, party index, local spawn offset, and Player UI prefab data.
`PlayerSquad.prefab` is not loaded or instantiated at runtime.

The default party contract is fixed to four roles:

| Role | Party index | Prefab |
| --- | ---: | --- |
| Player | 0 | `Player.prefab` |
| PartySlot1 | 1 | `Ally_Stryker.prefab` |
| PartySlot2 | 2 | `Ally_Stryker.prefab` |
| Helper | 3 | `Ally_Helper.prefab` |

The starting character lineup is Roma, Feno, Aires, and Milano for party
indices `0` through `3`. Basement `PartySlot` components use the active
`SaveManager.currentSlot`; their fallback ids follow the same lineup when the
active slot has no saved party data.

`CharacterDatabase.unlockEntries` contains all authored characters. Roma, Feno,
Aires, and Milano are unlocked by default. Dorothy, Noemi, Roger, and Abbygail
start locked unless their current-slot character progress explicitly records an
unlock; each locked starter-roster character costs 100 gold in the Basement
character shop. A character present in `CharacterDatabase.characters` without a
matching unlock entry remains locked and unavailable for purchase instead of
being treated as unlocked.

If save data is unavailable, each actor's existing
`CharacterContextPartyLoader` fallback character remains authoritative.

## Spawn Sequence

The marker runs early from `Awake` and creates an inactive
`PartyRuntimeRoot`. It instantiates all four actors and Player UI, then performs
runtime binding while the roots are inactive:

1. assign each `CharacterContextPartyLoader` party index, load its
   `CharacterStats`, and apply the optional AI behavior Subtree;
2. configure `FieldAllyMember` roles;
3. bind Behavior Designer's `player` GameObject in graph variables or
   GameObject shared variables;
4. rebuild `FieldAllyManager` membership;
5. bind party indices 1-3 into the Player's `PartyFormationController`;
6. bind the helper `AllyContext` into `AllyHelperManager`;
7. bind Player UI modules through `PlayerUIRuntimeBinder`;
8. call `IPartySpawnedReceiver.PrepareParty` on scene receivers;
9. activate actors, activate UI, then call `PartySpawned` and the static
   `PartySpawnPoint.Spawned` event.

For allies that share the same prefab, assign `CharacterStats.behaviorSubtree`
per character. The loader applies that Subtree while the party root is inactive
and before Behavior Designer's `player` variable is bound. A missing Subtree
keeps the prefab-authored behavior as a backward-compatible fallback.

Any exception or failed binding rolls back the incomplete runtime objects. The
system does not automatically respawn a party member after death. Scene changes
destroy the scene-local party normally; the destination scene creates its own
party from its marker.

## Stage Intro Relationship

The MapRun stage intro is not part of `PartySpawnPoint`. It runs later, inside
`MapRunController.EnterNode`, after party binding and after the Start-room warp,
and only for the Start node of a run. It reads the already-bound `PartyRuntime`
through `PartySpawnPoint.CurrentParty` and looks up actors by `ChainActorRole`.
See `Docs/SYSTEMS/MAP_SYSTEM.md` for the sequence and
`Docs/PREFABS_AND_AUTHORING.md` for the rig contract.

## Scene Contract

A gameplay scene either has exactly one active `PartySpawnPoint` or intentionally
has no runtime party. More than one marker is invalid. A scene with a marker must
not also contain manually placed Player, Ally, or Player UI contexts.

Systems such as gameplay camera or occlusion targeting must receive the runtime
player through `IPartySpawnedReceiver`. Do not add serialized references from a
scene object into `PlayerSquad.prefab` children.

The migrated gameplay scenes are:

- `MapRun`
- `Map_TestAI`
- `State_1`
- `BoosTest`

## Character-Owned Helper Loadout

`Ally_Helper.prefab` is a single shared actor: `PartyRuntimeBinder` binds it once via
`AllyHelperManager.BindHelper`, and from then on it is only ever `SetActive`-toggled, never
re-instantiated per summon.

Because it is shared, nothing character-specific may be serialized on it. Both halves of the helper
loadout live on the character asset loaded into the helper slot (party index 3):

| Data | Source |
|---|---|
| Helper procs | `helperContext.baseStats.helperProcs` |
| Manual party command | `helperContext.baseStats.helperCommandSkill` |

`CharacterSkillManager` resolves both from `ctx.baseStats`, and `AllyHelperProcController` reads its
proc list only from `AllyHelperManager.HelperSkillManager`. There is no prefab-authored fallback and
no collection from the other party members. An empty slot means the character has no such skill.

Helper Chain Attack is unaffected: it still comes from `CharacterStats.chainAttackSkill` through
`FieldAllyMember`.

### Swapping the loaded character at runtime

The helper GameObject is deactivated between summons, so it cannot notice a party change from its
own `Update`. The refresh is event-driven instead:

`CharacterContextPartyLoader.BaseStatsChanged` → `AllyHelperManager.OnHelperCharacterChanged` →
`CharacterSkillManager.RefreshCharacterOwnedLoadout()` + `AllyHelperManager.HelperLoadoutChanged`

`AllyHelperProcController` and `PartyCommandController` subscribe to `HelperLoadoutChanged` and
rebuild their proc list and HUD label from it. Swapping the character cancels any cast still running
against the previous character's skill.

If the helper rig is loaded with a character that has no stats, or one authored as `Stryker`,
`AllyHelperManager` logs one warning per invalid state and the helper contributes nothing.

Runtime state has the opposite ownership. `CharacterStats` and the skill/helper ScriptableObjects
hold configuration only - definitions, payloads, trigger settings, presentation. Cooldown remaining,
charge state, the locked target, and any active delivery runtime are runtime state and live with the
party actor and its managers.

The charge pool for a helper assist lives in the helper's own `CharacterSkillManager` orchestrator,
keyed by `SkillGemDefinition`. That survives the helper being hidden between summons, and because
`SkillChargeState` recharges on timestamps rather than ticks, the cooldown keeps running while the
helper GameObject is inactive. Charges are not persisted across a scene load - they reset to full,
matching every other skill in the game.


## Validation

Run **Tools > RB > Party > Run Party Spawn Smoke Tests**. The tests validate the
config and prefab contracts, instantiate and bind the full party in an empty
scene, and confirm that every migrated scene has one marker and no legacy
`PlayerSquad` root.

For C# validation, use `Assets/Scripts/CheckAssemblyBuild.ps1` as documented in
`Docs/VALIDATION.md`.
