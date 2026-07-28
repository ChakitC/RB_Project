# Project Context

This file records canonical project terms. It is a glossary, not an implementation specification.

## Active Skill Terms

- **Skill Slot** — one position in a character's active-skill loadout. It has a stable `slotId` and can contain multiple Skill Variants.
- **Skill Variant** — one selectable configuration inside a Skill Slot. It has a stable `optionId`, an active skill, support skills, and may override the skill's default Active Skill Tree.
- **Active Skill Tree** — the upgrade graph resolved for a Skill Variant from its override or the Skill Asset default. Its stable `treeId` identifies saved progress for that graph.
- **Upgrade Node** — one unlockable entry in an Active Skill Tree. Its stable `nodeId` identifies prerequisites, paid cost, and effects.
- **Active Skill Point** — the character-wide currency spent across all Active Skill Trees. It is separate from Passive Points.

## AI Movement Terms

- **Target Orbit** — controlled movement around a target within a radial band. Facing the target is an independent behavior choice.
  _Avoid_: Random Move Around Target, Orbit/Strafe Around Target

## Third-Person Shooter Terms

- **Free Look** — the normal exploration state where the camera can orbit independently and movement is camera-relative. The character is not forced to face the camera direction until a combat action requires it.
  _Avoid_: Normal Camera, Exploration Aim
- **Shoulder Aim** — the held aiming state where the camera frames the character from the right shoulder and the character faces the camera's horizontal aim direction.
  _Avoid_: ADS, Zoom Mode
- **Aim Point** — the validated world-space point under the center-screen reticle that ranged attacks and aim-dependent skills intend to hit.
  _Avoid_: Mouse Point, Cursor Target
- **Muzzle Trajectory** — the unobstructed path from the weapon muzzle toward the Aim Point. It is distinct from the camera sightline so nearby cover can block a shot.
  _Avoid_: Camera Ray
- **Hip Fire** — firing outside Shoulder Aim with reduced precision while temporarily aligning the character with the camera direction.
  _Avoid_: Blind Fire
- **Soft Target** — a visible enemy selected briefly near the center-screen direction to orient a melee action without locking the camera or persisting a target lock.
  _Avoid_: Lock-On, Hard Target
