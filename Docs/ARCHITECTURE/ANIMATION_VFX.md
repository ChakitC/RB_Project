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

`SetAnimationVfxData` owns generic source/entry authoring, hierarchy placement,
and preview state. `SetSkillVfxData` inherits it while retaining its script GUID,
serialized Skill fields, slots, entries, public buttons, and existing scene or
prefab workflow.

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
