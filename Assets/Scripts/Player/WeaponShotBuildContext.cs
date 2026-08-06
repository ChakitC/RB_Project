using UnityEngine;

public struct WeaponShotBuildContext
{
    public ProjectileConfig ProjectileConfig;
    public GameObject ProjectilePrefab;
    public Transform FirePoint;
    public float Damage;
    public float Speed;
    public float CritRate;
    public float CritMultiplier;
    public float StaggerPower;
    public Vector3 Direction;
    public string WeaponSourceId;
    public string WeaponInstanceId;
    public string AttackId;
    public int AmmoBefore;
    public int AmmoAfter;
    public int MaxMagazine;
    public bool AmmoConsumed;
    public bool IsLastRound;
    public PassiveEventContext PassiveContext;
    public WeaponAffixImpactPayload ImpactPayload;
}
