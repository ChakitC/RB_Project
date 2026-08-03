using UnityEngine;

[DisallowMultipleComponent]
public sealed class InteractionIndicatorAnchor : MonoBehaviour
{
    [SerializeField] private Transform anchorOverride;

    public Transform Anchor => anchorOverride != null ? anchorOverride : transform;
}
