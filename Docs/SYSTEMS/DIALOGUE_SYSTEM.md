# Dialogue System

Linear, presentation-first conversations triggered from world interact points. The whole gameplay
world freezes, the frozen view is dimmed, up to three posed character clones are lit and rendered on
top of it, and text is typed out until the player advances to the end.

Scripts live in `Assets/Scripts/Dialogue/`; editor tooling in `Assets/Scripts/Editor/Dialogue/`.

---

## Shape of a session

```
DialogueTrigger (scene)            owns repeat rules + OnCompleted scene events
  → DialogueDirector               safe-state gate, cinematic ownership, session state
      ├── DialogueWorldPauseScope  world freeze, control blocks, HUD/world-UI suppression
      ├── DialogueStage            3 isolated cells, actor clones, cameras/RTs, light rig
      ├── DialogueUI               dim + vignette + 3 actor RawImages + dialogue box
      └── DialogueInputController  advance / hold-to-skip
```

`DialogueSequenceSO` is the reusable, scene-free script. It never references scene objects — quests,
doors, and spawns hang off the trigger's `OnCompleted`, so one sequence can be played from several
places.

## Starting a conversation

`DialogueDirector.TryPlay(sequence, initiator, onCompleted)` returns `false` and touches nothing when
any of these fails:

- **`DialogueSafeStateGate`** — the initiator must be alive, grounded, in `UIStateId.Normal`, not
  dashing / stunned / knocked back, not firing / reloading / in melee, and with no skill playback
  still running. Everything is read from the existing `StateHub` and character modules; dialogue adds
  no new gameplay flag.
- **`CutsceneDirector.TryBegin`** — exclusive ownership of the single cinematic stage. Busy means
  **reject**; nothing is queued.
- **Sequence validity** — a sequence with no `dialogueId` or no lines is unplayable.
- **Required cast** — every opening cast entry with `optional` off must resolve to a real visual.
- **Interact input** — the initiator's `PlayerInput`, its `Interract` action, and at least one usable
  non-composite binding must all exist.

**These checks are a transaction, and they all run before any world state moves.** Casting and input
are resolved inside `TryPlay`, not in the play coroutine, so a refusal hands the game back exactly as
it was: time is not paused, control tokens are not taken, the HUD is not hidden, gameplay input is
not deactivated, and cinematic ownership is released again. Once the coroutine starts, "refuse" is no
longer available — only "abort", which the player sees as a visible flicker.

The input check earns its place: the old code deactivated gameplay input *before* discovering whether
it had anything to listen with, so a renamed action or an empty binding left the player frozen in a
conversation that could never be advanced. There is deliberately no hard-coded fallback key — one
would hide a broken Input Actions asset and ignore whatever the player rebound interact to.

### `optional` on a cast entry

| `optional` | unresolved key |
|---|---|
| on (default) | slot is left empty, remaining portraits re-centre |
| off | `TryPlay` refuses, with a warning naming the sequence and the missing key |

Use `off` only when the lines make no sense without that character. It applies to the **opening cast
only**: a `stageChanges` entry that cannot resolve mid-conversation keeps whoever is already standing
there and warns, because refusing is no longer possible by then.

A required *party role* cannot be checked in the editor — whether `role.PartySlot2` resolves depends
on the party the player deployed — so the validator flags required roles and explains that the
conversation will refuse to start when that slot is empty.

## The freeze

`DialogueWorldPauseScope` holds:

| What | How |
|---|---|
| World time | `GlobalTimeScaleManager.AcquirePauseToken()` → `Time.timeScale = 0` |
| Party input | `StateHub.AcquireExternalControlBlockToken(Move\|Shoot\|Skill\|Rotate)` per actor |
| HUD | `UIManager.SetHudVisible(false)` |
| World-space UI | the `WorldUI` layer is taken out of every camera's culling mask and restored |

Gameplay camera input is already suppressed by `GameplayCameraController` while a cinematic is
active, so the dialogue does not touch the camera at all.

**Everything the dialogue itself does runs unscaled** — the typewriter, the fades, the input hold,
and the actor poses (`Animator.updateMode = UnscaledTime`). Audio is unaffected by `timeScale`, so
voice plays normally.

The pause is **token-based**. `GlobalTimeScaleManager.SetPaused(bool)` still exists and is backed by
its own token, so an old caller flipping it off can no longer thaw a world another system is holding.

## Actor clones

`DialogueActorCloneFactory` clones the live character's
`CharacterVisualController.ModelRoot` — the subtree that already holds the built model, the mounted
weapon, and any active form override. That is why the stage always shows the player's real
appearance and equipment without the dialogue system knowing anything about equipment.

The clone is **presentation only**:

- It is instantiated under an **inactive** staging root, so no gameplay component ever gets an
  `Awake`.
## Rendering channels

The stage has **no Unity layer of its own**. Clones sit on layer 0 like every other character, and
isolation is carried by rendering layers plus the distance the stage sits at.

That is a deliberate reversal of the original design, forced by ASP. Two of its renderer features
filter by Unity layer *as well as* by rendering layer — `ASPMeshOutlineRendererFeature` and
`ASPDepthOffsetShadowFeature` — and both are authored for layer 0. Their `Layer` field holds a
single layer, not a mask, so one renderer cannot serve both a gameplay layer and a dialogue layer.
A second URP renderer is not a way out either: ASP's full-screen passes keep per-pipeline state, and
two renderers drawing in the same frame produced a badly distorted portrait (characters smeared into
vertical streaks). With clones on a dedicated layer they simply fell out of both passes and rendered
flatter than the same character does in gameplay.

| channel | value | why |
|---|---|---|
| clone Unity layer | 0 | the only layer ASP's layer-filtered features draw |
| clone `renderingLayerMask` | 38 = bits 1, 2, 5 | bit 1 depth-offset shadow, bit 2 mesh outline, bit 5 dialogue lights |
| dialogue light `renderingLayerMask` | 32 = bit 5 only | a world renderer answers to every bit, so a dialogue light claiming any other bit would light the whole level |
| world directional light | bit 0 | the one bit clones deliberately do **not** claim — that omission is what keeps the sun off the stage |
| stage position | `y = -20000` | see below |

**Distance is load-bearing now, so it has margin.** The stage used to sit at `y = -5000`, which was
fine while a layer kept it out of the gameplay camera. It is not fine on layer 0: measured against
the live gameplay camera, the nearest clone was 5000.0 units away against a 5000 far plane — already
inside it, and saved only by the camera happening to look horizontally rather than down. At
`y = -20000` the margin is 15000 units. A portrait camera cannot reach the world in the other
direction either; its far plane is under 11 units.

The trade is honest: the clones used to be blocked from the gameplay view by two independent
mechanisms (Unity layer and rendering layer) and now have one plus distance. A portrait camera also
went from "draws only tagged actors" to "draws whatever layer-0 object is within ~10 units of the
stage" — nothing is, but that is now a thing that could go wrong.

`DialogueLayers.AspFeatureRenderingLayerMask` mirrors values authored on the URP renderer, so
`DialogueAuthoringValidator` compares the two and reports drift. Retuning those features without it
would silently flatten every portrait.

- Every component that is not `Transform`, `Animator`, `SkinnedMeshRenderer`, `MeshRenderer`,
  `MeshFilter`, or `ASP.ASPCharacterPanel` is destroyed — a whitelist, so a newly added gameplay
  component cannot ride along. Scripts are removed first so `[RequireComponent]` dependencies do not
  block the built-ins.
- No `CharacteContext`, AI, collider, rigidbody, agent, VFX, or combat state survives.

`ASPCharacterPanel` is on the whitelist because **a shader fed by a script needs that script**. Its
`Update` writes `_CharacterCenterWS`, `_FaceFrontDirection` and `_FaceRightDirection` into the
ASP/Character and ASP/Eye materials every frame. Stripping it does not reset those properties, it
freezes them at whatever the live character last wrote — a position out in the gameplay world — so
the clone renders its shadow and face lighting for somewhere it is not standing. It already lives
inside every character's `ModelRoot`, so the clone carries one; the strip was simply killing it.

Keeping it is safe in both directions: `Update` is driven by the engine frame rather than
`timeScale`, so it keeps working while the world is frozen at 0, and `Start` → `SetupMaterialID`
goes through `renderer.materials`, giving the clone its own material instances so it can never write
back into the live character.

**If another script-driven shader is added to the characters, it has to be whitelisted too** — the
symptom is subtle (shading computed for the wrong world position), not a missing model.

Real actors are never moved onto the stage. (`StageIntroActorScope` warps the real party; this system
deliberately does not.)

## Casting a scene NPC

Party members are discovered automatically through `PartyRuntime` and keyed by
`CharacterStats.characterId`. An NPC usually has no `CharacteContext` at all — it is a plain model
prefab — so it declares itself instead:

1. Put **`DialogueStageActorSource`** on the NPC. Set `characterId` (use an `npc.` prefix so it can
   never collide with a `CharacterStats.characterId`), `displayName`, and optionally `modelRoot`
   (empty clones the NPC's own GameObject, which is what a model prefab wants).
2. List it in the `DialogueTrigger`'s **Stage cast**. Left empty, the trigger picks up any
   `DialogueStageActorSource` on itself or its children, so the usual case needs no wiring.
3. Cast that id in the sequence like any other actor, and give it a
   `CharacterDialogueAnimationProfile` registered in the database.

Both kinds of source arrive at the stage as a `DialogueCastSource` — a transform to clone plus a
display name — so nothing downstream knows or cares which one it came from. Scene actors are
registered **after** the party, so an NPC deliberately cast under a party member's id wins.

The stage still has exactly **three slots**: casting an NPC means one party member does not appear.

A line can also be spoken by someone who is not on stage at all — leave `speakerCharacterId` empty
and set `speakerNameOverride`. The name plate shows and no actor is emphasized.

## Known limitations

**The ally Helper does not render on stage.** The helper is a summon that is kept
`SetActive(false)` between summons. A clone built from it comes out active, on the right layer, with
enabled renderers, valid meshes, opaque materials, internal bones, and correct skinned bounds — and
still draws zero pixels. Ruled out so far: culling mask, `Renderer.enabled`, `activeInHierarchy`,
`forceRenderingOff`, material alpha and property blocks, bone scale, bones referencing transforms
outside the clone, stale skinned bounds, and calling `AllyHelperManager.BeginCinematicAppearance()`
before cloning. Until this is understood, **cast characters that are active in the world**; a helper
in the cast leaves its slot empty.

**Dialogue pose clips bind by transform path, not by retargeting.** These avatars are generic, not
humanoid, so a clip drives whatever bone paths it names. In practice the party rigs **do** share the
skeleton, so clips transfer between them:

```
clip Roma_Idle.Battle, core chain root/c_traj/root.x/spine_01.x/spine_02.x/neck.x/head.x
  Roma 6/6   Abbygail 6/6   Milano 6/6   Feno 6/6   Aires 6/6
```

Verified: Abbygail poses correctly from Roma's idle clip. The curves that miss are the *props* of the
clip's owner (`Roma.Carrots`, `Bag01.L/R`) — they simply do not bind, which is harmless.

Two caveats that remain. Proportions differ, so a borrowed pose can read slightly off (an arm angle
tuned for one body on another). And a character's **own** accessories are not animated by someone
else's clip; they hold their bind pose relative to their parent bone.

A clip authored on a rig that does **not** share this skeleton would fold the model over, so check the
core chain before borrowing.

**There is no Thai-capable TMP font in the project.** Thai lines render as tofu boxes with
`LiberationSans SDF`. A font asset covering U+0E01–U+0E5B has to be authored before shipping Thai
dialogue.

## Rendering

| Channel | Used by |
|---|---|
| Unity layer 0 (`Default`) | actor clones, like every other character — see **Rendering channels** for why the stage has no layer of its own |
| Rendering layer 5, named `Dialogue` | dialogue lights, and claimed by clone renderers, so world lights never touch the clones and dialogue lights never touch the world |
| Rendering layers 1 and 2 | claimed by clone renderers only, so ASP's layer-filtered features draw them |

Each slot is an isolated cell 100 m from its neighbours. Its camera clears to transparent black into
a runtime RenderTexture sized to one third of the current screen width by the full screen height.
`DialogueStage` samples the active pose's renderer bounds and fits that slot's camera without moving
the actor, so different model pivots and body sizes do not become layout offsets. The equal-third
`RawImage` anchors compose correctly at 16:9 and ultrawide resolutions.

`DialogueUI` draws, back to front: dim (alpha 0.5–0.6) → vignette → three actor RawImages → dialogue
box. The background is the frozen gameplay view itself; there is no cinematic camera move and no
blur in v1, and the presentation scene renders no environment of its own.

## The presentation scene

`DialoguePresentation.unity` is loaded **additively and kept loaded** for the whole gameplay
lifetime — building the stage on demand would hitch at the exact moment the player triggers a
conversation. Its camera and light rig are disabled whenever no dialogue is playing, so an idle
stage costs nothing.

`DialoguePresentationSceneLoader` (on the boot `System` object) preloads it and re-loads it after any
single-mode scene load, because a Single load unloads every additive scene.

## Slots and emphasis

Three fixed UI slots: `Left`, `Center`, `Right`. A cast of **one, two, or three** is supported: a slot
with no cast member has its portrait image *and* its camera switched off, so it costs nothing and
leaves no blank panel.

The occupied portraits are **re-centred as a group** when the conversation opens
(`DialogueUI.LayoutOccupiedPortraits`). Each portrait always keeps a band exactly one third of the
screen — its RenderTexture is drawn into that band, so widening it would stretch the character — and
only the band's position moves. Occupied slots keep their Left→Right order, so nobody swaps sides.

| Cast | Bands (screen fraction) | Measured |
|---|---|---|
| 3 | the authored thirds | `0.000–0.333`, `0.333–0.667`, `0.667–1.000` |
| 2 | straddling the centre, no hole in the middle | `0.167–0.500`, `0.500–0.833` |
| 1 | dead centre, whichever slot the author picked | `0.333–0.667` |

That last row matters: a solo actor cast into `Left` still lands in the middle rather than stranded
against the edge, so an author does not have to know the slot layout to compose a one-shot line.

**Emphasis must not move the portrait vertically.** `speakingOffset` defaults to `(0, 0)`: lifting
the speaker's portrait undoes the head line the camera framing works to establish, and reads on
screen as sloppy alignment rather than as emphasis — especially next to a character with taller
headgear. Emphasis is carried by scale, tint and draw order instead.

**Portraits scale about their bottom edge**, not their centre. `DialogueUI.CapturePortraitLayout`
forces `pivot = (0.5, 0)` on every band, and the scene builder authors the same value. The bands are
authored to fill the screen exactly (anchors `0 -> 1` vertically, zero offsets, RenderTexture sized
`Screen.width / 3 x Screen.height`), so emphasis scaling has no headroom to grow into and can only
take away. A centre pivot spends a `0.94` shrink on both edges — 32px off the top and 32px off the
bottom at 1080p — which lifts every listening portrait off the bottom of the screen and reads as a
frame that fails to reach the floor. A bottom pivot spends the whole 65px on the top instead, where
every cell already has empty headroom above the head, and pins the floor line so the cast never
appears to hover at different heights.

Because the bands are stretched (`sizeDelta` is zero on both axes), moving the pivot leaves the rect
exactly where it is; only the origin that `localScale` multiplies around changes.

`speakingScale` should not be raised above `1`. The RenderTexture is authored at exactly the band's
pixel size, so `1` is a 1:1 blit; anything larger upsamples and makes the one portrait the player is
actually looking at the blurriest thing on screen. To make a speaker read larger, take size away from
the listeners rather than adding it to the speaker.

**A speaker change blends; it does not cut.** Both halves run a clamped 0..1 clock through
`Mathf.SmoothStep` over 0.25 unscaled seconds — `DialogueUI.emphasisBlendSeconds` for the portrait's
scale and tint, `DialogueStage.emphasisBlendSeconds` for the 3D key/rim intensities. **Keep the two
matched**; they are two halves of one change. Neither chases its target with
`Lerp(current, target, dt / duration)`: that is exponential decay, which is fastest on the first
frame and never lands, and it read as a jerk followed by a crawl. `SetSpeaker` re-snapshots the
current values as the blend origin, so a change landing mid-blend continues from what is on screen.

**The camera does not refit on a speaker change.** Framing is settled when an actor takes its slot
and then held. Refitting per line re-measured the head bone while the idle loop was moving it, which
jumped every cell sideways — including cells belonging to characters not involved in the change.

Because that leaves exactly one measurement standing for the whole conversation, it has to be taken
on a pose that actually exists, and two things conspire against that: pose transitions author a
0.25s fade, and clones are built under the **inactive** staging root, so on the frame a slot is
filled the animator may not have run. So `DialogueActorVisual.EvaluatePose` snaps the current pose to
full weight before sampling, and `DialogueStage` queues the slot in `pendingFraming` and re-fits it
once on the next `Tick`. Each slot enters that queue once, when it is filled.

Draw order (`SetAsLastSibling`) is left as an instant change because it has no visible effect: bands
are exactly one third wide and nothing scales above 1, so portraits never overlap.

An actor keeps its slot for the whole sequence. When
the speaker changes, the actor and camera remain fixed; `DialogueUI` blends the slot's `RawImage`
scale, vertical offset, tint, and sibling order. `DialogueLightRigSO` now contains only 3D light
values (per-sequence override, else the stage default), with listeners dropped to
`listenerIntensityScale` of full.

## Casting by party role

A cast entry, a line's speaker, and a stage change all name a cast member by the same kind of string
key, and there are two kinds (`DialogueCastKeys`):

| Key | Resolves to |
|---|---|
| `ID.Roma`, `npc.abbygail` … | that one specific character. Used for NPCs and anything fixed. |
| `role.Player`, `role.PartySlot1`, `role.PartySlot2`, `role.Helper` | whoever currently fills that party slot |

**Prefer role keys for the squad.** The player can change their team at any time in the Basement, so
a conversation cast by concrete character id breaks the moment they do — the slot is silently left
empty and the name plate falls back to the raw id. Role keys resolve against the live
`PartyRuntime`, so the same sequence works with any team. Write the squad's lines neutral until
character-specific dialogue is actually wanted.

Every party member is registered under **both** keys, so the two can be mixed freely inside one
sequence — cast the NPC by id and the squad by role, which is what
`Dialogue.Abbygail_Greeting.asset` does.

Two details that make this safe:

- **The pose profile is looked up by the real character, not the key.** `DialogueCastSource` carries
  `CharacterId` alongside the key, so `role.Player` poses whoever is actually playing.
- **An actor matches either name.** `DialogueActorVisual.Matches` accepts its cast key *and* its real
  character id, so a sequence that casts by role but names a speaker by id still lands on the right
  actor instead of quietly emphasising nobody.

When a key resolves to nobody the slot stays empty, the remaining portraits re-centre, and the
conversation plays on. Known rough edge: a line spoken by an unresolved key shows the raw key on the
name plate. `DialogueCastEntry.optional` exists but is **not yet wired** — every entry behaves as
optional today.

## Swapping actors mid-conversation

The stage holds **at most three actors, and that is enforced by there being three slots** — there is
no separate cap to check. Bringing someone on is always also taking someone off.

`DialogueLine.stageChanges` is a list applied **just before its line is shown**, so a character
brought on for a line is already standing there when it is spoken:

| Field | Meaning |
|---|---|
| `characterId` | who to place. Any id the conversation can resolve — a party member, or a scene actor the trigger supplies. **Empty clears the slot.** |
| `slot` | where. Whoever is standing there is destroyed and replaced. |
| `idlePoseId` | pose held while not speaking. |

### The hand-over animation

A swap is played as a visible hand-over, driven entirely in UI space by
`DialogueDirector.PlayStageChanges`:

1. every portrait whose slot actually changes slides down to `offStageOffset` and fades out
   (`exitSeconds`, default 0.16s)
2. the clones are swapped and `LayoutOccupiedPortraits` re-centres the line-up
3. the arriving portraits slide back up and fade in (`enterSeconds`, default 0.22s)

It is **sequential rather than a cross-fade** because a slot owns one camera and one RenderTexture —
the outgoing and incoming actors cannot both be rendered into it at the same time. A slot that was
cleared rather than refilled simply stays parked off stage. Slots the line does not change are left
untouched, so a line that restates the current line-up neither blinks nor costs anything.

The slide is composed on top of speaker emphasis rather than replacing it: `DialogueUI` keeps the
lerped emphasis position/scale/tint in its own arrays and writes
`emphasis + offStageOffset * (1 - onStage)` with `alpha * onStage`, so the two systems cannot fight
over the RectTransform.

`DialogueStage.ApplyStageChange` does the clone work. Two things it is careful about:

- **Re-placing whoever is already in the slot is a no-op** — no clone rebuild, no camera re-fit, so a
  line that restates the current line-up costs nothing.
- **The outgoing actor is only destroyed once the replacement clone exists**, so a swap that fails to
  resolve cannot leave the slot empty.

After the changes are applied the director calls `DialogueUI.LayoutOccupiedPortraits()` again, so
swapping down to two actors re-centres the pair rather than leaving a hole.

Validation walks the line-up **forward** through the sequence rather than judging every line against
the opening cast, so a speaker who was swapped in earlier is recognised as being on stage, and
`ValidateCastCoverage` requires a pose profile for everyone who ever appears — not just the opening
three.

## Camera framing

`DialogueStage.FitCameraToActor` sets up each cell's camera. The actor never moves off its authored
cell origin. It runs once per actor when the session opens, and again for every actor on every
speaker change.

The portrait cameras are **orthographic** (`DialogueStageSlot.SetRenderingEnabled` pins that every
session), so `orthographicSize` is the half-height and camera distance does not affect scale.

| Axis | Driven by |
|---|---|
| size | `orthographicSize = framingViewHeight / 2` — a fixed slice of the world, identical for everyone |
| height | **not driven** — the camera stays at its authored height |
| sideways | the **head bone**, so the face sits in the middle of its band |
| depth / clip | renderer bounds, only to stand back far enough and set the clip planes |

### Height is deliberately not equalised

Every cell camera sits at the same authored height, so **a taller character reads as taller** — which
is the point. Measured with the cast standing:

```
Left   ID.Aires      camLocalY=1.390  orthoSize=1.200  headVpY=0.487
Center ID.Roma       camLocalY=1.390  orthoSize=1.200  headVpY=0.466
Right  npc.abbygail  camLocalY=1.390  orthoSize=1.200  headVpY=0.602
```

Abbygail stands visibly taller than Roma, and the portraits show exactly that.

The authored camera height is therefore **the** framing decision for the whole cast — there is no
per-character correction behind it. `orthographicSize 1.2` shows ±1.2m, so `y = 1.39` covers 0.19 up
to 2.59: the tallest hat still clears the top, and the bottom crop lands just above the feet, which
sit behind the dialogue box regardless. It was tuned in Play Mode against the live cast; the earlier
1.65 spent roughly the top half of every cell on empty sky.

Retune that one value in the scene (all three `PortraitCamera` transforms) and in
`DialoguePresentationSceneBuilder`, not in `DialogueStage` — `FitCameraToActor` reads the authored
height back from `DialogueStageSlot.AuthoredCameraLocalPosition` and only overrides X and Z.

Because the camera no longer refits on a speaker change, a height edited live in Play Mode now
survives for the rest of the conversation, which makes tuning it by eye practical.

### Sideways still tracks the head

Horizontal centring stays automatic, because lateral pivot offset is a model quirk rather than a
character trait: measured `headX - rootX` is **+0.213 for Feno**, +0.059 for Roma, 0.000 for
Abbygail, so pivot-centred framing leaves some faces off to one side. Bounds cannot be used either —
they drag the camera toward whatever prop the character is holding. A rig with no head bone falls
back to the authored cell centreline.

The camera is only re-fitted on a speaker change, so a character sways a few centimetres inside its
frame during the idle loop (measured up to 0.05 of the band). That is intentional; a camera tracking
head sway every frame would read as jitter.

## Poses

`CharacterDialogueAnimationProfileSO` is a **separate** per-character asset keyed by
`CharacterStats.characterId`; it never touches the gameplay `CharacterAnimProfileSO`.

The fallback chain is **`poseId` → that profile's own `idlePose` → nothing.** It falls back *within
one profile*; there is deliberately **no project-wide default idle**, because these avatars are
generic and a clip borrowed from another character's rig does not retarget — it folds the model over,
which is worse than no pose at all.

So a profile whose `idlePose` is itself empty leaves the actor standing in its imported bind pose,
which reads on screen as a T-pose. Both checks report it: the authoring validator
("idlePose is missing. Any unmapped pose leaves the actor un-posed") and, at runtime,
`DialogueActorVisual.SetIdlePose` logs a warning once per actor. `DialogueProfileDatabaseSO` maps ids
to profiles for the stage.

## Advance, skip, and voice

- A **tap** advances. The first tap during a reveal completes the line instead of skipping it.
- **Holding** the same press past `holdToSkipSeconds` skips the rest of the sequence — which still
  counts as reaching the end, so `OnCompleted` fires.
- Input mirrors the effective bindings of the `Player/Interract` action into a standalone action and
  deactivates the gameplay `PlayerInput` for the duration, exactly like `StageIntroSkipInput`. The
  press that opened the conversation is ignored until released once.
- `DialogueLine.voice` is played unscaled and **never advances the line**.

## Repeat rules and persistence

`DialogueTrigger.RepeatMode` is `PlayOnce` (default) or `Replay`. Completion is persisted per save
slot in `slot_N_dialogue.json` (`DialogueProgressSaveFile`), keyed by the sequence's stable
`dialogueId` — not by asset name, so renaming an asset is safe and changing the id replays the
dialogue for everyone.

`SaveManager.IsDialogueCompleted(id)` / `MarkDialogueCompleted(id)`; a
`Reset Dialogue Progress` context menu item clears the current slot.

**`SaveManager` caches the active slot's progress file in memory.** `IsDialogueCompleted` is reached
from `Interactor`'s per-frame evaluation of candidate targets, so reading it straight from disk meant
opening and parsing JSON every frame for every play-once trigger in range. The cache holds one slot
and is stamped with it, so it self-invalidates the moment `currentSlot` moves; it is also dropped
explicitly on load, slot switch, `RefreshLoadedCacheFromDisk`, and slot reset. `MarkDialogueCompleted`
updates the cached object first and writes that object once, so every other trigger sees the
completion without anyone rereading the file.

## Abort

Death, active scene change, director disable/destroy, and any explicit `Abort(reason)` all end the
conversation through the **same** `Cleanup` path that the normal ending uses, so the world, input,
and HUD always come back. An abort **never** reports completion, so a trigger's `OnCompleted` scene
events cannot fire off a conversation the player did not finish.

The normal ending fades out over `fadeSeconds` (0.2–0.3 unscaled) and only then hands back the world.

## Authoring checklist

1. `Tools/Dialogue/Set Up Project Layers` — names rendering layer 5 `Dialogue`. It no longer creates
   a `DialogueActor` layer; clones share layer 0 with everything else.
2. `Tools/Dialogue/Build DialoguePresentation Scene` — builds and wires the whole scene and adds it
   to Build Settings. **Re-running discards scene tuning.**
3. Create a `CharacterDialogueAnimationProfile` per speaking character and add it to
   `Assets/Data/Dialogue/DialogueProfileDatabase.asset`.
4. Create a `DialogueSequence`, fill the cast (≤ 3, one per slot) and the lines.
5. Put a `DialogueTrigger` on the interact point, assign the sequence, pick the repeat mode, and wire
   `OnCompleted`.
6. `Tools/Dialogue/Validate Dialogue Authoring` before committing.

## Validation

`Tools/Dialogue/Validate Dialogue Authoring` reports: duplicate `dialogueId`s (which would silently
share play-once completion), lines spoken by someone outside the cast, slot collisions, cast members
with no pose profile, missing idle poses, stage/UI wiring gaps, play-once triggers whose sequence has
no id, a presentation scene that is not enabled in Build Settings, scene-actor problems (a missing
or duplicated `npc.` source, or a source the sequence never puts on stage), a required cast entry on
a party role that runtime may not be able to fill, and drift between
`DialogueLayers.AspFeatureRenderingLayerMask` and what the URP renderer actually filters on.

`CheckAssemblyBuild.ps1` covers the runtime scripts. The editor tooling is **not** covered by it —
run the two menu items in Unity to verify.
