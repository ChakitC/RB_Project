using UnityEngine;

[CreateAssetMenu(
    fileName = "RestoreMagazineOnDashBehavior",
    menuName = "Combat/Passives/Behaviors/Restore Magazine On Dash")]
public sealed class RestoreMagazineOnDashPassiveBehavior : PassiveCustomBehavior
{
    [SerializeField, Range(0f, 1f)] private float procChance = 0.2f;
    [SerializeField, Min(0f)] private float magazineRestoreRatio = 0.2f;
    [SerializeField, Min(1)] private int minimumRestoreAmount = 1;

    // Serialized migration input only. Runtime restoration is owned by PassiveMagazineStep.
}