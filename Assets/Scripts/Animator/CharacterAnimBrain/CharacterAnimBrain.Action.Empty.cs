using UnityEngine;

public sealed partial class CharacterAnimBrain
{
    private sealed class Action_Empty : ActionState
    {
        private readonly CharacterAnimBrain owner;
        public Action_Empty(CharacterAnimBrain owner) => this.owner = owner;

        public override void OnEnterState()
        {
            owner.ActLayer.StartFade(0f, owner.ActionFadeOut);
        }
    }
}
