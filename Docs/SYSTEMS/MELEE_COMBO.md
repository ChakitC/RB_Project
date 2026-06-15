# Melee Combo

## Step Data

Each `MeleeComboSO.Step` owns its `ClipTransition`, timing, chain window, impact
settings, stable editor entry GUID, and optional embedded `AnimationVfxTrack`.
The GUID distinguishes steps that share the same clip and is not required by
runtime playback.

Run `Tools > RB > Animation VFX > Assign Missing Melee Step IDs` after creating,
duplicating, or merging steps. It changes only empty or duplicate IDs.

## Timeline And VFX

Melee clips support repeatable `HitStart` and `HitEnd` pairs plus shared `Vfx`
markers. The shared timeline exposes Hitbox events and an editable Chain Window;
range values are clamped and ordered in `0..1` before the owning struct is
assigned back into the list.

`Vfx` occurrences are numbered chronologically from zero. Multiple actions may
target one occurrence, and an empty marker is valid.

## Runtime Lifecycle

`CharacterAnimBrain` creates one `AnimationVfxPresenter` session for the active
step and binds it to the same runtime Animancer sequence used by melee hit and
chain callbacks. The session ends before replay or advance and on completion,
cancel, interrupt, state exit, disable, or destroy. Session tokens isolate Melee
loops from Skill and other animation owners even when loop keys match.
