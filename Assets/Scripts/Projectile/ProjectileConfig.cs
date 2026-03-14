using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Combat/Projectile Config")]
public class ProjectileConfig : ScriptableObject
{
    public float baseSpeed = 30f;
    public float lifeTime = 3f;

    // เรียงลำดับมีผล (เช่น Pierce ก่อน Split ถ้า Split ทำตอนนัดสุดท้าย)
    public List<ProjectileModule> modules = new();
}