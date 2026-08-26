# Character Placement Boundary

`CharacterPlacementResolver` is the shared boundary for relocating a character
actor during a same-frame gameplay transaction. It lives under
`Assets/Scripts/Combat/Placement` and is intentionally independent of Chain
Attack, Helper, Interruption, and Summon execution lifecycles.

## Request and result

`CharacterPlacementRequest` is an immutable input bundle containing:

- the actor root and character-position collider/footprint;
- a snapshot of the target anchor and target identity;
- authored candidate poses and deterministic candidate order;
- optional sampled planar root motion and the impact normalized time;
- optional ordered animation segments such as Utility Warp-Out tail and Attack;
- world/actor masks, NavMesh or ground policy, ignored roots/colliders, and
  runtime root-motion policy;
- an optional reservation owner.

`CharacterPlacementResult` returns the selected start and impact poses, the
candidate index, the lexicographic `CharacterPlacementScore`, and a failure
reason. The target anchor is not followed after the request is resolved.

## Evaluation boundary

The resolver performs broad non-allocating overlap checks for every candidate,
then detailed `ComputePenetration` checks for the best three candidates. Hard
NavMesh and ground-support requirements invalidate a candidate. Collision-free
candidates win first; when every candidate overlaps, the score still chooses
the least harmful deterministic result. If effective runtime planar root motion
is disabled, the animation input is ignored for position evaluation and the
trajectory is static. When animation is enabled, the resolver evaluates every
sampled pose and sweeps the footprint between consecutive poses, so a fast
root-motion clip cannot pass through a thin world collider between samples.
After a mobile candidate is snapped to NavMesh, an animation-aware request must
still predict the authored target impact within tolerance; otherwise that
candidate is rejected instead of silently moving the impact point.

`CharacterPlacementReservationRegistry.Shared` owns the single
`CharacterPlacementReservationService` used by Helper, Field Ally, Summon, and
Interruption placement. The service stores the selected request/result for the
current transaction. A later placement treats another actor's reservation as
actor overlap. Callers release by owner on completion, interruption, cancel,
actor disable, or invalid target. The service is not responsible for skill
costs, cooldowns, animation playback, or target locking. When a layer belongs
to both world and actor masks, a collider under a `CharacteContext` is
classified as actor before generic world-mask fallback.

Summon transaction reservations use `SummonContext.transform` as their owner
root. `SummonedEntityRuntime` and the physical position collider may be sibling
objects below that root, so the runtime component transform is not a sufficient
ownership boundary for transient-collider deduplication. The live collider is
authoritative after the transaction's Physics sync and reservation release.

## Module ownership

The shared boundary stays focused while each feature owns request assembly:

- `CharacterPlacementFootprint` and `CharacterPlacementFootprintUtility` own
  collider geometry conversion for Box, Capsule, and Sphere footprints plus the
  conservative fallback box. They do not depend on summon mobility, Chain
  profiles, or Helper state;
- `CharacterPlacementProbeUtility` owns summon mobility/component discovery and
  authoring error messages, then passes the selected component's dimensions and
  transform into the generic footprint conversion helpers;
- `CharacterPlacementRequest.AnchorSnapshot.Capture` owns null-safe anchor
  snapshots, while `CharacterPlacementRuntimePolicy.CreateDefault` owns the
  resolver's shared defaults;
- `ChainAttackTeleportUtility` maps profile yaw candidates and the existing
  probe collider into a central request, and keeps the legacy mutating
  `poseValidator` path separate;
- `TargetedSkillPlacementResolver` maps the cached clip/Avatar trajectory and
  target contact window into a central request;
- `SkillPayloadDef.TryGetTargetPlacement` exposes optional payload-owned caster
  placement intent. `TargetedDeliverySkillPayloadDef` supplies the horizontal
  target stand-off at the cast point, while `CompositeSkillPayloadDef` forwards
  the first child contract. `AllyHelperManager` combines that intent with the
  skill clip and cast point to derive start poses without scaling root motion;
- `SummonPlacementResolver` owns ground resolution, summon candidate rings, and
  transient reservation ownership, so an overlapping base can choose the
  least-overlapping candidate instead of failing immediately;
- Field Ally and Ally Interruption own reservation lifecycle around their
  placement transactions.

The resolver owns placement geometry and scoring. Controllers own when a
transaction starts, when the actor is snapped, when animation/payload playback
starts, and when the reservation is released. Projectile, hitbox, and skill
payload effects are outside this boundary.

Targeted Chain/Interruption placement composes the remaining Utility Warp-Out
tail with the Attack trajectory when both clips are available. The attack impact
time is remapped into that single continuous trajectory; root-motion distance is
never scaled to fit the target. If the effective runtime policy disables planar
root motion, the same request is evaluated as a static trajectory instead.

Player, Ally, and Helper actors are mobile placement consumers and therefore
must resolve on a NavMesh even when an older serialized profile has its optional
NavMesh flag disabled. The flag remains readable for asset compatibility, but it
cannot authorize an off-NavMesh mobile pose.
