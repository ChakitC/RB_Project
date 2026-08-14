using UnityEngine;

public sealed class SummonSpawnContext
{
    public CharacteContext Caster { get; set; }
    public MapRunController Map { get; set; }
    public SummonSkillPayloadDef Definition { get; set; }
    public GameObject Prefab { get; set; }
    public string SkillId { get; set; }
    public SummonMobility Mobility { get; set; }
    public float Lifetime { get; set; }
    public float DespawnDelay { get; set; }
    public float InheritedDamage { get; set; }
    public float HealPower { get; set; }
    public float AreaRadius { get; set; }
    public float EffectDuration { get; set; }
    public int PerSkillCap { get; set; }
    public Vector3 Position { get; set; }
    public Quaternion Rotation { get; set; }
    public ulong SpawnSequence { get; set; }
}
