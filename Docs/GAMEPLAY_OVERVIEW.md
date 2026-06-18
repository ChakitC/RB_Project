# Gameplay Overview

## Setting and Premise

RB Project is an Action RPG Looter Shooter presented in a semi-isometric perspective.
The player takes the role of an undercover police officer posing as a consultant inside
a criminal gang in order to investigate and contain an escalating conflict.

The central threat is the **SerpentTown** gang, which distributes a mysterious drug that
mutates those who take it. Their true goal is to use these mutations to open a
dimensional rift and pull demonic energy from another world, giving them total control
over **Rabbit Town**.

---

## Core Premise

### View and Controls

- Semi-isometric camera with the player controlling character facing direction via the
  mouse.
- Movement: **W A S D** for exploring areas and engaging in combat.

### Challenge

Players face enemies, traps, and environmental hazards throughout missions. Threat
composition and positioning vary each run due to procedural level generation.

### Equipment

The game features three main gear categories that players discover during missions:

- **Weapons** — the primary damage tool; each instance can roll unique affixes.
- **Accessories** — equippable items that add stat modifiers and passive abilities to a
  character.
- **Skill Gems / Support Gems** — active skills and the modifiers that augment them.

Collecting, equipping, upgrading, and dismantling these items is the primary loop for
improving combat performance.

---

## Progression

### Characters

- Players begin by choosing one of **4 starting characters** from the Yellow Gang.
- Additional characters are unlocked as the story and progression advance.
- Characters grow through combat: gaining new abilities, improving stats, and becoming
  capable of equipping higher-tier gear.

### Base of Operations (Basement)

Players alternate between running missions and returning to the Basement hub. The
Basement is where players:

- Manage and spend resources gathered during missions.
- Upgrade the base facilities.
- Upgrade characters and equipment.
- Access training and other support facilities.

---

## Unique Feature — Procedural Mission Levels

The core replayability driver is a procedurally generated mission level structure that
is re-randomized each run. Key points:

- Players must explore thoroughly; shortcuts, secret areas, and hidden rooms are
  generated fresh each session.
- Secret areas can contain bosses, recruitable characters, and special equipment
  necessary to unlock additional content.
- No two runs through the same mission stage are identical in layout.

---

## Party Composition

During missions the player controls the **main character** with:

- **2 AI ally companions** that follow and assist throughout the mission.
- **1 Helper character** — a dynamic role that enters and exits the battlefield at
  certain points rather than staying for the full mission.

---

## Combat Loop

A typical combat encounter flows as follows:

1. **Move and aim** — player positions with WASD and aims the firing direction with the
   mouse.
2. **Shoot** — hold the fire button to shoot. Weapons have a magazine; when it empties
   the player must reload manually or be reloaded automatically.
3. **Use skills** — active skills (bound to hotkeys) are cast on cooldown. Each cast
   plays an animation, fires the payload (projectile, hitbox, status application, or
   pickup spawn), and enters cooldown.
4. **Passive reactions** — combat events (shot fired, hit landed, kill, damage taken,
   reload) automatically trigger equipped passive effects without player input.
5. **Status effects** — hits can apply buffs or debuffs. Effects stack according to
   their stack mode (refresh, add stack, independent instances, or strongest-only) and
   can block movement, shooting, or skill use on affected characters.
6. **Stagger and Stun** — each character has a hidden Stagger Meter. Certain attacks
   deal stagger damage alongside normal damage. When the meter fills the character is
   **Stunned**: movement, shooting, and skill use are all locked out for the stun
   duration, AI stops acting, and all incoming damage is amplified. After recovery the
   character gains a brief **immunity window** before the meter can fill again. The
   meter decays back to zero on its own if the character stops taking stagger hits
   (see [Stagger and Stun](#stagger-and-stun) below).
7. **Chain Attack** — when conditions are met the player can launch a coordinated
   Chain Attack sequence (see below).

---

## Skill Gem and Support Gem System

Active skills are defined as **Skill Gems**. Each gem contains the skill's animation,
timing, payload type, base stats, and VFX.

Payload types available to skill gems:

| Payload | Effect |
|---------|--------|
| Projectile | fires one or more projectiles |
| Prefab Hitbox | activates an inline hitbox sequence |
| Apply Status | directly applies a status effect |
| Spawn Pickup | drops a pickup in the world |

**Support Gems** modify a Skill Gem they are socketed into. They can change damage,
area, cooldown, projectile behavior, or add secondary effects.

Characters have a fixed number of **skill loadout slots**. Each slot can hold one
active Skill Gem plus its socketed Support Gems. Slots are hotkey-bound and some slots
allow the player to swap between multiple configured skill options at runtime (e.g.
swapping between two different skills assigned to the same button).

Special **Cutscene Skills** add a cinematic two-phase presentation: the world slows to
near-freeze, a dedicated character and camera animation play, then the scene returns
and the payload fires normally.

---

## Chain Attack

Chain Attack is the party's coordinated finishing move. When triggered against a locked
target the system runs a scripted sequence where each party member plays their assigned
role in order.

**Participant roles in a sequence:**

| Role | Description |
|------|-------------|
| Player | executes their step in the sequence |
| Party Slot 1 | first AI ally step |
| Party Slot 2 | second AI ally step |
| Helper | optional step; Helper character enters specifically for this |

During an active Chain Attack sequence the player is automatically protected:
invincible, untargetable, and collision with enemies is suppressed. This window is
designed to let the cinematic sequence play without interruption.

Each step in the sequence can teleport the actor to the target, warp them in with
a visual effect, or keep them in place. After the step the actor can return to their
recorded origin, fade out and deactivate, or stay at the attack position.

---

## Stagger and Stun

Every character has a **Stagger Meter** — a hidden gauge that fills when the character
takes hits that carry stagger damage. When the meter reaches its maximum the character
is immediately **Stunned**.

### While Stunned

- Movement, shooting, and skill use are all blocked.
- AI behavior tree and NavMesh movement are suspended — enemies cannot act.
- All incoming damage is multiplied (default ×1.25).
- The stagger animation (`Stun`) plays on the character rig.
- The meter resets to zero on stagger entry (configurable).

### Recovery and Immunity

After the stun duration expires the character recovers and gains a **post-stagger
immunity window**. During this window the meter cannot gain any stagger, preventing
immediate re-stun after recovery.

### Meter Decay

If the character stops receiving stagger hits, the meter begins to drain after a short
delay. This means lighter pressure only partially fills the meter, which decays away
between bursts — sustained or heavy attacks are required to land the full stun.

### Impact Reaction Levels

Stagger is one of three impact reactions that an attack can request:

| Reaction | Effect |
|----------|--------|
| Root | character cannot move but can still shoot and use skills |
| Mini Stun | brief flinch animation; short interruption |
| Stun | full stagger stun — all actions locked, AI suspended, damage amplified |

The full Stun reaction is triggered by the Stagger Meter filling up. Root and Mini Stun
can be applied directly by individual attacks without needing the meter.

### Stagger Profile

Each character's stagger behavior is configured through a **StaggerProfileSO** asset:

| Parameter | Default | Description |
|-----------|---------|-------------|
| Max Stagger | 100 | meter capacity |
| Stagger Gain Multiplier | ×1 | scales all incoming stagger |
| Stagger Duration | 1.5 s | how long the stun lasts |
| Damage Multiplier (stunned) | ×1.25 | bonus damage received while stunned |
| Post-Stagger Immunity | 1 s | immunity window after recovery |
| Decay Delay | 1.5 s | idle time before meter starts draining |
| Decay Per Second | 20 | drain rate during decay |

---

## Passive System

Passives are always-active or conditionally-triggered modifiers equipped by characters.

| Kind | Behavior |
|------|----------|
| Always On | contributes stat modifiers while equipped (e.g. +damage, +speed) |
| Triggered | listens to a combat event and executes an action when conditions are met |
| Custom | runs a custom behavior object on equip and in response to passive events |

Combat events that can trigger passives: shot fired, hit landed, kill, damage taken,
reload, and movement distance.

Passives are gathered from character stat asset loadouts, runtime additions, prefab
components, and extra runtime slots. This makes it possible to grant passives through
items, story events, or buff pickups without changing the base character definition.

---

## Status Effects

Status effects are timed modifiers applied to characters by skills, weapons, passives,
or the environment.

**Categories:** Buff, Debuff, or Neutral.

**Stack modes** control how multiple applications of the same effect interact:

| Mode | Behavior |
|------|----------|
| Refresh Duration | resets the timer, no extra stack count |
| Add Stack and Refresh | increments a stack counter and resets the timer |
| Independent Instances | each application runs its own separate timer |
| Strongest Only | only the highest-magnitude instance is active at any time |

**Control blocks** are flags that a debuff can impose on a target, locking out any
combination of movement, shooting, and skill use for the effect's duration.

---

## Loot and Economy

### Weapons and Affixes

Weapons dropped during missions are **weapon instances** — each instance can roll one
or more **affixes** that modify combat behavior (e.g. bonus projectiles, damage
multipliers, special on-hit effects). Two copies of the same weapon base can have
entirely different affixes.

Weapons can be **upgraded** at the Basement using resources. Upgrade curves define
how stats scale with upgrade level.

### Currency

| Currency | Use |
|----------|-----|
| Gold | purchased at shops, traded for weapons and items |
| Scrap | upgrade material, gathered from dismantling unwanted loot |

### Accessories

Accessories are equippable items that directly contribute **stat modifiers** and
**passive abilities** to the character wearing them. Effects from all equipped
accessories are always active — no activation required.

**Modifier roll** — when an accessory instance is created (from a drop or shop), it
may roll one **Modifier** from the accessory's modifier pool. The modifier adds an
extra set of stats and passives on top of the base definition. Each modifier has a
weight that controls how often it is rolled; the pool also includes a configurable
no-modifier weight for plain base-only drops.

**Accessory instance data:**

| Field | Meaning |
|-------|---------|
| `accessoryId` | which base definition this instance uses |
| `modifierId` | which modifier was rolled (empty = no modifier) |
| `upgradeLevel` | how many times the accessory has been upgraded |

**Slots** — each character has an **Accessory Loadout** with a fixed number of slots
(default 5). Slots are shared across the party's inventory save, so the same physical
instance cannot be equipped on two characters at the same time.

**Dismantle** — accessories that are not currently equipped on any character can be
dismantled for **Scrap**. The scrap reward scales with the instance:

```
Scrap = base (10) + modifier bonus (5, if a modifier was rolled) + upgradeLevel × 3
```

### Drop and Shop Flow

Enemies drop loot according to their **drop table**, which can include weapons,
accessories, and items at configured rarity weights. Shops in the Basement and
mission areas offer a rotating catalog of purchasable gear.

---

## Brief Story Premise

An undercover officer embedded in the Yellow Gang discovers that **SerpentTown** is
far more dangerous than a conventional criminal organization. Their experimental drug
does not merely alter users physically — it is the catalyst for opening a dimensional
gate. With demonic energy threatening to pour into Rabbit Town, the player must
investigate, disrupt SerpentTown operations from within, and ultimately close the rift
before the town falls.
