# Face Switcher Visibility

## Blender Face Presets (add-on 2.3.0)

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
Apply creates new groups (suffixing name collisions), rigid head weights and
Visibility controls; it preserves existing groups. Blender Undo/Redo and saving
unfinished placement are supported. No Unity importer or runtime is added.

This first version uses Attach to Head. It bakes geometry modifiers such as
Mirror and current shape-key values on disposable copies, excluding Armature
deformation. The source rig, mesh, materials, animation and weights are unchanged.
Imported copies do not retain facial deformation weights or shape-key animation;
Map Bones is not implemented. Different proportions/bone axes need manual fit.
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
