using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ZLZ.AnimeShader
{
    // Runtime only — UI is managed by ZLZ_CharacterDashboard.
    [AddComponentMenu("")]
    public class ZLZ_SelectionController : MonoBehaviour
    {
        // Each value is a Rendering Layer INDEX (not a bitmask).
        public enum SelectionType
        {
            Character = 1,
            Enemy     = 2,
            Item      = 3,
        }

        public enum AnimState { Idle, Intro, Loop, Outro }

        [HideInInspector] public SelectionType defaultType   = SelectionType.Character;
        [HideInInspector] public bool          startSelected = false;

        // All controllers currently animated — read by SelectionOutlineAnimPass.
        internal static readonly List<ZLZ_SelectionController> s_Animating =
            new List<ZLZ_SelectionController>();

        Renderer[] _renderers;
        Material   _outlineMat;
        Coroutine  _routine;

        static readonly int ID_OutlineColor = Shader.PropertyToID("_OutlineColor");
        static readonly int ID_OutlineWidth = Shader.PropertyToID("_OutlineWidth");

        // Synced from ZLZ_SelectionOutlineFeature every frame via SyncSettings().
        ZLZ_SelectionOutlineFeature.TypeSettings      _type;
        ZLZ_SelectionOutlineFeature.AnimationSettings _anim;
        Color _baseColor = Color.white;
        float _baseWidth = 2f;

        public AnimState CurrentState { get; private set; } = AnimState.Idle;

        internal Renderer[] Renderers => _renderers;
        internal Material   OutlineMat => _outlineMat;

        void Awake()
        {
            _renderers = GetComponentsInChildren<Renderer>(true);

            var shader = Shader.Find("ZLZ/SelectionOutline");
            if (shader != null)
            {
                _outlineMat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            }
        }

        void Start()
        {
            if (startSelected) Select();
        }

        void OnDisable()
        {
            // Stop animation and detach from the global list so a disabled controller
            // doesn't keep drawing or hold a stale reference.
            StopRoutine();
            s_Animating.Remove(this);
            CurrentState = AnimState.Idle;
        }

        void OnDestroy()
        {
            s_Animating.Remove(this);
            if (_outlineMat != null) Destroy(_outlineMat);
        }

        // ─── Called by feature every frame to push latest settings ───────────

        internal void SyncSettings(ZLZ_SelectionOutlineFeature.TypeSettings type,
                                    ZLZ_SelectionOutlineFeature.AnimationSettings anim)
        {
            _type      = type;
            _anim      = anim;
            _baseColor = type.outlineColor;
            _baseWidth = type.outlineWidth;
        }

        // ─── Public API — instant (no animation) ─────────────────────────────

        public void Select(SelectionType type)
        {
            if (_renderers == null) return;
            uint bit = LayerBit(type);
            foreach (var r in _renderers)
                if (r != null) r.renderingLayerMask |= bit;
        }

        public void Deselect(SelectionType type)
        {
            if (_renderers == null) return;
            uint bit = LayerBit(type);
            foreach (var r in _renderers)
                if (r != null) r.renderingLayerMask &= ~bit;
        }

        public void ToggleSelection(SelectionType type)
        {
            if (IsSelected(type)) Deselect(type); else Select(type);
        }

        public bool IsSelected(SelectionType type)
        {
            if (_renderers == null || _renderers.Length == 0) return false;
            return (_renderers[0].renderingLayerMask & LayerBit(type)) != 0;
        }

        // ─── Public API — animated ────────────────────────────────────────────

        /// <summary>
        /// Show the outline using the configured animation (Intro → Loop). Falls back to
        /// the instant path automatically if no Selection Outline Renderer Feature is registered.
        /// </summary>
        public void Select()
        {
            if (ZLZ_SelectionOutlineFeature.TryGetSettings(defaultType, out var t, out var a))
            {
                _type      = t;
                _anim      = a;
                _baseColor = t.outlineColor;
                _baseWidth = t.outlineWidth;
                AnimSelect();
            }
            else
            {
                Select(defaultType);
            }
        }

        /// <summary>
        /// Hide the outline using the configured Outro animation. Falls back to instant
        /// removal if no Renderer Feature is registered.
        /// </summary>
        public void Deselect()
        {
            if (CurrentState != AnimState.Idle && _anim != null)
                AnimDeselect();
            else
                Deselect(defaultType);
        }

        /// <summary>Activate the outline if currently hidden, otherwise hide it.</summary>
        public void ToggleSelection()
        {
            if (!IsSelected()) Select(); else Deselect();
        }

        /// <summary>
        /// True while the outline is visible — covers all animation phases (Intro/Loop/Outro)
        /// and the instant path (renderingLayerMask).
        /// </summary>
        public bool IsSelected()
        {
            // Animated path: any non-Idle state means the outline is currently drawn.
            if (CurrentState != AnimState.Idle) return true;
            // Instant path fallback: check renderingLayerMask.
            return IsSelected(defaultType);
        }

        // ─── Animation ────────────────────────────────────────────────────────

        void AnimSelect()
        {
            StopRoutine();
            if (!s_Animating.Contains(this)) s_Animating.Add(this);
            _routine = StartCoroutine(CoIntroLoop());
        }

        void AnimDeselect()
        {
            StopRoutine();
            _routine = StartCoroutine(CoOutroHide());
        }

        IEnumerator CoIntroLoop()
        {
            yield return CoState(
                AnimState.Intro,
                _anim.introDuration,
                _anim.introWidthCurve,
                _anim.introBrightnessCurve);
            yield return CoLoop();
        }

        IEnumerator CoLoop()
        {
            CurrentState = AnimState.Loop;
            if (_anim.loopPeriod <= 0f) { SetVisuals(1f, 1f); yield break; }
            while (true)
            {
                for (float t = 0f; t < _anim.loopPeriod; t += Time.deltaTime)
                {
                    float n = t / _anim.loopPeriod;
                    SetVisuals(
                        _anim.loopWidthCurve.Evaluate(n),
                        _anim.loopBrightnessCurve.Evaluate(n));
                    yield return null;
                }
            }
        }

        IEnumerator CoOutroHide()
        {
            yield return CoState(
                AnimState.Outro,
                _anim.outroDuration,
                _anim.outroWidthCurve,
                _anim.outroBrightnessCurve);
            s_Animating.Remove(this);
            CurrentState = AnimState.Idle;
        }

        IEnumerator CoState(AnimState s, float duration,
                             AnimationCurve widthCurve, AnimationCurve brightCurve)
        {
            CurrentState = s;
            if (duration <= 0f)
            {
                SetVisuals(widthCurve.Evaluate(1f), brightCurve.Evaluate(1f));
                yield break;
            }
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float n = t / duration;
                SetVisuals(widthCurve.Evaluate(n), brightCurve.Evaluate(n));
                yield return null;
            }
            SetVisuals(widthCurve.Evaluate(1f), brightCurve.Evaluate(1f));
        }

        void SetVisuals(float widthT, float brightT)
        {
            if (_outlineMat == null) return;
            _outlineMat.SetFloat(ID_OutlineWidth, _baseWidth * widthT);
            _outlineMat.SetColor(ID_OutlineColor, new Color(
                _baseColor.r * brightT,
                _baseColor.g * brightT,
                _baseColor.b * brightT,
                _baseColor.a));
        }

        void StopRoutine()
        {
            if (_routine != null) { StopCoroutine(_routine); _routine = null; }
        }

        // ─── Helpers ──────────────────────────────────────────────────────────

        static uint LayerBit(SelectionType type) => 1u << (int)type;
    }
}
