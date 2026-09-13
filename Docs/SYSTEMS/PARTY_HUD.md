# Separate Party HUD

`Assets/Prefab/User Interface/PlayerUI_PartyHud.prefab` is an independent copy of
PlayerUI. `DefaultPartySpawnConfig.asset` now loads this new prefab in the game.
The original PlayerHUD child remains inactive in the copy to retain serialized
references; the new `UI_Manager/PartyHUD` supplies the visible combat HUD.
Inventory/equipment and the existing PlayerUI runtime binder remain in the copy.

## Layout and data

The reference layout is authored at 1920 x 1080 with a scaling Canvas:

- Bottom left: three allies, portraits and HP only, including the Helper.
- Bottom center: controlled Player HP and three Command Point segments.
- Bottom right: Active above Ultimate, four aligned columns. Column 1 is Player,
  2/3 are PartySlot1/PartySlot2, and 4 is Helper. Helper has no battle Active or
  Ultimate and shows unavailable cells; no Helper skill mapping is fabricated.
- Right center: Combo offers in a horizontal row, above magazine/reserve ammo.
  Combo is hidden with no live Offered entries. Earliest expiry goes first,
  ties use OfferId. Each portrait has its own remaining-seconds label.

`PartyHudPresenter` binds to `PlayerContext.Instance` and resolves field allies
through FieldAllyManager. HP, ammo, Command Points and skill charge status are
read from their owners every 0.1 seconds. Readiness checks the actor's life and
active state plus CharacterSkillManager's existing permission/resource checks.
No new cooldown clock or charge pool is created.

HP fills and each Command Point segment use the opaque `PartyHudArt/SolidFill.asset`
sprite with horizontal fill starting at the left edge. Rounded masks retain
the track's end caps; Command Point segments share one continuous rounded track
and fill from the leftmost slot first. Unavailable skills dim both the frame
and the skill icon; ready skills restore both to white, while cooldown text remains readable.
Skill icons use circular masks and the `RB/UI/Party HUD Monochrome` material;
the original skill sprites remain unchanged. Portraits retain their original colors.
The BULLET caption is authored separately in the prefab. Runtime updates only
the ammunition count and does not prepend a caption, newline, or font-size markup.
Set the count's font size in its TMP Inspector and its spacing below BULLET through
its RectTransform, rather than leading blank lines that runtime text updates replace.
Keycaps and the Combo leading bracket/connecting links follow the visual
reference. Each Combo card owns its trailing link so hidden cards leave no stray links.

## Keyboard contract

`PartyHudInputRouter` belongs only to the new prefab:

- Release 1–4 before 0.4 seconds: request that column's Active.
- Reach 0.4 seconds: request Ultimate immediately; release cannot also request
  Active. A rejected Ultimate never falls back to Active.
- E consumes the earliest-expiring Combo. If none exists, E requests the existing
  soft-targeted melee. A rejected Combo attempt does not also perform melee.
- Requests retain skill costs. Ultimate adds no Command Point charge.
- Losing focus, pause, cinematic UI or rebinding cancels held presses.

While active, the router overrides only conflicting keyboard bindings on the
PlayerInput runtime action instance. Disabling/rebinding it restores the prior
overrides. The original `.inputactions` asset is unchanged. Gamepad mappings
remain the existing mappings; this new tap/hold contract is keyboard-specific.
Use one PlayerUI prefab at a time, rather than stacking old and new HUD instances.

## Authoring and adoption

HUD text uses `Assets/Fonts/Novecento/Novecentosanswide-DemiBold SDF.asset`,
baked at 80-point SDF16 with 8-pixel padding into a static 1024 atlas.
Liberation Sans supplies missing symbols through fallback. Existing non-HUD
text and TMP project defaults retain their original fonts.

The default game configuration assigns `PlayerUI_PartyHud.prefab` to
`PartySpawnConfigSO.playerUIPrefab`. Other custom configurations can use the
same reference. The prefab already includes the required
PlayerUIContext and PlayerUIRuntimeBinder.

The three supplied `Assets/Spain/UI` textures had Multiple sprite import with
no sprite subassets. Independent copies in `Assets/Prefab/User Interface/PartyHudArt`
use Single sprite import; originals and their import settings are untouched.
Edit RectTransforms and serialized view references directly in the new prefab.
`Tools > Party HUD > Create Separate Prefab` creates the initial copy only; it
refuses to overwrite an existing new prefab.

## Validation

Run `CheckAssemblyBuild.ps1` for default assembly compilation. `PartyHudTests`
covers short release, the exact hold threshold, no double-cast/fallback,
cancellation, keyboard override restoration, and prefab structure. Preview
renders use illustrative portraits/readiness values and are not live combat captures.
