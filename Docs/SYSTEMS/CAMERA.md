# Camera System

## Overview

The camera layer has three components, each with a distinct responsibility.

| Component | File | Responsibility |
|-----------|------|----------------|
| `GameplayCameraController` | `Assets/Scripts/GameplayCameraController.cs` | Gameplay follow camera with aim-ahead |
| `CameraShake` | `Assets/Scripts/CameraShake.cs` | Trauma-based shake — API only, not yet wired |
| `CameraManager` | `Assets/Scripts/SelectCharactor/CameraManager.cs` | Cinemachine camera switcher for the Select Screen |

---

## GameplayCameraController — Follow Camera with Aim-Ahead

`GameplayCameraController` runs in `LateUpdate` and moves the camera to the player using
`SmoothDamp`. On top of the base follow it applies an **aim-ahead offset**
that shifts the frame toward the world position the player is aiming at,
so the player can see more battlefield in the shooting direction.

### Inspector Fields

| Field | Description |
|-------|-------------|
| `taget` | Transform to follow (player root) |
| `smooth` | SmoothDamp time for base follow |
| `offset` | Fixed positional offset from the target |
| `aimTarget` | World-space aim cursor transform (assign `PlayerContext.aimTarget`) |
| `lookAheadWeight` | 0–1 fraction of cursor distance applied as offset |
| `lookAheadDistance` | Per-weapon-type distance cap (see table below) |
| `lookAheadSmooth` | SmoothDamp time for the aim offset (faster than base follow) |
| `defaultLookAheadDistance` | Fallback cap when no weapon is equipped |

### Aim-Ahead Formula

```
delta       = aimTarget.position - player.position  (Y zeroed)
lookAmount  = Min(delta.magnitude × lookAheadWeight, maxDistanceForWeapon)
aimOffset   = normalize(delta) × lookAmount
finalTarget = player.position + offset + aimOffset
```

### Look-Ahead Distance per Weapon Type

Configured via `lookAheadTable` in the Inspector (defaults below):

| Weapon Type | Default Max Distance |
|-------------|---------------------|
| Sniper | 7 units |
| HMG | 5 units |
| Rifle | 4 units |
| SMG | 3 units |
| Shotgun | 2.5 units |
| Pistol | 2 units |
| Melee | 0.5 units |

`GameplayCameraController` reads the current weapon type from `PlayerContext.Instance.WeaponSystem.gunType`
automatically — no serialized reference needed on the camera.

### Scene Setup

1. Place `GameplayCameraController` on the camera rig GameObject.
2. Assign `taget` → player root Transform.
3. Assign `aimTarget` → the same `aimTarget` Transform referenced by `PlayerContext`.
4. Tune `offset`, `smooth`, `lookAheadWeight`, `lookAheadSmooth` to taste.
5. Optionally override per-type distances in `lookAheadTable`.

---

## Skill Timeline Camera Shake

`GameplayCameraController` has a built-in trauma shake system that fires when a `ShakeCamera`
marker in a main skill clip is reached. Shake is applied to the Camera child's
`localPosition` / `localEulerAngles`, leaving the rig's world position
(follow + aim-ahead) untouched.

### Supported Sources

Shake fires for skills cast by:

- the player (`PlayerContext.AnimBrain`)
- field allies registered in `FieldAllyManager`
- the summoned helper managed by `AllyHelperManager`

Enemy skills do not trigger camera shake. `GameplayCameraController` subscribes to
`SkillTimelineEventRaised` from each source's `CharacterAnimBrain` and
refreshes subscriptions when allies register/unregister or the helper changes.

### Shake Behavior

- Intensity = `trauma²` (quadratic falloff via Perlin noise)
- Multiple `ShakeCamera` markers in one clip stack trauma additively
- Trauma decays using `Time.unscaledDeltaTime`, so shake speed and decay are
  stable during world slow
- Markers in a cutscene character clip are not bound; only the main skill clip
  fires `ShakeCamera`

### Inspector Fields (on `GameplayCameraController`)

| Field | Default | Description |
|-------|---------|-------------|
| `shakeTraumaPerMarker` | 0.6 | Trauma added per `ShakeCamera` marker |
| `shakeMaxPositionOffset` | 0.3 | Peak local position offset at full trauma |
| `shakeMaxRotationDeg` | 2 | Peak Z-rotation in degrees at full trauma |
| `shakeTraumaDecayPerSecond` | 1.5 | How fast trauma drains to zero |
| `shakeNoiseSpeed` | 8 | Frequency of Perlin noise motion |

### Prefab Setup

```
CameraHolder       ← GameplayCameraController here (auto-resolves first child as shake target)
  └─ Camera        ← shake applied to localPosition / localEulerAngles
```

`GameplayCameraController` stores the Camera child's base local pose at `Awake` and restores it
when shake ends or the component is disabled.

---

## CameraShake — Legacy Shake Component

`CameraShake` is a standalone trauma shake component with its own `AddTrauma`
public API. It is **not used** by the skill timeline camera shake system above.
The component and its API are preserved for potential future use by other
systems (e.g., explosion effects, damage feedback).

### Public API

```csharp
CameraShake shake = ...; // GetComponent or cached reference
shake.AddTrauma(0.4f);   // add trauma; clamps to [0, 1] internally
```

### Inspector Fields

| Field | Default | Description |
|-------|---------|-------------|
| `maxPositionOffset` | 0.3 | Peak local position offset at full trauma |
| `maxRotationDeg` | 2 | Peak Z-rotation in degrees at full trauma |
| `traumaDecayPerSecond` | 1.5 | How fast trauma drains to zero |
| `noiseSpeed` | 8 | Frequency of Perlin noise motion |

---

## CameraManager — Select Screen

`CameraManager` switches between two Cinemachine cameras during the
character and map selection flow by adjusting `Priority`.

| Camera | Active When |
|--------|-------------|
| `CharacterSelectCamera` | character selection (default) |
| `StadeSelectCamera` | map selection after `MobilizClick()` |

This component is unrelated to gameplay camera behavior.
