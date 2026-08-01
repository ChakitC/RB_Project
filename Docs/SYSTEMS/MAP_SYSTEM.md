# Map System

The runtime map is generated as a `MapGraph`. Each `MapNode` owns one lazily
created room instance for the lifetime of the current run. A room is created
the first time its node is entered, cached by `MapNode.Id`, and disabled when
the party travels away. Returning to a visited node re-enables the same room
instance instead of instantiating its prefab again.

All cached rooms share the configured `roomSpawnAnchor`. Only the current room
may be active. Disabling a room removes its baked `NavMeshSurface` data through
the surface lifecycle; enabling the destination room adds that baked data
again before the party is warped. Room transitions never rebuild NavMesh data
at runtime.

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

## Travel And Rollback

Rooms whose definition requires clearing cannot be left while their current
`RoomController` is uncleared. `MapRunController` enforces this independently
of exit visuals and interaction locks.

A transition keeps the previous cached room and party pose until destination
placement succeeds. If any required party member cannot be placed on the new
room NavMesh, the destination is disabled, the previous room is re-enabled,
and the previous party pose and item-drop ownership are restored. Graph state
is committed only after a successful party warp.

Starting a new run destroys every cached room and clears all runtime ownership.
The cache is in-memory only; saving and loading a run across application
sessions is outside the current map runtime contract.

## Performance Validation

The current run configuration produces a small graph, so every visited room is
kept until the run ends. Profile both PC and mobile after visiting every node:

- revisiting nodes must not create additional room instances;
- only one room and one set of baked NavMesh data may be active;
- memory should stabilize after all reachable rooms have been visited;
- repeated backtracking should not produce growing GC allocations.
