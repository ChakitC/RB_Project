using UnityEngine;

/// <summary>
/// Authoring marker that places one party role during the MapRun stage intro.
/// Lives under a <see cref="StageIntroRig"/> so the pose follows the room instance transform.
/// </summary>
[DisallowMultipleComponent]
public sealed class StageIntroActorMarker : MonoBehaviour
{
    [SerializeField]
    [Tooltip("Party role placed on this marker. Every rig needs exactly one marker per role.")]
    private ChainActorRole role = ChainActorRole.Player;

    [SerializeField, Min(0f)]
    [Tooltip("Gizmo radius used to preview the marker footprint in the Scene view.")]
    private float gizmoRadius = 0.35f;

    public ChainActorRole Role => role;
    public Vector3 Position => transform.position;
    public Quaternion Rotation => transform.rotation;

#if UNITY_EDITOR
    public void SetRoleForAuthoring(ChainActorRole value) => role = value;
#endif

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.25f, 0.85f, 1f, 0.65f);
        Gizmos.DrawWireSphere(transform.position, gizmoRadius);
        Gizmos.DrawLine(transform.position, transform.position + transform.forward * (gizmoRadius * 2.5f));
    }
}
