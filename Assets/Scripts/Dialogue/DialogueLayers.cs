using UnityEngine;

/// <summary>
/// The rendering/lighting channels the dialogue stage runs on.
///
/// The stage no longer has a Unity layer of its own. It used to put its clones on a `DialogueActor`
/// layer so the portrait cameras could see only them and the gameplay camera could exclude them, but
/// two of ASP's renderer features filter by Unity layer as well as by rendering layer and are
/// authored for layer 0 — `ASPMeshOutlineRendererFeature` and `ASPDepthOffsetShadowFeature`. Their
/// `Layer` field holds a single layer, not a mask, so one renderer cannot serve both the gameplay
/// layer and a dialogue layer, and a second URP renderer is not an option either: ASP's full-screen
/// passes keep per-pipeline state and produce a badly distorted image when two renderers draw in the
/// same frame. Clones therefore sit on layer 0 like every other character, and isolation is carried
/// entirely by rendering layers plus the distance the stage sits at.
/// </summary>
public static class DialogueLayers
{
    /// <summary>
    /// Rendering layer (URP light layers) reserved for the dialogue stage. **Lights** use exactly
    /// this and nothing else, which is what keeps the stage rig off the gameplay world: a world
    /// renderer answers to every bit, so a dialogue light that claimed any other bit would light the
    /// whole level.
    /// </summary>
    public const int DialogueRenderingLayerIndex = 5;

    public static uint DialogueRenderingLayerMask => 1u << DialogueRenderingLayerIndex;

    /// <summary>
    /// Rendering layers ASP's layer-filtered renderer features draw. Bit 1 is
    /// `ASPDepthOffsetShadowFeature`, bit 2 is `ASPMeshOutlineRendererFeature`. Clones have to claim
    /// these or they render without the mesh outline and depth-offset shadow that the same character
    /// has in gameplay — which reads as the portrait being flatter than the game.
    ///
    /// These are values authored on the URP renderer, so they are mirrored here rather than derived.
    /// `DialogueAuthoringValidator` checks the two against the renderer and reports any drift.
    /// </summary>
    public const uint AspFeatureRenderingLayerMask = (1u << 1) | (1u << 2);

    /// <summary>
    /// What a dialogue clone's renderers are set to: the stage's own light channel plus the channels
    /// ASP's features filter on.
    ///
    /// This deliberately does **not** include bit 0, which is the only bit the world's directional
    /// light claims. That single omission is what keeps the sun off the stage now that the clones
    /// share layer 0 with everything else.
    /// </summary>
    public static uint ActorRenderingLayerMask => DialogueRenderingLayerMask | AspFeatureRenderingLayerMask;

    /// <summary>
    /// Unity layer the clones live on, and the only layer a portrait camera draws.
    ///
    /// Layer 0 is not isolation, so the stage is parked far below the level instead — see
    /// <see cref="DialoguePresentationScene"/>. A portrait camera's far plane is a few metres, so it
    /// cannot reach the world; the gameplay camera cannot reach the stage because the stage is
    /// further away than its far plane by a wide margin.
    /// </summary>
    public const int ActorLayer = 0;

    public static int ActorLayerMask => 1 << ActorLayer;
}
