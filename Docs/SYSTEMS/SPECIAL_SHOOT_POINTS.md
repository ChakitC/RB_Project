# Special Shoot Points

A reusable, opt-in enemy weak-point challenge. A Behavior Tree opens a timed set of shootable
points attached to authored bone anchors; destroying every active point in the same round always
produces a root-motion **Special Point Mini Stun** and a stagger reward, and hands off to the
existing ChainReady flow when that reward fills the meter.

The success reaction is named *Special Point Mini Stun* to distinguish it from the project's
generic Mini Stun. Generic Mini Stun behaviour is unchanged by this feature.

## Ownership

| Concern | Owner |
|---|---|
| Round state machine, anchor rotation, pool, timers, outcome | `SpecialShootPointController` |
| Shared timing, HP/stagger ratios, pool prefab, presentation | `SpecialShootPointProfileSO` |
| One point's collider, HP, hit zone, ring/break presentation | `SpecialShootPointInstance` |
| Authored bone anchors, living on the visual model | `SpecialShootPointAnchorSet` |
| Collider → point lookup for the direct-hit path | `SpecialShootPointRegistry` |
| One-hit transaction (enemy damage + point damage + reward) | `SpecialShootPointHitScope` |
| Deferred ChainReady, pinned meter, BT/NavMesh suspension | `StaggerMeter` |
| One-shot reaction playback | `CharacterAnimBrain.Locomotion_SpecialReaction` |
| Behavior Tree adapters | `Assets/Scripts/AI/SpecialShootPoint/` |

The controller is registered on `EnemyContext.SpecialShootPoints` and resolved in
`EnemyContext.ResolveReferences()`. It is enemy-only and opt-in, so it is deliberately **not** on
the shared `CharacteContext` base, and staying `null` is the normal case.

## Player-facing flow

```text
Behavior Tree trigger
    -> Telegraph (default 0.6 s; points visible, colliders disabled)
    -> Active challenge (default 4.0 s)
        -> all points destroyed
            -> Special Point stagger reward added immediately
            -> Special Point Mini Stun (one-shot, full animation root motion)
                -> stagger below max: restore normal combat state
                -> stagger at max:    existing ChainReady flow
                                      -> F Chain Attack or ChainReady timeout
                                      -> existing Full Stun handoff
        -> time expires   -> no Mini Stun, no reward, no extra punishment
        -> cancellation   -> no Mini Stun, no reward
    -> cooldown (default 8.0 s, measured from challenge resolution)
```

Rounds carry no partial progress. Every runtime point resets to full HP on the next activation.

## Activation and selection

| Rule | Behaviour |
|---|---|
| Trigger owner | `TriggerSpecialShootPointRound` Behavior Tree task |
| Trigger result | `Success` if a round starts, `Failure` if unavailable. Never stays `Running` |
| Default count | `profile.defaultPointCount` (2) |
| Per-trigger count | BT override, clamped to `maxPointCount` and the usable anchor count |
| Selection | Unique anchors per round |
| Rotation | Shuffle bag: no anchor repeats until every enabled one has been consumed |
| Visibility filtering | None. Occluded and back-facing anchors are valid results |
| Time base | Gameplay time (`WorldDeltaTime` when the actor uses world slow), never unscaled |

A round is rejected while another round is running, during cooldown, on Death/Down, during a
cinematic, Chain Attack, ChainReady, Full Stun, or post-Stagger immunity, and whenever fewer usable
anchors are authored than the round needs.

## Anchors live on the model

`SpecialShootPointAnchorSet` sits on the **visual model prefab**, next to
`CharacterColliderRefs`. Enemy models are rebuilt from that prefab at runtime, so a bone Transform
serialized on the context root would point at the destroyed model instance after the first
rebuild — the same constraint that put `CharacterColliderRefs` on the model rather than the context.

The controller resolves the set through its context and re-resolves it whenever the reference dies,
so a model swap picks the new set up automatically. The shuffle bag resets itself when the anchor
count changes. If a rebuild destroys points that were parented to bones mid-round, the controller
cancels the round rather than leaving an uncompletable challenge, and prunes the destroyed pool
entries on the next rent.

The controller also keeps a local `anchors` fallback list, appropriate only for actors whose model
is never rebuilt (turrets). The authoring validator warns when it is used on anything else.

## Point HP

Every point in a round shares the same HP: `profile.pointHealthPercentOfMaxHp` percent of the
enemy's current Max HP, clamped by `pointHealthMin` / `pointHealthMax`. Initial tuning target is
**3 %**. Anchors configure collider radius, local offset/orientation, VFX scale, and hit zone — not
an HP multiplier.

A head anchor authored as `CharacterHitZone.Head` takes the ordinary Headshot multiplier.

## Eligible damage

A point accepts damage only when **all** of these hold:

- the collision is a direct ranged hit on that point's own collider,
- the credited actor is the player (`CharacteContext.TargetIdentity == Player`),
- the source is a normal weapon or a player Active Skill reporting a real direct collision,
- the point is in the **Active** phase.

Explicitly excluded: AoE, explosions, pulses, splash, weapon-affix area damage, melee, ally /
helper / AI attacks, Chain Attack steps, and status/DoT ticks.

Critical hits, armor, hit-zone multipliers, and weapon/skill modifiers all still apply — the point
is reduced by whatever the enemy actually took.

## One damage result, never two enemy hits

`SpecialShootPointHitScope` is the whole contract:

```csharp
using (var scope = SpecialShootPointHitScope.Begin(hitCollider, target, creditedActor))
{
    DamageResult result = /* the single TakeDamage for this hit */;
    scope.ApplyPointDamage(result);
}
```

- The enemy takes damage **once**, through its ordinary pipeline. No second `TakeDamage`, no second
  normal damage event, one damage number.
- The point is reduced by that same `DamageResult.AppliedDamage`.
- Point HP feedback is the ring/crack presentation and hit flash, never a number.
- `Begin` opens `StaggerMeter.BeginDirectHitStaggerDeferral()` **before** `TakeDamage` runs;
  `Dispose` closes it. The `using` is the `try/finally` that makes an exception or early return
  unable to leave the meter permanently deferred.
- A piercing projectile may damage at most one point and the enemy's HP at most once per enemy —
  the existing per-root pierce ignore already provides this, because a point is parented under the
  enemy hierarchy. The projectile may still continue to another enemy.
- A continuous direct-hit beam is evaluated per accepted damage tick, not once per `AttackId`.

Entry points wired to this contract: `Projectile`, `SkillProjectile`, and the legacy `Bullet`. Any
future direct-hit delivery must enter the same way. Overlap/AoE and melee paths must not.

## ChainReady sequencing

The final-point hit is one atomic gameplay result:

1. Apply the shot's normal HP damage and its normal weapon/skill stagger.
2. If the shot kills or downs the enemy, Death/Down wins and the round is cancelled.
3. Apply the same resolved damage to the point.
4. If that destroys the last point, add the Special Point reward in the same transaction.
5. Below max: play only the Special Point Mini Stun.
6. At max: pin the meter, play the Mini Stun, and enter ChainReady on the animation completion
   callback (or its watchdog fallback).

`StaggerMeter.ApplyStagger` therefore no longer enters ChainReady directly; every full-meter path
routes through `RequestChainReady`, which defers while a transaction is open. The result does not
depend on whether `EnemyHealth`, the projectile, or the point callback ran first.

If an **unrelated** hit fills the meter before the round completes, the ordinary ChainReady flow
wins and the controller cancels the incomplete round — no Mini Stun, no reward.

### StaggerMeter API added for this feature

| Member | Purpose |
|---|---|
| `BeginDirectHitStaggerDeferral()` / `EndDirectHitStaggerDeferral()` | Open/close one direct-hit transaction. Nested; commits on the outermost close |
| `ApplySpecialPointReward(float, GameObject)` | Adds the round reward through the ordinary gain gate (immunity, ChainReady, and death still reject it) |
| `BeginPendingSpecialPointBreak(GameObject)` | Pins a full meter. Returns `false` when the meter is not actually full, which is how "Mini Stun only" is decided |
| `BeginSpecialPointReactionHold()` / `EndSpecialPointReactionHold()` | Single owner of BT and NavMesh suspension across the reaction and the ChainReady that may follow. `End` enters ChainReady in the same call stack when the meter is pinned |
| `ReleaseSpecialPointBreakAndEnterChainReady()` | Idempotent release. Returns whether it entered ChainReady |
| `CancelPendingSpecialPointBreak()` | Drops the pending transition for death, down, cinematic, or disable |
| `HasPendingSpecialPointBreak`, `IsDirectHitStaggerDeferred`, `IsInPostStaggerImmunity` | Read-only state |

While the meter is pinned it rejects further stagger gain and stops decay.

## Priority

```text
Death / Down > Cutscene > active Chain Attack > Special Point Mini Stun > every other reaction
```

Encoded in `CharacterAnimationTransitionPolicy` as `CharacterAnimationMode.SpecialReaction` plus
`CharacterAnimationTransitionReason.SpecialReactionOverride`, and asserted as a table in
`SpecialPointReactionPrioritySmokeTests`. See `Docs/ARCHITECTURE/ANIMATION_COMMAND_FLOW.md`.

Consequences: the reaction cannot interrupt an active Chain Attack or a cinematic; a lower-priority
reaction that is already running is replaced and the Mini Stun restarts from the beginning; already
released projectiles are never deleted — only the enemy action that had not reached its release
point is cancelled.

## Presentation

**Telegraph** — point VFX visible, collider disabled. Shots pass through to the ordinary body and
hit-zone colliders and deal normal damage.

**Active** — colliders enabled on the `Hit` layer. One shared HUD countdown for the round, not one
timer per point. Each point shows remaining HP as a ring/crack fill, never a number. Every accepted
hit flashes and plays a sound. In the last second all surviving points flash faster.

**Visibility helper** — a directional marker appears only for a point that is *outside the camera
frame*. A point that is inside the frame but occluded gets no helper, nothing renders through the
model, and there is no aim assist or projectile magnetism anywhere in this feature.

**Resolution** — a broken point disables its collider immediately, plays break VFX/audio, then
fades over `resolveFadeSeconds` (~0.2–0.3 s). A timed-out point extinguishes with a *different*
effect: success and failure must never read the same. There is no persistent limb damage, armor
break, disabled attack, or altered bone behaviour.

## Cancellation and cleanup

| Event | Round behaviour |
|---|---|
| Telegraph/Active timer expires | `TimedOut`; no reward; cooldown starts |
| Enemy dies or downs | `Cancelled`; Death/Down owns animation; UI/pool/locks cleared |
| Cutscene takes ownership | `Cancelled`; UI/pool/locks cleared |
| Chain Attack already active | Trigger rejected; the active chain is untouched |
| Unrelated stagger fills the meter first | `Cancelled`; ordinary ChainReady |
| Component disabled or scene unloaded | `Cancelled`; unsubscribe and restore owned state |
| Missing anchor/profile/runtime point | Trigger rejected; one authoring warning |
| Missing Mini Stun clip after success | Fallback lock for `missingClipFallbackSeconds`, one warning, then the normal result/ChainReady handoff |

Cooldown starts whenever an *accepted* challenge resolves — last point, timeout, or cancellation —
and does not wait for the Mini Stun, ChainReady, or Full Stun to finish. A rejected trigger or an
authoring error does not start it. Even with the numeric cooldown expired, invalid combat states
and post-Stagger immunity still reject a new round.

## Behavior Tree tasks

Category `Enemy/Special Shoot Point`. All three are thin adapters; every piece of gameplay state
lives on the controller and is never mirrored into mutable graph variables.

| Task | Kind | Behaviour |
|---|---|---|
| `TriggerSpecialShootPointRound` | Action | Optional `SharedVariable<int> pointCountOverride`. `Success` only when the controller accepts and starts |
| `IsSpecialShootPointRoundActive` | Conditional | `Success` while the round is Telegraph, Active, or Resolving |
| `CompareSpecialShootPointRoundOutcome` | Conditional | Serialized `expectedOutcome`. Compares the latest resolved round; a stale result from an earlier activation never reports `Success` |

Both conditionals return the same pure result from `OnUpdate()` and `OnReevaluateUpdate()`, so they
behave identically under conditional abort.

## Outcome identity

`SpecialShootPointOutcome` is `None`, `Succeeded`, `TimedOut`, or `Cancelled`. Every outcome is
stamped with the round's request id (`LastOutcomeRequestId`), and `CompareSpecialShootPointRoundOutcome`
records which round was in flight when its branch began, so a tree cannot consume an older
activation's result.

## Related documents

- `Docs/ARCHITECTURE/ANIMATION_COMMAND_FLOW.md` — reaction priority, root motion, terminal callback
- `Docs/SYSTEMS/HEALTH_AND_HIT_ZONES.md` — point colliders and direct-only routing
- `Docs/SYSTEMS/AI_AND_TARGETING.md` — the Behavior Tree adapters
- `Docs/PREFABS_AND_AUTHORING.md` — profile, controller, anchors, runtime point prefab, HUD
- `Docs/VALIDATION.md` — the automated suites and the authoring validator
