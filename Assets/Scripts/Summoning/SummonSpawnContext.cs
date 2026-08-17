using System.Collections.Generic;
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

    /// <summary>Resolved max HP for the summon, or 0 to keep the prefab's own authored health.</summary>
    public float MaxHealth { get; set; }

    /// <summary>Upgrade IDs the caster owned at cast time and that the summon should carry. May be null.</summary>
    public IReadOnlyList<string> UpgradeIds { get; set; }

    public int PerSkillCap { get; set; }
    public Vector3 Position { get; set; }
    public Quaternion Rotation { get; set; }
    public ulong SpawnSequence { get; set; }
}
