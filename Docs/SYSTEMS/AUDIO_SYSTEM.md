# Audio System

`AudioService` owns runtime playback for `AudioCue` assets. The service keeps a
shared pool of `AudioSource` components under the persistent gameplay-system
root and applies master, category, cue, and provider volume multipliers.

## Pool Exhaustion

When the configured source pool is full, a new request may replace an existing
one-shot playback. Replacement follows Unity's audio-priority convention, where
a smaller `AudioCue.priority` value is more important:

- Music is never replaced by pool pressure.
- Looping playback is never replaced, regardless of category.
- A request can replace only a non-looping playback of equal or lower
  importance.
- The least-important eligible playback is replaced first. If priorities are
  equal, the oldest eligible playback is replaced.
- If no playback is eligible, the new request is rejected.

Per-cue `cooldown` and `maxInstances` remain the authoring controls for limiting
high-frequency sounds. Pool replacement is a last-resort capacity policy and
must not be used as the primary concurrency limit for a noisy cue.

## Lifetime

`AudioService` persists its root GameObject with `DontDestroyOnLoad`. In the
authored `GamePlaySystem` prefab, `AudioSystem` remains a child of that root; do
not move the persistent service under a scene-owned object that should be
destroyed during a scene transition.

Scene music and ambience selection is owned by `SceneLoaderSystem`. A matching
scene entry can stop the previous category and start the configured cue after
the scene loads.
