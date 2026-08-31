using UnityEngine;

/// <summary>
/// The rendering/lighting channels the dialogue stage runs on.
///
/// The stage has no Unity layer of its own. It used to put its clones on a `DialogueActor` layer so
/// the portrait cameras could see only them and the gameplay camera could exclude them, but the
/// character render features filter by Unity layer: `ZLZ_CharacterContactShadowFeature.casterLayers`
/// and `ZLZ_ScreenSpaceOutlineFeature.characterLayers` are authored for the gameplay character
/// layer, and a clone on a layer of its own silently renders without them. Clones therefore sit on
/// layer 0 like every other character, and isolation is carried by the dialogue light channel plus
/// the distance the stage sits at.
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
    /// What a dialogue clone's renderers are set to: the stage's own light channel and nothing else.
    ///
    /// This deliberately does **not** include bit 0, which is the only bit the world's directional
    /// light claims. That single omission is what keeps the sun off the stage now that the clones
    /// share layer 0 with everything else.
    ///
    /// It also claims no other bit on purpose. The low rendering-layer bits are what
    /// `ZLZ_SelectionOutlineFeature` filters its selection types on, so a clone that claimed one
    /// would come onto the stage wearing a selection outline. ZLZ's character shading needs no
    /// rendering-layer opt-in of its own — it is driven by the material and by Unity-layer-filtered
    /// renderer features.
    /// </summary>
    public static uint ActorRenderingLayerMask => DialogueRenderingLayerMask;

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
