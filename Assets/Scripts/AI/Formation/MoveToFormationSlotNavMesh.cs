using UnityEngine;
using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.BehaviorDesigner.Runtime.Tasks.Actions;
using Opsive.GraphDesigner.Runtime.Variables;

[Opsive.Shared.Utility.Category("Ally/Follow")]
[Opsive.Shared.Utility.Description("Moves an ally to its stable party formation slot.")]
public class MoveToFormationSlotNavMesh : Action
{
    public SharedVariable<CharacteContext> context;
    public SharedVariable<bool> InCombat;
    public SharedVariable<GameObject> player;

    readonly PartyFormationFollowRuntime _formationRuntime = new();

    public override void OnStart()
    {
        _formationRuntime.TryBegin(AIContextBinding.Read(context), ResolvePlayerObject(), InCombat);
    }

    public override TaskStatus OnUpdate()
    {
        return _formationRuntime.TryTick(AIContextBinding.Read(context), ResolvePlayerObject(), InCombat, out TaskStatus status)
            ? status
            : TaskStatus.Failure;
    }

    public override void OnEnd()
    {
        _formationRuntime.End();
    }

    public override void Reset()
    {
        _formationRuntime.Reset();
        player = null;
    }

    protected virtual GameObject ResolvePlayerObject()
    {
        return player != null ? player.Value : null;
    }
}
