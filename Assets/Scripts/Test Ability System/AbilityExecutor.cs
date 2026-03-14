using UnityEngine;
using System.Collections.Generic;

public class AbilityExecutor : MonoBehaviour
{
    [SerializeField] Transform castPoint;
    [SerializeField] AbilityDef[] loadout;

    readonly Dictionary<string, float> _cd = new(); // abilityId -> cooldownLeft

    public CombatParams DefaultCombat; // ตั้งค่าเริ่มต้นจากอาวุธของตัวละคร

    void Awake()
    {
        foreach (var a in loadout)
        {
            if (!a) continue;
            _cd[a.id] = 0f;
        }
    }

    void Update()
    {
        float dt = Time.deltaTime;
        var keys = new List<string>(_cd.Keys);
        foreach (var k in keys) if (_cd[k] > 0f) _cd[k] -= dt;
    }

    public bool TryCast(string abilityId, Vector3 aimDir, CombatParams? overrideCombat = null)
    {
        var ab = System.Array.Find(loadout, x => x && x.id == abilityId);
        if (!ab) return false;
        if (_cd[abilityId] > 0f) return false;

        var cp = overrideCombat ?? DefaultCombat;
        var origin = castPoint ? castPoint.position : transform.position;
        var ctx = new AbilityContext(ab, transform, origin, aimDir.normalized, cp);

        // สำหรับ TargetMode.Point เราจะให้โปรเจกไทล์ไปจัดการต่อเอง
        foreach (var e in ab.effects)
            e.Apply(ctx, System.Array.Empty<Collider>());

        _cd[abilityId] = ab.cooldown;
        return true;
    }
}