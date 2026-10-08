#if UNITY_EDITOR
using UnityEngine;

// Explicit deterministic input for isolated weapon fixtures; production actors use camera/AI sources.
public sealed class WeaponAimTestSource : WeaponAimSource
{
    public CharacteContext Owner;
    public Vector3 Point = Vector3.forward * 20f;
    public Vector3 Forward = Vector3.forward;
    public float Movement;
    public bool Valid = true;
    public override CharacteContext OwnerContext => Owner;
    public override float MovementSpreadFactor => Movement;
    public override bool TryGetAimSample(out WeaponAimSample sample)
    {
        sample = new WeaponAimSample(Point, Forward);
        return Valid && sample.IsValid;
    }

    public static void Bind(WeaponSystem weapon, Transform actor)
    {
        var model = new GameObject("Test weapon model");
        model.transform.SetParent(actor, false);
        var muzzle = new GameObject("FirePoint").transform;
        muzzle.SetParent(model.transform, false);
        var binding = model.AddComponent<WeaponMuzzleBinding>();
        binding.Configure(muzzle);
        weapon.BindFirePoint(muzzle);
        var owner = weapon.ctx != null ? weapon.ctx : actor.GetComponent<CharacteContext>();
        if (owner == null) return;
        var source = actor.GetComponent<WeaponAimTestSource>();
        if (source == null) source = actor.gameObject.AddComponent<WeaponAimTestSource>();
        source.Owner = owner;
        source.Point = actor.position + actor.forward * 20f;
        source.Forward = actor.forward;
        owner.WeaponAim = source;
    }
}
#endif
