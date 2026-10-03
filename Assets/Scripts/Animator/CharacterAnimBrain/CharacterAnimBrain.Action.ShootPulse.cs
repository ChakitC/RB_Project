using UnityEngine;
using System;
using Animancer;
using Animancer.FSM;
public sealed partial class CharacterAnimBrain
{private sealed class Action_ShootPulse : SingleClipActionState
{
    private float lastPlayTime;

    public Action_ShootPulse(CharacterAnimBrain owner) : base(owner) { }

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

    protected override void EnterPlayback()
    {
        lastPlayTime = owner.AnimationTime;

        playback.Play(new AnimationPlaybackOptions
        {
            Transition = owner.ShootPulseClip, TargetLayer = AnimationPlaybackOptions.Layer.UpperAction,
            PairAction = PairOffsetUpperAction.ShootPulse,
        }, onEnd: endCallback);
    }

    protected override void OnPlaybackEnd() => owner.HandleShootPulseEnd();
}}
