using UnityEngine;
using System;
using Animancer;
using Animancer.FSM;
public sealed partial class CharacterAnimBrain
{
    // ===================== Locomotion: Dash  ===============================
    
    private sealed class Locomotion_Dash : LocomotionState
    {
        private readonly CharacterAnimBrain owner;
        private AnimancerState state;

        public Locomotion_Dash(CharacterAnimBrain owner) => this.owner = owner;

        public override bool CanEnterState =>
            owner.DashForward != null &&
            owner.DashBackward != null &&
            owner.DashLeft != null &&
            owner.DashRight != null;

        public override void OnEnterState()
        {
            // ล็อคทิศ dash เป็น 4 ทิศ
            Vector2 dir = Snap4(owner._dashDirLocal);

            ClipTransition clip = Pick(owner, dir);

            state = owner.LocoLayer.Play(clip);

            // ปรับ speed ให้จบพอดีกับเวลา dash ของเกม
            float len = Mathf.Max(0.01f, state.Length);
            float dur = Mathf.Max(0.01f, owner._dashDuration);
            state.Speed = len / dur;

            owner.onDashEndCache ??= () => owner.locomotionSM.TrySetState(owner.locomotion);
            state.Events(owner).OnEnd = owner.onDashEndCache;
        }

        private static ClipTransition Pick(CharacterAnimBrain o, Vector2 dir4)
        {
            // dir4 จะเป็น (0,1)(0,-1)(1,0)(-1,0)
            if (dir4.y > 0.5f)  return o.DashForward;
            if (dir4.y < -0.5f) return o.DashBackward;
            if (dir4.x > 0.5f)  return o.DashRight;
            return o.DashLeft;
        }

        private static Vector2 Snap4(Vector2 v)
        {
            if (v.sqrMagnitude < 0.0001f) return Vector2.up; // ไม่มีทิศ -> ให้พุ่งหน้าเป็นค่าเริ่มต้น

            // เลือกแกนที่เด่นกว่า: X หรือ Y
            if (Mathf.Abs(v.x) > Mathf.Abs(v.y))
                return (v.x >= 0f) ? Vector2.right : Vector2.left;
            else
                return (v.y >= 0f) ? Vector2.up : Vector2.down;
        }
    }

}
