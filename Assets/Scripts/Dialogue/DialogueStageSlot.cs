using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One isolated portrait cell on the dialogue stage. The cell owns its actor anchor, camera,
/// runtime RenderTexture, lights, and the RawImage that composes its output on the dialogue canvas.
/// Cells are spaced far enough apart that a camera cannot render a neighbouring actor.
/// </summary>
[DisallowMultipleComponent]
public sealed class DialogueStageSlot : MonoBehaviour
{
    [SerializeField] private DialogueSlot slot = DialogueSlot.Center;

    [SerializeField, Tooltip("Where the actor clone is parented. Empty uses this transform.")]
    private Transform anchor;

    [Header("Rendering")]
    [SerializeField, Tooltip("Renders only this cell into its runtime RenderTexture.")]
    private Camera portraitCamera;

    [SerializeField, Tooltip("UI image that composes this slot over the frozen gameplay view.")]
    private RawImage portraitImage;

    [Header("Lights")]
    [SerializeField, Tooltip("Main light on this actor. Only affects the dialogue rendering layer.")]
    private Light keyLight;

    [SerializeField] private Light rimLight;

    RenderTexture runtimeTexture;

    // Camera fitting overwrites the camera's position every time the speaker changes, so the authored
    // placement has to be remembered separately or it is lost after the first fit.
    Vector3 authoredCameraLocalPosition;
    bool authoredCameraCaptured;

    public DialogueSlot Slot => slot;
    public Transform Anchor => anchor != null ? anchor : transform;
    public Camera PortraitCamera => portraitCamera;
    public RawImage PortraitImage => portraitImage;
    public Light KeyLight => keyLight;
    public Light RimLight => rimLight;
    public RenderTexture OutputTexture => runtimeTexture;

    public DialogueActorVisual Occupant { get; private set; }

    /// <summary>
    /// The camera's authored local position. Fitting keeps the sideways component of this so the
    /// actor stays on the cell's centreline: bounds include held props — a parasol, a slung rifle —
    /// and letting the bounds centre drive X slides the camera sideways until the body is off-centre
    /// even though the bounding box is not.
    /// </summary>
    public Vector3 AuthoredCameraLocalPosition
    {
        get
        {
            CaptureAuthoredCamera();
            return authoredCameraLocalPosition;
        }
    }

    void Awake()
    {
        CaptureAuthoredCamera();
    }

    void CaptureAuthoredCamera()
    {
        if (authoredCameraCaptured || portraitCamera == null)
            return;

        authoredCameraLocalPosition = portraitCamera.transform.localPosition;
        authoredCameraCaptured = true;
    }

    internal void SetOccupant(DialogueActorVisual actor)
    {
        Occupant = actor;
    }

    internal void ClearOccupant()
    {
        Occupant = null;
    }

    internal RenderTexture EnsureOutputTexture(int width, int height)
    {
        width = Mathf.Max(2, width);
        height = Mathf.Max(2, height);

        if (runtimeTexture != null &&
            (runtimeTexture.width != width || runtimeTexture.height != height))
        {
            ReleaseOutputTexture();
        }

        if (runtimeTexture == null)
        {
            runtimeTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
            {
                name = $"DialogueSlot_{slot}_Output",
            };
            runtimeTexture.Create();
        }

        if (portraitCamera != null)
            portraitCamera.targetTexture = runtimeTexture;

        if (portraitImage != null)
            portraitImage.texture = runtimeTexture;

        return runtimeTexture;
    }

    internal void ReleaseOutputTexture()
    {
        if (runtimeTexture == null)
            return;

        if (portraitCamera != null && portraitCamera.targetTexture == runtimeTexture)
            portraitCamera.targetTexture = null;

        if (portraitImage != null && portraitImage.texture == runtimeTexture)
            portraitImage.texture = null;

        runtimeTexture.Release();
        Destroy(runtimeTexture);
        runtimeTexture = null;
    }

    internal void SetRenderingEnabled(bool enabled)
    {
        bool hasActor = Occupant != null;

        if (portraitCamera != null)
        {
            // Portrait framing is built on orthographicSize, so the projection is pinned here rather
            // than trusted to stay as authored.
            portraitCamera.orthographic = true;
            portraitCamera.cullingMask = DialogueLayers.ActorLayerMask;
            portraitCamera.clearFlags = CameraClearFlags.SolidColor;
            portraitCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            portraitCamera.enabled = enabled && hasActor && runtimeTexture != null;
        }

        if (portraitImage != null)
            portraitImage.enabled = enabled && hasActor && runtimeTexture != null;
    }

    /// <summary>Turns this slot's lights off. Called when the slot is empty or dialogue has ended.</summary>
    internal void SetLightsEnabled(bool enabled)
    {
        if (keyLight != null)
            keyLight.enabled = enabled;

        if (rimLight != null)
            rimLight.enabled = enabled;
    }

    internal void CollectValidationIssues(System.Collections.Generic.List<string> issues)
    {
        if (anchor == null)
            issues.Add($"Stage slot '{slot}' has no actor anchor.");

        if (portraitCamera == null)
            issues.Add($"Stage slot '{slot}' has no portrait camera.");

        if (portraitImage == null)
            issues.Add($"Stage slot '{slot}' has no portrait RawImage.");
    }
}
