using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class StartingAmmoTestFixture
{
    public void Run()
    {
        Assert.That(SaveManager.Instance, Is.Null, "Run in an isolated test scene without a save manager.");
        var gun = ScriptableObject.CreateInstance<GunConfig>();
        var actor = new GameObject("Starting ammo test");
        actor.SetActive(false);
        try
        {
            gun.magazine = gun.maxMagazine = 10;
            gun.maxReserveAmmo = 40;
            var weapon = actor.AddComponent<WeaponSystem>();
            WeaponAimTestSource.Bind(weapon, actor.transform);
            var inventory = actor.AddComponent<PlayerInventory>();
            typeof(PlayerInventory).GetField("weaponSystem", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(inventory, weapon);

            foreach (bool infinite in new[] { false, true })
            {
                gun.infiniteReserveAmmo = infinite;
                foreach (bool weaponStartsFirst in new[] { false, true })
                {
                    var instance = WeaponInstanceFactory.CreatePlainInstance(gun);
                    weapon.Equip(gun, instance);
                    weapon.magazine = 2;
                    weapon.reserveAmmo = 3;
                    if (weaponStartsFirst) Start(weapon);
                    Start(inventory);
                    if (!weaponStartsFirst) Start(weapon);
                    Assert.That(weapon.CurrentAmmo, Is.EqualTo(weapon.MagazineSize));
                    Assert.That(weapon.CurrentReserveAmmo, Is.EqualTo(infinite ? -1 : weapon.ReserveAmmoSize));
                    Assert.That(instance.currentMagazine, Is.EqualTo(weapon.MagazineSize));

                    weapon.magazine = 4;
                    weapon.reserveAmmo = 7;
                    weapon.Equip(gun, instance);
                    Assert.That(weapon.CurrentAmmo, Is.EqualTo(4), "Ordinary equip must preserve spent ammo.");
                    Assert.That(weapon.CurrentReserveAmmo, Is.EqualTo(infinite ? -1 : 7));
                }
            }

            var state = new WeaponAmmoState();
            var upgraded = WeaponInstanceFactory.CreatePlainInstance(gun);
            upgraded.currentMagazine = 15;
            state.Equip(gun, upgraded, 15, 40, false);
            Assert.That(state.Magazine, Is.EqualTo(15), "Upgraded magazine must survive equip.");
            state.Equip(gun, upgraded, 8, 40, false);
            Assert.That(state.Magazine, Is.EqualTo(8), "Reduced capacity must still clamp ammo.");
        }
        finally
        {
            Object.DestroyImmediate(actor);
            Object.DestroyImmediate(gun);
        }
    }

    static void Start(MonoBehaviour target) => target.GetType()
        .GetMethod("Start", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
}
