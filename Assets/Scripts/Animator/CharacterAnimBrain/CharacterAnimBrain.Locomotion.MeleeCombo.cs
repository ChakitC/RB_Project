using UnityEngine;
using System;
using Animancer;
using Animancer.FSM;
public sealed partial class CharacterAnimBrain
{
    // ===================== Locomotion: Melee Combo (Layer 0) =====================
    private sealed class Locomotion_MeleeCombo : LocomotionState
    {
        private readonly CharacterAnimBrain owner;
        private AnimancerState state;

        // รองรับหลายคอมโบ: เลือกชุดคอมโบก่อนเข้า state ด้วย SetCombo
        private MeleeComboSO comboLocked;

        public MeleeComboSO CurrentCombo => comboLocked;

        /// <summary>ตั้งคอมโบที่จะใช้ "รอบถัดไป" (ควรเรียกก่อน TrySetState)</summary>
        public void SetCombo(MeleeComboSO newCombo)
        {
            // ล็อคคอมโบที่ใช้ (โดยดีไซน์ไม่แนะนำให้สลับกลางคอมโบ)
            comboLocked = newCombo;
        }

        private bool _prevApplyRootMotion;
        private int step;
        private int bufferedPresses;
        private bool windowExpired;
        
        private bool _chainOpen;
        private bool _pressedInWindow;
        private bool _hasChain;
        private float _cs, _ce;
        private MeleeComboSO.Step _cfg;

        internal AnimancerState DebugState => state;
        internal int DebugBufferedPresses => bufferedPresses;
        internal bool DebugChainWindowOpen => _chainOpen;
        internal bool DebugPressedInWindow => _pressedInWindow;
        internal bool DebugWindowExpired => windowExpired;
        internal float DebugChainWindowStart => _cs;
        internal float DebugChainWindowEnd => _ce;
        

        public Locomotion_MeleeCombo(CharacterAnimBrain owner) => this.owner = owner;

        public override bool CanEnterState
        {
            get
            {
                // if(owner.ctx.stateHub.Isdown || !owner.ctx.stateHub.IsAlive)
                //     return false;

                // backward-compatible: ถ้ายังไม่ได้ SetCombo ให้ใช้ของเดิมใน owner
                var c = comboLocked != null ? comboLocked : owner.DefaultMeleeCombo;

                if (c == null) return false;
                if (!c.IsValid(out _)) return false;
                
                
                return true;
            }
        }
        

        public void QueueNextPress()
        {
            var c = comboLocked != null ? comboLocked : owner.DefaultMeleeCombo;
            if (c == null) return;

            int last = c.Steps.Count - 1;
            bool canRepeat = CanRepeatLastStep(last);

            int maxRemaining = last - step;
            if (maxRemaining <= 0 && !canRepeat) return;

            // ถ้าตั้งให้ทิ้ง buffer เมื่อหมด window: ยังคงพฤติกรรมเดิม
            if (windowExpired && _cfg.dropBufferOnWindowExpire)
                return;

            // ถ้า repeat last step ให้ buffer สูงสุดแค่ 1 ก็พอ
            bufferedPresses = canRepeat
                ? Mathf.Min(1, bufferedPresses + 1)
                : Mathf.Min(maxRemaining, bufferedPresses + 1);

            // ถ้ากดใน window แล้ว -> ไปต่อ/รีเพลย์ทันที
            if (_hasChain && _chainOpen && state != null)
            {
                float nt = state.NormalizedTime;
                if (nt >= _cs && nt <= _ce)
                {
                    _pressedInWindow = true;
                    Advance(); // Advance จะจัดการ repeat ให้เองด้านล่าง
                }
            }
        }

        public override void OnEnterState()
        {
            
            // lock combo at enter (ไม่สลับกลางคอมโบ)
            if (comboLocked == null)
                comboLocked = owner.DefaultMeleeCombo;

            _prevApplyRootMotion = owner.animancer.Animator.applyRootMotion;
            owner.animancer.Animator.applyRootMotion = true;
            owner.RootMotionActive = true;

            step = 0;
            bufferedPresses = 0;
            windowExpired = false;

            // เคลียร์ action layer (ยิง/รีโหลด/ฯลฯ) เพราะเมเล่เป็น full-body แล้ว
            owner.IsHoldingFire = false;
            owner._pendingPulse = false;
            owner._pendingAction = PendingAction.Empty;

            owner.actionSM.TrySetState(owner.empty);
            owner.ActLayer.StartFade(0f, 0.5f);

            PlayStep(0);
        }

        public override void OnExitState()
        {
            owner.animancer.Animator.applyRootMotion = _prevApplyRootMotion;
            owner.RootMotionActive = false;
            
            owner._pendingAction = PendingAction.Empty;
            owner.actionSM.TrySetState(owner.empty);

            owner.MeleeHitEnd?.Invoke();
            // ปล่อยให้ locomotion live เล่น mixer ต่อเองตอนกลับไป
        }

        private void PlayStep(int newStep)
    {
      
        step = newStep;
        windowExpired = false;

        var c = comboLocked != null ? comboLocked : owner.DefaultMeleeCombo;
        if (c == null) { EndComboSafe(); return; }

        var steps = c.Steps;
        if (step < 0 || step >= steps.Count) { EndComboSafe(); return; }

        var cfg = steps[step];
        _cfg = cfg;

        if (cfg.clip == null) { EndComboSafe(); return; }

        owner.CurrentMeleeStep = cfg;
        owner.CurrentMeleeStepIndex = step;

        // เล่นบน Layer 0
        state = owner.LocoLayer.Play(cfg.clip);

        // speed match ให้จบตาม duration
        if (cfg.duration > 0.01f)
        {
            float len = Mathf.Max(0.01f, state.Length);
            state.Speed = len / Mathf.Max(0.01f, cfg.duration);
        }
        else state.Speed = 1f;

        var ev = state.Events(owner);
        ev.Clear();

        // owner.onMeleeHitStartCache ??= () => owner.MeleeHitStart?.Invoke();
        owner.onMeleeHitStartCache = () =>
        {
            Debug.Log($"[Invoke] MeleeHitStart step={step} frame={Time.frameCount}");
            owner.MeleeHitStart?.Invoke();
        };
        
        owner.onMeleeHitEndCache = () => owner.MeleeHitEnd?.Invoke();

        
        // hit window
        float hs = Mathf.Clamp01(cfg.hitWindowN.x);
        float he = Mathf.Clamp01(cfg.hitWindowN.y);
        if (he < hs) (hs, he) = (he, hs);

        ev.Add(hs, owner.onMeleeHitStartCache);
        ev.Add(he, owner.onMeleeHitEndCache);

        // chain window
        int last = steps.Count - 1;
        float cs = Mathf.Clamp01(cfg.chainWindowN.x);
        float ce = Mathf.Clamp01(cfg.chainWindowN.y);
        if (ce < cs) (cs, ce) = (ce, cs);

        _cs = cs;
        _ce = ce;

        bool repeatLast = (step == last) && (ce > 0.0001f);
        _hasChain = ((step < last) || repeatLast) && (ce > 0.0001f);

        _chainOpen = false;
        _pressedInWindow = false;

        if (_hasChain)
        {
            // เปิด chain window
            ev.Add(_cs, () =>
            {
                _chainOpen = true;
                _pressedInWindow = false;

                // ถ้ากดบัฟเฟอร์มาก่อนแล้ว -> ไปต่อทันทีตอน window เปิด
                if (bufferedPresses > 0)
                {
                    _pressedInWindow = true;
                    Advance();
                }
            });

            // ปิด chain window
            ev.Add(_ce, () =>
            {
                _chainOpen = false;
                windowExpired = true;

                // ถ้าตั้งให้ทิ้ง buffer เมื่อพลาด window -> ทิ้งเฉพาะกรณี "ไม่ได้กดใน window"
                if (cfg.dropBufferOnWindowExpire && !_pressedInWindow)
                    bufferedPresses = 0;
            });
        }
        else
        {
            windowExpired = true;
        }

        // ✅ อย่าใช้ ??= ตรงนี้ (กันปัญหา delegate เก่าค้างหลัง re-init)
        owner.onMeleeEndCache = () =>
        {
            Debug.Log($"[MeleeCombo OnEnd] current={owner.locomotionSM.CurrentState}", owner);
            
            if (owner.locomotionSM.CurrentState != owner.meleeCombo)
                return;

            var cc = comboLocked != null ? comboLocked : owner.DefaultMeleeCombo;
            int last2 = cc.Steps.Count - 1;
            bool canRepeat = CanRepeatLastStep(last2);

            if (bufferedPresses > 0 && (step < last2 || canRepeat))
            {
                Advance(); // ถ้าเป็น last step + canRepeat -> จะรีเพลย์ตัวเดิม
                return;
            }

            EndComboSafe();
        };

        ev.OnEnd = owner.onMeleeEndCache;
    }
        private void Advance()
        {
            
            var c = comboLocked != null ? comboLocked : owner.DefaultMeleeCombo;
            if (c == null) return;

            int last = c.Steps.Count - 1;
            bool canRepeat = CanRepeatLastStep(last);

            // กรณีสุดท้ายแต่ repeat ได้ -> รีเพลย์ step เดิม
            if (step >= last)
            {
                if (!canRepeat) return;

                bufferedPresses = Mathf.Max(0, bufferedPresses - 1);
                PlayStep(step); // เล่นท่าเดิมซ้ำ
                return;
            }

            bufferedPresses = Mathf.Max(0, bufferedPresses - 1);
            
            PlayStep(step + 1);
            
            // Debug.Log($"PlayStep step={step} frame={Time.frameCount}");
            
        }

            private bool CanRepeatLastStep(int lastIndex)
            {
                // ถ้าเป็น step สุดท้าย และมี chainWindow (ce > 0) ให้ repeat ได้
                return step == lastIndex && _cfg.chainWindowN.y > 0.0001f;
            }

            private void EndComboSafe()
            {
                owner.animancer.Animator.applyRootMotion = _prevApplyRootMotion;
                owner.RootMotionActive = false;
            
                owner._pendingAction = PendingAction.Empty;
                owner.actionSM.TrySetState(owner.empty);
                
                owner.MeleeComboEnded?.Invoke();

                bool ok = owner.locomotionSM.TrySetState(owner.locomotion);
                Debug.Log($"[MeleeCombo] EndComboSafe -> locomotion ok={ok}", owner);

                if (!ok)
                {
                    Debug.LogWarning("[MeleeCombo] Failed to exit meleeCombo", owner);
                }
            }
        }
}
