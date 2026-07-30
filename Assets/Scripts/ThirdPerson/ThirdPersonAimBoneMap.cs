using UnityEngine;

[DisallowMultipleComponent]
public sealed class ThirdPersonAimBoneMap : MonoBehaviour
{
    [SerializeField] private Transform spine;
    [SerializeField] private Transform chest;
    [SerializeField] private Transform upperChest;

    public Transform Spine => spine;
    public Transform Chest => chest;
    public Transform UpperChest => upperChest;
    public bool HasAnyBone => spine != null || chest != null || upperChest != null;
}
