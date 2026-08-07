using UnityEngine;

[CreateAssetMenu(
    fileName = "RestoreMagazineOnDashBehavior",
    menuName = "Combat/Passives/Behaviors/Restore Magazine On Dash")]
public sealed class RestoreMagazineOnDashPassiveBehavior : PassiveCustomBehavior
{
    [SerializeField, Range(0f, 1f)] private float procChance = 0.2f;
    [SerializeField, Min(0f)] private float magazineRestoreRatio = 0.2f;
    [SerializeField, Min(1)] private int minimumRestoreAmount = 1;

    public override void OnPassiveEvent(
        PassiveController controller,
        CustomPassiveDef definition,
        in PassiveEventContext context,
        SkillUpgradeStatSnapshot upgrades)
    {
        if (context.Type != PassiveEventType.DashStarted || controller == null)
            return;

        WeaponSystem weaponSystem = ResolveWeaponSystem(controller);
        if (weaponSystem == null || weaponSystem.MagazineSize <= 0)
            return;

        float chance = Mathf.Clamp01(procChance);
        if (chance <= 0f)
            return;

        int restoreAmount = Mathf.Max(
            minimumRestoreAmount,
            Mathf.CeilToInt(weaponSystem.MagazineSize * Mathf.Max(0f, magazineRestoreRatio)));

        if (!weaponSystem.CanRestoreMagazine(restoreAmount))
            return;

        if (Random.value > chance)
            return;

        weaponSystem.RestoreMagazine(restoreAmount);
    }

    static WeaponSystem ResolveWeaponSystem(PassiveController controller)
    {
        if (controller == null)
            return null;

        CharacteContext ctx = controller.GetComponent<CharacteContext>();
        if (ctx == null)
            ctx = controller.GetComponentInParent<CharacteContext>();

        if (ctx != null)
        {
            ctx.ResolveReferences();
            if (ctx.WeaponSystem != null)
                return ctx.WeaponSystem;
        }

        WeaponSystem weaponSystem = controller.GetComponent<WeaponSystem>();
        if (weaponSystem != null)
            return weaponSystem;

        weaponSystem = controller.GetComponentInParent<WeaponSystem>();
        if (weaponSystem != null)
            return weaponSystem;

        return controller.GetComponentInChildren<WeaponSystem>(true);
    }
}
