using UnityEngine;
using System;
using Animancer;
using Animancer.FSM;
public sealed partial class CharacterAnimBrain
{private sealed class Action_ShootPulse : ActionState
{
    private readonly CharacterAnimBrain owner;
    private AnimancerState state;
    private float lastPlayTime;

    public Action_ShootPulse(CharacterAnimBrain owner) => this.owner = owner;

    public override bool CanEnterState
    {
        get
        {
            if (owner.ShootPulseClip == null) return false;

            // ถ้ากดค้างและไม่มี holdLoop -> กันการรีสตาร์ตถี่เกิน (auto fire rate สูง ๆ)
            if (owner.IsHoldingFire && owner.ShootHoldLoopClip == null)
            {
                if (owner.AnimationTime - lastPlayTime < owner.HoldPulseMinInterval)
                    return false;
            }

            return true;
        }
    }

    public override void OnEnterState()
    {
        lastPlayTime = owner.AnimationTime;

        state = owner.PlayActionTransition(owner.ShootPulseClip, PairOffsetUpperAction.ShootPulse);

        owner.onShootEndCache ??= owner.HandleShootPulseEnd;
        if (state != null)
            state.Events(owner).OnEnd = owner.onShootEndCache;
    }

    public override void OnExitState()
    {
        // ไม่ต้อง fade out ที่นี่ เพราะตอนกลับ empty จะ fade out ให้
    }
}}
