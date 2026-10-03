# Face Switcher Visibility

## Remove an expression group (add-on 2.5.2)

Select a group in Face Switcher > Setup and click Remove Group beside Add Group
and Rename. The confirmation lists the expression count and affected Actions.
Removal deletes that group's selector property and curves in this rig's Action
slots, plus its local Face Controls slots and thumbnail records. Mesh objects remain;
their recorded original visibility and binding are restored. Helpers still used by
skin weights or bone parenting remain. Empty and final groups can also be removed.
The operator supports Undo. Setup requires a local, single-user rig; affected linked
Actions or slots shared with another object must be made local/single-user first.
The minus button in Face Controls only removes a display slot.

Validation: `Tools/Blender/FaceSwitcher/tests/test_remove_group.py` exposes `run()`
for an existing Blender session with the add-on enabled. It uses temporary fixtures,
cleans them up, and checks that the user's selection, mode and groups are unchanged.
It never saves/loads a file or launches another Blender process.

## Expression image previews (add-on 2.5.1)

Each Face Controls slot has Normal and Preview modes. Preview adapts from one to eight
columns to the Sidebar width and UI scale, with page arrows. Each slot starts at two
rows. Hold the left mouse button on the full-width Drag to Resize bar and drag down
for more rows or up for fewer; release to finish. Esc/right-click restores the old
height and page. Rows are capped at twelve and the available expression count.
The popup uses its fixed width. Resizing and changing columns update page capacity;
the last page retains the same cell widths. Per-image footers remain hidden. Click an image or its name to choose
that expression through the existing Auto Key operator. Switching modes or pages
does not change face values or insert keys. Normal keeps the previous name list.
Scroll the mouse wheel over preview tiles to browse the next/previous page of
images. Scrolling only moves the display, even with Auto Key on, and stops at the
first/last page. Empty cells on a partial last page also receive the wheel, so
scrolling back works without moving the pointer onto a remaining image. Empty
cells never select an expression or insert keys. Other UI lists and Normal mode keep their usual wheel behavior.
Image selection follows the group's current expression ID, including after reorder
or playback; it uses the same Auto Key operation as the name button and supports Undo.
Generate/Refresh Previews in Setup renders a group's copied meshes from rig-local
front (-Y) using Workbench textures/material colors and shared framing.
From 2.4.1, previews use Flat lighting with shadows, cavity and specular highlights
disabled. Refresh existing previews to apply this appearance. The generator inherits
the portable mesh/material restrictions of Face Presets. Refresh after editing the
source mesh/material; thumbnails are static and may differ from shader renders.
Images are packed in the blend file and stored in the scene, with per-rig/group/ID
references. Modes/pages/row counts are stored per control slot. Adding/removing other slots
or changing its group preserves that slot's row count. Save the file to retain them.
Override workflows require expression mesh overrides in the current view layer;
preview images are local to the animation file and do not auto-sync across files.

## Face Controls group display (add-on 2.3.4)

The Item sidebar and Face Controls popup initially show one expression group.
Click its group name to choose another, use + Add Group for another visible slot,
or use the minus button to remove a slot. Removing a slot only changes the UI;
meshes, expression definitions and animation keys remain. Layouts are stored per
rig in the scene, persist in the blend file and support Undo/Redo and Library
Overrides. Stable group tokens preserve selection across renaming/reordering.
Key All Groups and Reset Preview still affect every group. The main Animate panel
continues to display all groups.

## Add expressions from a Blender Collection (add-on 2.3.3)

In Face Switcher > Setup, select the destination group, then use Add Expressions
from Collection. Each mesh becomes one expression named after its object, sorted
by name. Include Child Collections is optional. Non-mesh objects and meshes already
registered on this rig are skipped; name collisions receive a numeric suffix.
The whole batch is checked against the existing binding rules before adding.
Enable source collections in the current view layer. Existing defaults and preview
selection are retained; an empty group uses the first expression as its default.
This setup operation requires a local rig and supports Blender Undo/Redo.

## Blender Face Presets (add-on 2.6.0)

Starting with 2.3.2, Apply preserves each mesh's preset origin after placement,
in the target rig's rest space. The shared placement box still transforms all
expressions together. Only residual shear is baked into vertices. The armature
deforms the mesh to follow the head; its object origin does not follow that pose.
This applies to new Load/Apply operations and does not change previously applied
meshes or automatically center their origins on geometry.

Use 2.3.1 or later when saving presets: it evaluates hidden expression copies
before capturing their world transform. Presets already saved with stale
transforms in 2.3.0 must be re-saved from their source rig; updating the add-on
alone cannot reconstruct an offset missing from the old archive.

Face Switcher > Face Presets saves selected/all groups into one `.facepreset`
archive with real meshes, packed still-image textures/materials, a thumbnail,
expression order and defaults. Load onto a local, single-user armature with a
chosen Head Bone, adjust the shared placement box, then Apply or Cancel.
Apply creates new groups (suffixing name collisions), selected skin bindings and
Visibility controls; it preserves existing groups. Blender Undo/Redo and saving
unfinished placement are supported. No Unity importer or runtime is added.

Schema 2 saves vertex group names, evaluated weights, empty groups and lock flags.
From 2.6.1, loading schema 2 defaults to Map Bones to preserve these weights.
Attach to Head is an explicit choice; old schema 1 files still default to it.
Attach to Head replaces deform weights with rigid helper weights while keeping masks.
Map Bones keeps saved weights and renames deform groups to chosen target bones.
Matching names are suggested automatically; source head/helper groups suggest the
chosen head. Missing/non-deforming targets, per-mesh mapping collisions and masks
whose names match target deform bones block Apply before any rig edits. Armature
Preserve Volume and mask settings are retained in Map Bones. Bone envelopes are
unsupported. No automatic weight normalization or weight transfer is performed.
Schema 1 remains readable in Attach to Head, but needs a new save from source meshes
to recover weights. Schema 2 archives require add-on 2.6.0 or newer.

Presets bake geometry modifiers such as Mirror and current shape-key values on
disposable copies, excluding Armature deformation. Source data remains unchanged.
Shape-key animation and the source skeleton are not transferred. Fit mapped meshes
in the target rest pose; posed bones will deform them after Apply. Different
proportions/bone axes may require manual placement and weight adjustments.
`tests/test_preset_weights.py` runs an isolated temporary scene in the existing
Blender instance, including real archive round trips and deformation checks.
Use the normal ARP export after Apply. The existing Unity base visibility and
clip import constraints below still apply. Authoring steps and validation are in
`Tools/Blender/FaceSwitcher/README.md` and `TEST_REPORT.md`.

## Recommended: embedded FBX Visibility (Blender add-on 2.2.0)

### Separate base model and linked animation (add-on 2.2.1)

The ARP checkbox is now installed before the first export after starting Blender
and appears above ARP's settings. With Include Face Switcher on, turning off
Bake Animations exports a **Base Model** containing every expression mesh and
its existing skinning, with no animation stacks or sidecar. Turning baking on
retains the animated export workflow below.

Export the base from the source rig and clips from its Library Override. Keep
Rig Name, bone names, mesh names and hierarchy consistent across both exports.
Use the base model's Animator/Animancer with the clips from the animation FBX.
Enable Import Visibility for both files and disable resampling/compression on
the animation FBX. Generic base + linked animation is verified.

Unity 6000.0.58f2 initially enables all skinned expression renderers on the base,
despite the static Visibility values stored in the FBX. Play an idle/action clip
with initial face keys, or set the initial Renderer Enabled overrides in your
prefab. Keep the mesh GameObjects active so the clip can animate their renderers.
No custom Unity importer is installed or needed.

Additional diagnosis on 6000.0.58f2: changing standard FBX Show/Visibility
properties did not preserve the initial disabled state for SkinnedMeshRenderer.
The same visibility values worked on MeshRenderer when skin deformers were
removed from a test copy. Rig=None also reads them but loses skinning, so it is
not an acceptable workaround for this model. No export-only solution preserving
skinning has been verified for the initial state. See
`Tools/Blender/FaceSwitcher/artifacts/base_default_visibility_diagnostic.json`.

Export with **File > Export > Auto-Rig Pro FBX**, **Include Face Switcher** and
**Bake Animations** enabled. Leave **Also Write Legacy .face** off. The Blender
add-on writes standard FBX Visibility properties and Constant animation curves
after ARP finishes baking. Unity's standard ModelImporter reads these as
`Renderer.m_Enabled`; no Face Switcher Unity package, sidecar, or runtime player
is required. Existing geometry, skin weights, transforms and body curves are
preserved. Generic animation is the tested workflow.

In Unity, enable **Import Visibility** and **Import Animation**, disable
**Resample Curves**, and set **Animation Compression = Off**. The tested default
resampling/compression caused early switches and two visible mouths between
keys. Copy only the FBX to Assets and use its model and clips normally. Current model:
`Assets/FaceSwitcher/NativeFBX/Roma_Pickup.fbx`. Enabled channels appear
under Renderer in the Animation window. Unity may insert a key immediately
before a stepped change; do not expect the same key count as the Blender UI.

### Preview visibility on a scene instance

Unity's AnimationClip Inspector uses AvatarPreview, which enables every Renderer
immediately before drawing and disables them afterwards. This overrides animated
Enabled values in that preview and can show all expressions together. Confirmed
on the running 6000.0.58f2 Inspector by inspecting its preview instance and invoking
its SetPreviewCharacterEnabled method; all six expressions became enabled.
The same behavior is visible in the [Unity reference source](https://github.com/Unity-Technologies/UnityCsReference/blob/master/Editor/Mono/Inspector/AvatarPreview.cs).

Select the model in the **Hierarchy**, enable **Preview** in the **Animation
window**, and scrub while watching **Scene View**. The model needs an Animator
with a Controller referencing the clip. Do not use the clip asset's Inspector
preview to validate Renderer visibility. The temporary preview scene and controller were removed after testing. Frames 0/99 show Roma_Emotion; frames 20 through just before 99 show
Roma_Emotion.008. The upside-down body pose is intentional in the source clip.

When migrating an existing asset, move its old sibling `.face` outside Assets
and remove FaceVisibilityPlayer from the model so the legacy integration does
not also control visibility. Blender backs up a matching previous export's
sidecar as `.face.<id>.bak`; mismatched old pairs require a new export filename.
Existing source animation files are not converted or overwritten by installing
the add-on. Re-export them to embed visibility.

The native workflow follows Unity's normal animation blending, without the
legacy player's strongest-input rule. Multiple clips/layers can therefore blend
Enabled values. A separate face layer or the legacy system may be needed when
crossfading must always select exactly one expression. Humanoid retargeting and
complex layer/mask configurations have not been verified.

Validation: native import and real Playables playback with no sidecar and no
custom MonoBehaviours, all six expressions, action and stretched NLA output,
frame boundaries and reverse sampling. See the Blender add-on TEST_REPORT.md.

## Project cleanup

The custom .face importer, FBX model postprocessor, shared import validation,
FaceVisibilityLibrary, FaceVisibilityPlayer and their Unity tests were removed.
No production prefab or scene referenced these scripts or generated assets.
Test models, the temporary preview scene/controller, and .face sidecars were
removed from Assets. The user's Roma_Pickup.fbx in NativeFBX remains.

Before removing the importer, the existing legacy clip at
Assets/Animation/Ch_Roma_Anim/Roma_Pickup.fbx was converted to embedded FBX
Visibility, retaining its asset GUID. Its original FBX, meta and sidecar were
backed up outside Assets under Tools/Blender/FaceSwitcher/artifacts/
before_unity_importer_removal. Both retained clips use standard Unity import.
The Blender add-on, source files, developer regression tests and historical
reports/backups outside Assets remain available.

Validation uses Unity script compilation, CheckAssemblyBuild.ps1, and sampling
both retained clips without any Face Switcher Unity code installed.
