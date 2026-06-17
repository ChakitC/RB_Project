# Animation VFX

## Runtime Ownership

`AnimationVfxPresenter` is the shared runtime for animation-driven VFX. It reads
`IAnimationVfxCueSource` and does not depend on Skill, Melee, Dash, Reload, or an
editor authoring component.

Each animation owner starts a session and retains its
`AnimationVfxSessionToken`. `HandleCue` affects only that session, and
`EndSession` immediately cleans up any loops left by that animation. Multiple
sessions may use the same loop key without affecting each other.

`AnimationVfxAnchorContext` supplies the character root, cast origin, aim
transform, and Animator. The shared presenter must not discover Skill-specific
references itself.

`GenericBone` resolves a stored transform path below the context Animator root,
so the same cue contract works with Generic rigs as well as Humanoid rigs.
`WorldSpace` samples the bone pose once. `FollowAnchor` uses
`AnimationVfxFollowAnchor` to update world position and rotation in `LateUpdate`
without parenting the VFX to the bone, so animated or non-uniform bone scale is
not inherited. Other anchor modes retain their existing parenting behavior.

## Timeline Binding

`AnimationVfxEventBinder` binds the shared Animancer event name `Vfx` on a
runtime event sequence. Occurrences are numbered from zero in sequence order.
Several cue records may share one cue index, and a marker may intentionally have
no cue records.

Animation owners are responsible for ending their own session on completion,
interrupt, cancellation, failed playback, state exit, and disable.

`MeleeComboSO.Step` owns an optional embedded `AnimationVfxTrack`.
`CharacterAnimBrain` starts one VFX session for the active step, binds `Vfx`
markers on the same runtime sequence as hit and chain callbacks, and ends the
session before replay, advance, combo exit, interrupt, or disable.

`CharacterAnimProfileSO` owns separate embedded tracks for `Dash Forward`,
`Dash Backward`, and `Reload` while retaining the existing `ClipTransition`
fields. Dash and Reload states own their session tokens and clean them up on
replay, completion, state exit, interruption, profile change, or disable.
Upper-body Reload and Dash can therefore keep independent sessions active at
the same time, including when both tracks use the same loop key.

## Editor Source Adapters

`IAnimationVfxTimelineSource` is the shared editor contract for source/entry
identity, `ClipTransition`, VFX cues, validation, save ownership, and lane
descriptors. V2 provides `SkillVfxTimelineSource` and
`MeleeComboVfxTimelineSource`. V3 adds
`CharacterAnimProfileVfxTimelineSource` with stable entries `dash.forward`,
`dash.backward`, and `reload`.

Open `Tools > RB > Animation VFX > Animation Event VFX Timeline`. The old Skill
timeline menu opens the same window. Skill contributes Cast Point plus
conditional Pre-Cast and Hitbox lanes. Melee contributes repeatable
HitStart/HitEnd events and an editable normalized Chain Window. Character
profile entries contribute no gameplay lanes; they use Animation, VFX, and
Other Events only.

`SetAnimationVfxData` is the only scene authoring component. It owns generic
source/entry selection, hierarchy placement, preview state, validation, and
save/load actions. Skill, Melee, and Character Profile data all flow through
their `IAnimationVfxTimelineSource` adapters.

The timeline window uses deterministic editor sampling rather than firing a VFX
and allowing it to advance on editor time. It samples the animation pose first,
reconstructs OneShot and loop state from every `Vfx` marker up to the playhead,
then simulates each ParticleSystem to its elapsed cue time. Forward playback
advances the cached systems incrementally at 75 FPS; scrubbing, rewinding, and
playback-loop wrap restart simulation at the requested time. Pause leaves the
sampled animation and VFX state frozen, while Stop, source changes, window close,
and Play Mode transitions clear the timeline preview.

Timeline sampling and manual preview are separate modes. Entry preview buttons
and `Play All VFX` retain realtime editor-clock playback. Starting either mode
stops the other so they cannot control the same temporary playback instances.
Runtime event binding and `AnimationVfxPresenter` are unaffected.

The selected source asset and entry are the authoring source of truth. Changing
either selection immediately stops preview, removes existing VFX slots and
entries, and rebuilds them from the selected asset. `Create / Sync VFX Slots`
and `Load VFX Data` use the same replace-and-rebuild operation. Unsaved hierarchy
changes are discarded by rebuild, but the complete operation is grouped for
Unity Undo. Non-VFX children below the configured source root are not removed.

Generic bone paths are stored in the existing custom-path field for serialized
compatibility, but are interpreted relative to the Animator rather than the
character context root. The authoring entry's `Use Selected Bone` button accepts
only a Transform below the preview Animator and writes the Animator-relative
path. Selecting `GenericBone` defaults the entry to `FollowAnchor`; authors may
still switch it to `WorldSpace`.

Melee step GUIDs are editor identity only. Run
`Tools > RB > Animation VFX > Assign Missing Melee Step IDs` when IDs are empty
or duplicated. The command changes IDs only.

## Skill Compatibility

Skill assets continue to serialize `SkillVfxEvent` in `SkillGemDefinition`.
`SkillVfxCueSource` exposes that existing list through the shared runtime
contract without duplicating or migrating asset data. `SkillVfxPresenter`
remains as the request-id compatibility facade used by `CharacterAnimBrain`.

Skill continues to serialize only `SkillVfxEvent`; its adapter does not create a
second VFX list.
