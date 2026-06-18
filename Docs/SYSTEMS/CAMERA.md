# Camera System

## Overview

The camera layer has three components, each with a distinct responsibility.

| Component | File | Responsibility |
|-----------|------|----------------|
| `CameraF` | `Assets/Scripts/CameraF.cs` | Gameplay follow camera with aim-ahead |
| `CameraShake` | `Assets/Scripts/CameraShake.cs` | Trauma-based shake — API only, not yet wired |
| `CameraManager` | `Assets/Scripts/SelectCharactor/CameraManager.cs` | Cinemachine camera switcher for the Select Screen |

---

## CameraF — Follow Camera with Aim-Ahead

`CameraF` runs in `LateUpdate` and moves the camera to the player using
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

`CameraF` reads the current weapon type from `PlayerContext.Instance.WeaponSystem.gunType`
automatically — no serialized reference needed on the camera.

### Scene Setup

1. Place `CameraF` on the camera rig GameObject.
2. Assign `taget` → player root Transform.
3. Assign `aimTarget` → the same `aimTarget` Transform referenced by `PlayerContext`.
4. Tune `offset`, `smooth`, `lookAheadWeight`, `lookAheadSmooth` to taste.
5. Optionally override per-type distances in `lookAheadTable`.

---

## CameraShake — Trauma-Based Shake API

`CameraShake` accumulates **trauma** (0–1) and converts it to a position and
rotation shake applied to `localPosition` / `localEulerAngles`.
Shake intensity = `trauma²`, so light trauma produces subtle movement and
high trauma produces strong movement.

### Public API

```csharp
CameraShake shake = ...; // GetComponent or cached reference
shake.AddTrauma(0.4f);   // add trauma; clamps to [0, 1] internally
```

Trauma decays automatically at `traumaDecayPerSecond` — no manual reset needed.

### Inspector Fields

| Field | Default | Description |
|-------|---------|-------------|
| `maxPositionOffset` | 0.3 | Peak local position offset at full trauma |
| `maxRotationDeg` | 2 | Peak Z-rotation in degrees at full trauma |
| `traumaDecayPerSecond` | 1.5 | How fast trauma drains to zero |
| `noiseSpeed` | 8 | Frequency of Perlin noise motion |

### Scene Setup

Place `CameraShake` on a **child ShakeNode** that is a child of the camera
rig, not on the rig itself. `CameraF` controls the rig's world position;
`CameraShake` applies local offsets on the child so the two do not fight.

```
CameraRig          ← CameraF here
  └─ ShakeNode     ← CameraShake here
       └─ Camera
```

> `CameraShake` is not yet connected to any gameplay event.
> Wire it up by calling `AddTrauma` from damage handlers, explosion effects,
> or any other combat event that should produce screen shake.

---

## CameraManager — Select Screen

`CameraManager` switches between two Cinemachine cameras during the
character and map selection flow by adjusting `Priority`.

| Camera | Active When |
|--------|-------------|
| `CharacterSelectCamera` | character selection (default) |
| `StadeSelectCamera` | map selection after `MobilizClick()` |

This component is unrelated to gameplay camera behavior.
