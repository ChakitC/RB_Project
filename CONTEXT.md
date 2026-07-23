# Project Context

This file records canonical project terms. It is a glossary, not an implementation specification.

## Active Skill Terms

- **Skill Slot** — one position in a character's active-skill loadout. It has a stable `slotId` and can contain multiple Skill Variants.
- **Skill Variant** — one selectable configuration inside a Skill Slot. It has a stable `optionId`, an active skill, support skills, and may override the skill's default Active Skill Tree.
- **Active Skill Tree** — the upgrade graph resolved for a Skill Variant from its override or the Skill Asset default. Its stable `treeId` identifies saved progress for that graph.
- **Upgrade Node** — one unlockable entry in an Active Skill Tree. Its stable `nodeId` identifies prerequisites, paid cost, and effects.
- **Active Skill Point** — the character-wide currency spent across all Active Skill Trees. It is separate from Passive Points.
