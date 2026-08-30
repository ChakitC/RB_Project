using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.BehaviorDesigner.Runtime.Tasks.Actions;
using Opsive.GraphDesigner.Runtime.Variables;

/// <summary>
/// Opens a Special Shoot Point round on this enemy.
///
/// One-shot by design: the task never stays <c>Running</c> for the four-second challenge. It asks
/// the controller to start and reports immediately, so the tree stays free to keep fighting while
/// the round plays out. All gameplay state lives in
/// <see cref="SpecialShootPointController"/>; this class is only the adapter.
/// </summary>
[Opsive.Shared.Utility.Category("Enemy/Special Shoot Point")]
[Opsive.Shared.Utility.Description(
    "Success only when the controller accepts and starts a round. Failure when the feature is " +
    "unavailable: no controller, missing profile/anchors, a round already running, cooldown, " +
    "Death/Down, Cutscene, Chain Attack, ChainReady, Full Stun, or post-Stagger immunity. " +
    "Does not stay Running for the challenge.")]
public class TriggerSpecialShootPointRound : ActionNode
{
    [UnityEngine.Tooltip(
        "Optional point-count override, clamped to the profile maximum and the usable anchor " +
        "count. Zero or less uses the profile default.")]
    public SharedVariable<int> pointCountOverride;

    private SpecialShootPointController controller;

    public override void OnStart()
    {
        CacheReferences();
    }

    public override TaskStatus OnUpdate()
    {
        CacheReferences();

        if (controller == null)
            return TaskStatus.Failure;

        int requestedCount = pointCountOverride != null ? pointCountOverride.Value : 0;
        return controller.TryStartRound(requestedCount) ? TaskStatus.Success : TaskStatus.Failure;
    }

    void CacheReferences()
    {
        if (controller != null)
            return;

        controller = SpecialShootPointTaskUtility.Resolve(gameObject);
    }

    public override void Reset()
    {
        pointCountOverride = null;
        controller = null;
    }
}
