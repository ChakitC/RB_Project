# Party Combo Skill System

Party Combo is an opt-in, event-driven combat system. Each field ally owns one
fixed `PartyComboSkillDef`. A qualifying combat fact opens a short-lived offer;
the player may consume that exact offer to make the owning ally execute its
dedicated combo skill. It is separate from the legacy scripted Chain Attack.

## Runtime Flow

1. `PartyRuntimeBinder` binds `PartyComboOpportunityController` after the party
   and Helper runtime are ready and before UI binding.
2. `PartyCombatEventRouter` subscribes to every unique party
   `CombatEventBus`. It assigns missing `FactId` and root `CombatChainId` values
   once at the boundary.
3. `PartyComboTriggerEvaluator` evaluates the fixed combo authored on each
   field ally's `CharacterStats`.
4. `PartyComboOpportunityController` creates at most one current offer per role
   and snapshots its target, IDs, expiry, charge eligibility, and provenance.
5. Input consumes the current `OfferId`. The controller rejects stale IDs and
   revalidates the owner, target, charge, window, and party session.
6. `PartyComboSkillExecutor` reserves the ally, applies its placement profile,
   and starts the dedicated skill through `CharacterSkillManager`.
7. A successful charge reservation commits first. Only then is
   `ComboSkillCommitted` published with its `SessionId`, `ComboWindowId`, and
   `ExecutionId`. Child facts retain that provenance.

The controller uses a pause-aware local clock. Offers and chain windows stop
while gameplay is paused, but every window remains finite. Chain lifetime and
depth limits come from `PartyComboFeatureFlags`.

## Authoring Assets

Create assets through:

- **Assets > Create > Game > Party Combo > Skill**
- **Assets > Create > Game > Party Combo > Execution Profile**
- **Assets > Create > Game > Party Combo > Feature Flags**

Assign the combo asset to `CharacterStats > Party Combo Skill`. The MVP uses
one fixed combo per field ally and supports `PartySlot1` and `PartySlot2`.
`Helper` input/API support is reserved, but Helper offer generation is not part
of this MVP.

`PartyComboSkillDef` must have a stable, unique `comboId`, a dedicated
`executionSkill`, a supported trigger, positive offer durations, and an
`executionProfile`. Do not reuse the character's battle-slot, Helper proc,
Helper command, or legacy Chain Attack skill as the combo execution skill.

Supported triggers are:

| Trigger | Accepted fact |
| --- | --- |
| `ComboSkillCommitted` | another committed Party Combo execution |
| `TargetHasStatus` | `StatusApplied` or `StatusStackChanged` with the required status/tag/stacks |
| `EnteredBreak` | `Hit` whose metadata records the ChainReady/break edge |

Reserved enum values for later phases (`FinalStrike`, `ArtsReaction`, and
`InflictionCountReached`) fail authoring validation and do not create offers.

`TargetHasStatus` reads `StatusEffectController` through the target's
`CharacteContext`; it does not discover actors through physics. The status
lifecycle publisher and the `EnteredBreak` trigger are guarded by
`phase4aPublishersEnabled`.

Damage targets that own their own stagger application must preserve
`DamageResult.StaggerApplied` and `DamageResult.EnteredChainReady` when returning
from `TakeDamage`. The Party Combo publisher consumes this edge from the
attacker's `Hit` fact; entering ChainReady visually without returning the edge
does not create an offer.

Normal Combo targets resolve through `CharacteContext` and must remain living
character effect targets. `Map_TestAI` is the one explicit authoring exception:
its lightweight `ChainAttackTestTarget` can be locked as a living placement
anchor without pretending to be a character or exposing character modules. This
keeps the Test AI Combo popup and placement testable while preserving the
context-first target contract for gameplay actors.

## Cost, Charge, and Commit Contract

Party Combo uses the execution skill's normal shared charge pool but ignores
Energy. `CharacterSkillManager` maintains a dedicated runtime entry and reads
only the Combo definition's `upgradeTree`. Its snapshot uses slot key
`party-combo` and option key `comboId`, isolating it from battle-slot upgrades.
The Skill Loadout screen includes Combo in the bottom type selector, with the
fixed Combo on the left and its upgrade tree on the right. Unlock/reset uses
the existing shared Skill Points and `activeSkillTrees` save storage; Combo
selection is never written to battle loadout or run snapshot selections.
The manager refreshes the Combo snapshot when its runtime entry is requested,
including before casting, so unlocks and resets affect subsequent casts.
The fixed combo choice is not player-selectable save data, and its transient
charge entry is rebuilt full after load under the current charge-pool contract.

Assign a dedicated `upgradeTree` on `PartyComboSkillDef`; a missing tree leaves
the Combo usable with no upgrades. Keep `comboId` and the tree ID stable after
shipping. Feno/Aires test Combo definitions have starter trees with a one-point
node reducing cooldown by 10%; these are editable test balance values.

Commit means the cast reservation succeeded and was committed. It is not
`CastReleased`, and cancellation before commit creates no combo fact. Immediate
casts raise `CastStarted` before committing, matching animated casts. Request-
scoped commit/failure/cancel callbacks prevent unrelated casts from being
mistaken for the accepted combo offer.

## Identity and Loop Safety

`PassiveEventContext` carries `FactId`, `CombatChainId`, `Depth`, and
`ComboExecutionProvenance`. `CombatEventBus` assigns IDs for new external and
child facts. Existing publishers that still emit zero IDs are normalized once
by the party router.

The controller guards three independent identities:

- `FactId + ComboId + OwnerRole` prevents duplicate delivery of one fact.
- `ComboWindowId + ComboId + OwnerRole` prevents reusing a combo in one window.
- `ExecutionId` settles pending execution facts and buffers synchronous child
  facts until the parent commit has been accepted.

Self-triggering the same combo skill is disabled by default. Old-session,
expired, too-deep, and uncommitted provenance is rejected.

## Scene and Prefab Wiring

On the player/context hierarchy add:

- `PartyComboSkillExecutor`
- `PartyComboOpportunityController`, with the executor and a
  `PartyComboFeatureFlags` asset assigned

Bind both fields on `PlayerContext` (or let its context resolution find them).
`PartyRuntimeBinder` performs runtime binding automatically.

The project prefab is wired at `Assets/Prefab/Player/Player.prefab`. Both
components live on the Player root, the `PlayerContext` fields point to those
components, and the controller uses
`Assets/Data/PartyCombo/PartyComboFeatureFlags.asset`. The feature and Phase 4a
publishers are intentionally disabled in that asset until the focused Play
Mode matrix has passed.

For HUD, add `PartyComboHudPresenter` under the player HUD and assign one
`PartyComboSlotView` for each field ally role. Each slot may bind an icon, radial
expiry fill, fallback label, and disabled-state label. `PlayerUIRuntimeBinder`
connects the presenter automatically.

The current HUD lives at
`PlayerUI/UI_Manager/PlayerHUD/PartyComboHud` in
`Assets/Prefab/User Interface/PlayerUI.prefab`. It contains two hidden-by-default
views for `PartySlot1` and `PartySlot2`, stacked on the right edge of the screen.
The presenter reveals only the view that owns an active offer. A new offer fades
and scales in, then uses a subtle pulse while it remains pressable. The popup
shows `COMBO READY`, the combo display name, the authored icon (or fallback
label), the role's input glyph, and a radial offer-lifetime fill.

Each field-ally vital block also contains a `ComboCooldown` indicator above its
HP bar. `PartyComboSlotView` polls the owning ally's combo-owned
`CharacterSkillEntry` through `PartyComboOpportunityController`; the horizontal
fill and seconds label therefore use the same shared charge pool as execution.
The indicator reads `READY` at full charge and never derives a second cooldown
timer from the popup. Both the popup and cooldown indicator remain hidden while
the runtime feature flag is disabled.

`PlayerInputHandler` exposes `OnPartyComboSlot1`, `OnPartyComboSlot2`, and
`OnPartyComboHelper`. `Assets/Input/Inputmaneger.inputactions` currently maps
`PartyComboSlot1` to keyboard `4` and gamepad D-pad Left, and
`PartyComboSlot2` to keyboard `5` and gamepad D-pad Right. The Player prefab's
`PlayerInput` Unity Events call the matching callbacks. Keep the visible glyphs
in `PartyComboHud` synchronized if these bindings change; do not edit the
`.inputactions` JSON by hand.

## Vertical-Slice Content

The default-off test fixture uses the shared
`Assets/Data/PartyCombo/FieldAllyComboExecution.asset` profile:

- Feno owns `Test.Feno.EnteredBreakCombo.asset`; an Entered Break fact from any
  active party member offers `Suppressive Break`. This prevents allied AI from
  stealing the final stagger edge without producing the player-facing offer.
- Aires owns `Test.Aires.ComboFollowUp.asset`; Feno's committed combo fact
  offers `Shielded Follow-Up`.

Both definitions are under `Assets/Data/PartyCombo/Test/` and use dedicated
execution skills that are not present in those characters' Battle or Helper
loadouts. They are validation/playtest content, not a signal to enable the
runtime flag for production rollout.

## Execution and Cleanup

An execution profile may keep the ally in place or reserve a valid position
near the event target. It may keep the final position or return to the recorded
origin. Ally autonomy is temporarily suspended through `FieldAllyMember`; the
same reservation owner is used for placement and released on success, failure,
timeout, disable, party rebind, player death, or cancellation.

Targets are snapshots. An offer does not silently retarget when the current aim
or lock-on changes. A dead or missing owner/target makes the offer invalid.
The target handle also captures the character's enabled-lifetime generation, so disabling and
re-enabling the same pooled GameObject invalidates the old offer. Explicit event targets and the
non-character `Map_TestAI` exception remain authoritative; Party Combo does not replace them with
the player's soft target.

Legacy ChainReady dispatch is the separate Aim-derived path: it snapshots the one committed
`PlayerTargetingController.CurrentTarget`. That same life-aware handle is forwarded through the
intro cutscene, coordinator, Field Ally/Helper steps, warp, facing, and skill request. A later Aim
change cannot redirect the chain, and a pooled new life cannot inherit the old meter transaction or
reservation.

## Validation

Run **Tools > Validation > Validate Party Combo Skills** after editing combo or
character assets. Edit Mode smoke coverage is in
`PartyComboCoreSmokeTests.cs`. C# compilation must use the repository's
`Assets/Scripts/CheckAssemblyBuild.ps1` workflow.

Before enabling the feature in production content, verify two-player-slot
chains, duplicate facts, status refresh/stack edges, break entry, stale input,
zero charge, pause, death, rebind, cancellation before/after commit, placement
failure, and cleanup in representative ally prefab layouts.
