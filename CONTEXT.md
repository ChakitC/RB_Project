# Project Context

This file records canonical project terms. It is a glossary, not an implementation specification.

## Map Progression Terms

- **Test Stage** — a selectable map-progression band used to test continuous character growth across a defined level range over multiple runs.
  _Avoid_: Test Map, Level Bracket Map
- **Stage Placard** — a selectable map placard on the Basement Mobiliz board associated with one Test Stage. It is a direct selection and does not imply a level gate or world-space portal.
  _Avoid_: Stage Card, Level Gate, Stage Lock, World Portal
- **Mobiliz Board Page** — one page of Stage Placards on the Basement Mobiliz board. Adjacent pages are reached with bounded left and right navigation rather than wrapping around.
  _Avoid_: Map Submenu, Carousel Loop
- **Stage Run** — one attempt in a Test Stage that counts only after the Boss is cleared and the party returns to the Basement. Death, abandonment, or leaving before that return does not complete the run.
  _Avoid_: Map Attempt, Room Run
- **Stage XP Budget** — the approximate experience available during one successful Stage Run, derived from a Level Table range and its target number of successful runs. It targets a progression pace rather than guaranteeing an exact ending level.
  _Avoid_: Forced Level, Guaranteed XP
- **Stage Completion Bonus** — the completion-only portion of the Stage XP Budget granted to every deployed party member after the Stage Exit successfully returns the party to the Basement.
  _Avoid_: Boss Kill XP, Attempt Reward
- **Party XP Award** — experience granted in full to every deployed party member participating in the Stage Run, independent of which character lands the final hit. Characters left in the Basement roster do not receive it.
  _Avoid_: Last-hit XP, Shared XP Split
- **Stage Exit** — the player-activated return point revealed after the Boss is cleared, allowing the party to finish the Stage Run and return to the Basement after collecting rewards.
  _Avoid_: Automatic Return, Boss Teleport
- **Enemy Level** — the combat-scaling level assigned by the current Test Stage to enemies spawned during its Stage Run. Enemy prefabs provide base stats and per-level growth but do not choose this level.
  _Avoid_: Prefab Level, Player-owned Enemy Level
- **Stage Progress Count** — the Save/Profile-wide progression count retained across play sessions for one Test Stage. It advances after completed Stage Runs until the stage's final Enemy Level tier, then remains capped while the stage stays repeatable for farming.
  _Avoid_: Attempt Count, Lifetime Clear Count, Global Run Count
- **Run Seed** — the resolved random seed identifying the generated layout of one Stage Run. A recorded Run Seed allows that layout to be reproduced for testing.
  _Avoid_: Stage ID, Save Seed

## Active Skill Terms

- **Skill Slot** — one position in a character's active-skill loadout. It has a stable `slotId` and can contain multiple Skill Variants.
- **Skill Variant** — one selectable configuration inside a Skill Slot. It has a stable `optionId`, an active skill, support skills, and may override the skill's default Active Skill Tree.
- **Active Skill Tree** — the upgrade graph resolved for a Skill Variant from its override or the Skill Asset default. Its stable `treeId` identifies saved progress for that graph.
- **Upgrade Node** — one unlockable entry in an Active Skill Tree. Its stable `nodeId` identifies prerequisites, paid cost, and effects.
- **Active Skill Point** — the character-wide currency spent across all Active Skill Trees. It is separate from Passive Points.

## AI Movement Terms

- **Target Orbit** — controlled movement around a target within a radial band. Facing the target is an independent behavior choice.
  _Avoid_: Random Move Around Target, Orbit/Strafe Around Target

## Combat Damage Terms

- **Hit Zone** — a dedicated collider-backed region on a character that identifies which body part received a direct weapon-projectile hit. It does not own separate HP.
  _Avoid_: Limb Health, Body-part HP
- **Head** — the head Hit Zone. Its damage multiplier is resolved from the target's Hit Zone Damage Profile.
  _Avoid_: Head Collider Damage
- **Torso** — the body Hit Zone. It is the normal-damage region and defaults to a `1.0` multiplier.
  _Avoid_: Body Collider, Chest HP
- **Hit Zone Damage Profile** — target-side damage multiplier data used by `HealthSystem` after the projectile's normal damage calculation.
  _Avoid_: Projectile Headshot Config

## Third-Person Shooter Terms

- **Free Look** — the normal exploration state where the camera can orbit independently and movement is camera-relative. The character is not forced to face the camera direction until a combat action requires it.
  _Avoid_: Normal Camera, Exploration Aim
- **Shoulder Aim** — the held aiming state where the camera frames the character from the right shoulder and the character faces the camera's horizontal aim direction.
  _Avoid_: ADS, Zoom Mode
- **Aim Point** — the validated world-space point under the center-screen reticle that ranged attacks and aim-dependent skills intend to hit.
  _Avoid_: Mouse Point, Cursor Target
- **Upper-body Aim** — the visual pitch of a character's torso and muzzle origin toward the Aim Point during combat alignment. Character yaw and the actual Muzzle Trajectory remain separate concerns.
  _Avoid_: Full-body Aim, Weapon IK
- **Muzzle Trajectory** — the unobstructed path from the weapon muzzle toward the Aim Point. It is distinct from the camera sightline so nearby cover can block a shot.
  _Avoid_: Camera Ray
- **Hip Fire** — firing outside Shoulder Aim with reduced precision while temporarily aligning the character with the camera direction.
  _Avoid_: Blind Fire
- **Soft Target** — a visible enemy selected briefly near the center-screen direction to orient a melee action without locking the camera or persisting a target lock.
  _Avoid_: Lock-On, Hard Target
