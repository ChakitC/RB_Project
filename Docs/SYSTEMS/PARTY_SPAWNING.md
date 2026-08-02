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

## Validation

Run **Tools > RB > Party > Run Party Spawn Smoke Tests**. The tests validate the
config and prefab contracts, instantiate and bind the full party in an empty
scene, and confirm that every migrated scene has one marker and no legacy
`PlayerSquad` root.

For C# validation, use `Assets/Scripts/CheckAssemblyBuild.ps1` as documented in
`Docs/VALIDATION.md`.
