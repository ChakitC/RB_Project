using System.Collections;
using UnityEngine;

namespace ZLZ.AnimeShader
{
    /// <summary>
    /// Scene-level controller that animates the global shader uniform
    /// <c>_TargetDarkenGlobal</c> — affects every ZLZ character whose material has
    /// the Darken feature enabled. Per-character opt-out is done on
    /// <c>ZLZ_CharacterVFX.Darken.Exclude()</c>.
    /// </summary>
    [AddComponentMenu("ZLZ/Anime Shader/ZLZ_Darken Manager")]
    public class ZLZ_DarkenManager : MonoBehaviour
    {
        public enum AnimState { Idle, Intro, Loop, Outro }

        [SerializeField] ZLZ_DarkenSettings _settings;

        public AnimState CurrentState { get; private set; } = AnimState.Idle;
        public bool IsActive() => CurrentState != AnimState.Idle;

        // ── Singleton access (first registered instance wins) ─────────────
        static ZLZ_DarkenManager s_Instance;
        public static ZLZ_DarkenManager Instance => s_Instance;

        // Reset the singleton reference at runtime start. Required for Fast Enter
        // Play Mode (Unity 6.6 default), which skips the AppDomain reload and would
        // otherwise leave a dangling reference from the previous Play mode session.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_Instance = null;
        }

        static readonly int s_PropId = Shader.PropertyToID("_TargetDarkenGlobal");

        static readonly ZLZ_FXAnimationSettings _fallback = new ZLZ_FXAnimationSettings
        {
            loopCount     = 0,
            introDuration = 0.6f,
            loopPeriod    = 0f,
            outroDuration = 0.6f,
            loopCurve     = AnimationCurve.Constant(0f, 1f, 1f),
        };
        ZLZ_FXAnimationSettings Anim => _settings != null ? _settings.animation : _fallback;

        Coroutine _routine;

        void OnEnable()
        {
            if (s_Instance == null) s_Instance = this;
            else if (s_Instance != this)
                Debug.LogWarning(
                    $"[ZLZ] Multiple ZLZ_DarkenManager in scene — '{s_Instance.name}' is the active singleton, '{name}' will be ignored.",
                    this);
        }

        void OnDisable()
        {
            StopRoutine();
            CurrentState = AnimState.Idle;
            // Reset the global so a disabled manager doesn't leave the scene dark.
            Shader.SetGlobalFloat(s_PropId, 0f);
            if (s_Instance == this) s_Instance = null;
        }

        // ── Public API ────────────────────────────────────────────────────
        /// <summary>Darken the whole scene: animate _TargetDarkenGlobal 0 → 1 → hold.</summary>
        public void Darken()
        {
            StopRoutine();
            _routine = StartCoroutine(CoIntroLoop());
        }

        /// <summary>Restore the scene to normal brightness: animate 1 → 0.</summary>
        public void Restore()
        {
            if (CurrentState == AnimState.Idle) return;
            StopRoutine();
            _routine = StartCoroutine(CoOutroHide());
        }

        /// <summary>Activate if idle, restore otherwise.</summary>
        public void ToggleDarken()
        {
            if (CurrentState == AnimState.Idle) Darken(); else Restore();
        }

        /// <summary>Set the global value immediately without animation.</summary>
        public void SetInstant(float value)
        {
            StopRoutine();
            CurrentState = AnimState.Idle;
            Shader.SetGlobalFloat(s_PropId, value);
        }

        // ── Coroutines (mirrors ZLZ_FXBlockBase pattern) ──────────────────
        IEnumerator CoIntroLoop()
        {
            yield return CoPhase(AnimState.Intro, Anim.introDuration, Anim.introCurve);
            yield return CoLoop();
        }

        IEnumerator CoLoop()
        {
            CurrentState = AnimState.Loop;
            if (Anim.loopPeriod <= 0f) { SetValue(1f); yield break; }
            int count = 0;
            while (Anim.loopCount == 0 || count < Anim.loopCount)
            {
                for (float t = 0f; t < Anim.loopPeriod; t += Time.deltaTime)
                {
                    SetValue(Anim.loopCurve.Evaluate(t / Anim.loopPeriod));
                    yield return null;
                }
                count++;
            }
            yield return CoPhase(AnimState.Outro, Anim.outroDuration, Anim.outroCurve);
            FinishToIdle();
        }

        IEnumerator CoOutroHide()
        {
            yield return CoPhase(AnimState.Outro, Anim.outroDuration, Anim.outroCurve);
            FinishToIdle();
        }

        IEnumerator CoPhase(AnimState state, float duration, AnimationCurve curve)
        {
            CurrentState = state;
            if (duration <= 0f) { SetValue(curve.Evaluate(1f)); yield break; }
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                SetValue(curve.Evaluate(t / duration));
                yield return null;
            }
            SetValue(curve.Evaluate(1f));
        }

        void SetValue(float value) => Shader.SetGlobalFloat(s_PropId, value);

        void FinishToIdle()
        {
            SetValue(0f);
            CurrentState = AnimState.Idle;
        }

        void StopRoutine()
        {
            if (_routine != null) { StopCoroutine(_routine); _routine = null; }
        }
    }
}
