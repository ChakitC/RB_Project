/// <summary>
/// Final stat values produced by <see cref="CharacterStatFormula.Compute"/>.
/// Mirrors every stat <see cref="StatsHub"/> caches.
/// </summary>
public struct CharacterStatTotals
{
    public float Damage;
    public float Armor;
    public float MoveSpeed;
    public float CritRatePercent;
    public float CritMultiplier;
    public float FireInterval;
    public float ReloadTime;
    public float Stability;
    public float BulletSpeed;
    public int MaxMagazine;
    public int MaxReserveAmmo;
    public float MaxHealth;
    public float MaxStamina;
    public float MaxEnergy;
    public float SkillBaseDamage;
}
