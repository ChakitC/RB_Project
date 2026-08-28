using UnityEngine;

/// <summary>
/// Names of the dedicated dialogue rendering/lighting channels, resolved once so a missing layer is
/// reported one time instead of every frame.
/// </summary>
public static class DialogueLayers
{
    public const string ActorLayerName = "DialogueActor";

    /// <summary>
    /// Rendering layer (URP light layers) reserved for the dialogue stage. Dialogue lights only
    /// touch this channel and world lights never do, so the two lighting worlds stay separate.
    /// </summary>
    public const int DialogueRenderingLayerIndex = 5;

    public static uint DialogueRenderingLayerMask => 1u << DialogueRenderingLayerIndex;

    static int _actorLayer = -2;   // -2 = unresolved, -1 = missing

    /// <summary>The DialogueActor layer index, or -1 when the project has no such layer.</summary>
    public static int ActorLayer
    {
        get
        {
            if (_actorLayer == -2)
            {
                _actorLayer = LayerMask.NameToLayer(ActorLayerName);
                if (_actorLayer < 0)
                {
                    Debug.LogWarning(
                        $"[Dialogue] Layer '{ActorLayerName}' is not defined in Tags & Layers. " +
                        "Dialogue actors will render into the gameplay view.");
                }
            }

            return _actorLayer;
        }
    }

    public static int ActorLayerMask => ActorLayer >= 0 ? 1 << ActorLayer : 0;
}
