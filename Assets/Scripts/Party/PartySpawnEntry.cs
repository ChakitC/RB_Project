using System;
using UnityEngine;

[Serializable]
public sealed class PartySpawnEntry
{
    [SerializeField] private ChainActorRole role;
    [SerializeField, Min(0)] private int partyIndex;
    [SerializeField] private GameObject prefab;
    [SerializeField] private Vector3 localPosition;
    [SerializeField] private Vector3 localEulerAngles;

    public ChainActorRole Role => role;
    public int PartyIndex => partyIndex;
    public GameObject Prefab => prefab;
    public Vector3 LocalPosition => localPosition;
    public Quaternion LocalRotation => Quaternion.Euler(localEulerAngles);

#if UNITY_EDITOR
    public PartySpawnEntry(
        ChainActorRole role,
        int partyIndex,
        GameObject prefab,
        Vector3 localPosition,
        Vector3 localEulerAngles)
    {
        this.role = role;
        this.partyIndex = partyIndex;
        this.prefab = prefab;
        this.localPosition = localPosition;
        this.localEulerAngles = localEulerAngles;
    }
#endif
}
