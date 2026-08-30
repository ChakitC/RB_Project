using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ZLZ.AnimeShader
{
    // ─────────────────────────────────────────────────────────────────────
    // Per-character shader-property writer backed by INSTANCED materials
    // instead of a MaterialPropertyBlock.
    //
    // Why not MaterialPropertyBlock: in Unity 6 URP the per-renderer light-probe
    // SH (unity_SHAr..unity_SHC) and baked occlusion (unity_ProbesOcclusion) ride
    // the same per-renderer channel as an MPB. Calling SetPropertyBlock overwrites
    // that data, so the character loses ambient + baked main-light contribution and
    // renders dark/unlit (direction-only effects like Face Shadow still respond to
    // light rotation, which is the tell-tale symptom). A material instance carries
    // the override on the material itself, stays SRP-Batcher compatible, and never
    // touches the SH / occlusion channel.
    //
    // Built Play-only — instancing materials in edit mode would leak "(Instance)"
    // assets and dirty scenes. One instance per ZLZ_CharacterVFX, shared by every FX
    // block, Darken, the camera-fade anchor, and ZLZ_OcclusionFader. Eligibility is
    // checked per write via HasProperty so non-ZLZ materials parented under the
    // character (props, particles) are skipped.
    // ─────────────────────────────────────────────────────────────────────
    sealed class ZLZ_MaterialOverride
    {
        readonly List<Material> _mats = new List<Material>();

        public ZLZ_MaterialOverride(Renderer[] renderers)
        {
            if (renderers == null) return;
            foreach (var r in renderers)
            {
                if (r == null) continue;
                // Accessing .materials instantiates the shared materials and makes the
                // renderer use these instances from now on — capture them once here.
                var instances = r.materials;
                for (int i = 0; i < instances.Length; i++)
                    if (instances[i] != null) _mats.Add(instances[i]);
            }
        }

        public void SetFloat(int id, float value)
        {
            for (int i = 0; i < _mats.Count; i++)
            {
                var m = _mats[i];
                if (m != null && m.HasProperty(id)) m.SetFloat(id, value);
            }
        }

        public void SetVector(int id, Vector4 value)
        {
            for (int i = 0; i < _mats.Count; i++)
            {
                var m = _mats[i];
                if (m != null && m.HasProperty(id)) m.SetVector(id, value);
            }
        }
    }

    /// <summary>
    /// Single container for every per-character VFX feature that animates a
    /// shader property via a per-character material instance — Upgrade, GetHit, and
    /// any future effects share the same target renderer list and base machinery.
    /// (Selection Outline lives separately because it uses rendering layer
    /// mask + a URP Renderer Feature, not a per-character property animation.)
    /// </summary>
    [AddComponentMenu("ZLZ/Anime Shader/ZLZ_Character VFX")]
    public class ZLZ_CharacterVFX : MonoBehaviour
    {
        [Tooltip("Leave empty to auto-detect from children. Shared by every FX block.")]
        [SerializeField] Renderer[] _targetRenderers;

        /// <summary>
        /// Read-only access to the renderer set every FX block targets. Used by the
        /// Dashboard's Dither preview slider to write MPB directly in edit mode.
        /// </summary>
        public Renderer[] TargetRenderers => _targetRenderers;

        // Cached id — the shader reads this from CBUFFER to get a single per-character
        // anchor point for Dither Camera Near Fade. Set in LateUpdate so the whole
        // character fades uniformly instead of fragment-by-fragment.
        static readonly int s_CharacterAnchorId = Shader.PropertyToID("_CharacterAnchorWS");

        // Per-character material-instance overrides (anchor, camera fade, FX alphas,
        // darken local, occlusion). Built Play-only — null in edit mode.
        ZLZ_MaterialOverride _mats;

        // True while LateUpdate is actively writing camera-fade params, so we can clear
        // _DitherCameraEnable exactly once when camera fade (or all of Dither) turns off.
        // Without it a stale / baked _DitherCameraEnable would keep the character fading.
        bool _camFadeActive;

        public ZLZ_UpgradeFX   Upgrade   = new ZLZ_UpgradeFX();
        public ZLZ_GetHitFX    GetHit    = new ZLZ_GetHitFX();
        public ZLZ_IndicatorFX Indicator = new ZLZ_IndicatorFX();
        public ZLZ_DissolveFX  Dissolve  = new ZLZ_DissolveFX();
        public ZLZ_DitherFX    Dither    = new ZLZ_DitherFX();
        public ZLZ_DarkenFX    Darken    = new ZLZ_DarkenFX();

        void Awake()
        {
            if (_targetRenderers == null || _targetRenderers.Length == 0)
                _targetRenderers = GetComponentsInChildren<Renderer>(true);

            // Per-character overrides live on instanced materials (see ZLZ_MaterialOverride),
            // not a MaterialPropertyBlock. Awake only runs in Play, so this never
            // instantiates materials in edit mode.
            _mats = new ZLZ_MaterialOverride(_targetRenderers);

            Upgrade  .Init(this, _mats);
            GetHit   .Init(this, _mats);
            Indicator.Init(this, _mats);
            Dissolve .Init(this, _mats);
            Dither   .Init(this, _mats);
            Darken   .Init(_mats);

            // Normalize dither values on the fresh instances to a clean "off" baseline.
            // The dither path is always compiled now, so a non-zero dither value baked into
            // the source material (e.g. a leaked _DitherCameraEnable=1) would otherwise make
            // the character dither even when Dither is disabled.
            Dither.ResetRuntimeState(_mats);
        }

        void OnDisable()
        {
            Upgrade  .OnHostDisable();
            GetHit   .OnHostDisable();
            Indicator.OnHostDisable();
            Dissolve .OnHostDisable();
            Dither   .OnHostDisable();
            Darken   .OnHostDisable();
        }

        // Pushes the per-character Dither Camera Near Fade data — the world-space anchor
        // plus the enable / near / far params — onto this character's material instances.
        // Runs in LateUpdate so this frame's transform changes are reflected before the
        // next render.
        //
        // Per-character material instances (not the shared .mat) keep two characters that
        // share a source material independent: each gets its own anchor → each fades from
        // its own camera distance. The distance is measured from this single anchor point
        // so the whole character fades uniformly (not per-fragment, which would let the
        // camera see through the body). Instances also keep the shared .mat asset pristine
        // (no edit-time git noise) and — unlike a MaterialPropertyBlock — do NOT wipe the
        // renderer's SH / occlusion data (which rendered characters dark in Unity 6 URP).
        //
        // Gated to the Camera Near Fade path — the only consumer of this data.
        void LateUpdate()
        {
            if (_mats == null) return;                                   // Play-only

            bool active = Dither.Enabled && Dither.EnableCameraNearFade;
            if (active)
            {
                Vector4 anchor = transform.position;
                anchor.w = 1f;   // marker for "anchor was set" — w=0 means default/unset

                // One anchor for the whole character — every renderer of this character
                // shares it, so the entire body fades uniformly.
                _mats.SetVector(s_CharacterAnchorId, anchor);
                Dither.WriteCameraFadeParams(_mats);
            }
            else if (_camFadeActive)
            {
                // Camera fade just turned off — clear _DitherCameraEnable once so the
                // character stops camera-dithering (the dither path is always compiled,
                // so leaving it set would keep fading).
                Dither.ClearCameraFade(_mats);
            }
            _camFadeActive = active;
        }

        void OnDestroy()
        {
            Upgrade  .Cleanup();
            GetHit   .Cleanup();
            Indicator.Cleanup();
            Dissolve .Cleanup();
            Dither   .Cleanup();
            Darken   .Cleanup();
        }

        /// <summary>
        /// Re-scan child renderers and re-initialize every FX block.
        /// Character Dashboard's Refresh button calls this.
        /// </summary>
        public void RefreshRenderers()
        {
            _targetRenderers = GetComponentsInChildren<Renderer>(true);

            // Rebuild instanced-material overrides. Play-only — RefreshRenderers can be
            // invoked from the Dashboard, and instancing materials in edit mode would
            // leak "(Instance)" assets.
            if (!Application.isPlaying) return;

            _mats = new ZLZ_MaterialOverride(_targetRenderers);
            Upgrade  .Init(this, _mats);
            GetHit   .Init(this, _mats);
            Indicator.Init(this, _mats);
            Dissolve .Init(this, _mats);
            Dither   .Init(this, _mats);
            Darken   .Init(_mats);
            Dither.ResetRuntimeState(_mats);   // clean "off" baseline (see Awake)
        }

        /// <summary>
        /// Drives this character's _DitherOcclusionAlpha — called every frame by
        /// ZLZ_OcclusionFader while the character occludes the camera→player line.
        /// Writes onto the per-character material instances; no-op in edit mode.
        /// </summary>
        public void WriteOcclusionAlpha(float alpha)
        {
            _mats?.SetFloat(ZLZ_DitherFX.OcclusionAlphaShaderId, alpha);
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // Shared base — every FX block inherits this and only specifies the
    // shader property + how to extract its animation settings.
    // ─────────────────────────────────────────────────────────────────────

    public abstract class ZLZ_FXBlockBase
    {
        public enum AnimState { Idle, Intro, Loop, Outro }

        public AnimState CurrentState { get; private set; } = AnimState.Idle;
        public bool IsActive() => CurrentState != AnimState.Idle;

        // ── Subclass contract ─────────────────────────────────────────────
        protected abstract int                     ShaderPropertyId { get; }
        protected abstract ZLZ_FXAnimationSettings Anim             { get; }

        // Master gate for the "turn-on" drive methods (Activate / SetInstant /
        // PlayOutroOnly). Base FX blocks are always drivable; ZLZ_DitherFX overrides this
        // with its Enable toggle so a disabled Dither never dithers even if a script calls
        // Hide / Spawn / SetInstant directly — the dither shader path is always compiled
        // now (the _DITHER keyword was removed), so this C# gate is the hard-off.
        protected virtual bool CanDrive => true;

        protected static readonly ZLZ_FXAnimationSettings _fallback = new ZLZ_FXAnimationSettings();

        // ── Internal state ────────────────────────────────────────────────
        MonoBehaviour        _owner;
        ZLZ_MaterialOverride _mats;
        Coroutine            _routine;

        // ── Lifecycle (called by ZLZ_CharacterVFX) ────────────────────────
        // Shares the host's instanced-material override. Eligibility (which materials
        // expose ShaderPropertyId) is resolved per write via HasProperty, so no
        // per-block renderer filtering is needed here.
        internal void Init(MonoBehaviour owner, ZLZ_MaterialOverride mats)
        {
            _owner = owner;
            _mats  = mats;
        }

        internal void OnHostDisable()
        {
            if (CurrentState != AnimState.Idle)
            {
                StopRoutine();
                CurrentState = AnimState.Idle;
            }
            ResetProperty();
        }

        internal void Cleanup()
        {
            StopRoutine();
            ResetProperty();
        }

        // ── Public API ────────────────────────────────────────────────────
        /// <summary>Plays the effect: Intro → Loop → (auto-Outro when loopCount reaches limit).</summary>
        public void Activate()
        {
            if (_owner == null || !CanDrive) return;
            StopRoutine();
            _routine = _owner.StartCoroutine(CoIntroLoop());
        }

        /// <summary>Cancels the effect early with the Outro phase.</summary>
        public void Deactivate()
        {
            if (_owner == null || CurrentState == AnimState.Idle) return;
            StopRoutine();
            _routine = _owner.StartCoroutine(CoOutroHide());
        }

        /// <summary>
        /// Set the shader property to a specific value immediately, bypassing animation.
        /// Useful for pre-setting state — e.g., call <c>SetInstant(1f)</c> before
        /// <c>ZLZ_DissolveFX.Spawn()</c> so the character starts already invisible.
        /// </summary>
        public void SetInstant(float value)
        {
            if (!CanDrive) return;
            StopRoutine();
            CurrentState = AnimState.Idle;
            _mats?.SetFloat(ShaderPropertyId, value);
        }

        /// <summary>
        /// Plays only the Outro phase from its starting value, regardless of current state.
        /// Lets subclasses expose a "reverse" workflow (e.g., Dissolve.Spawn animates
        /// dissolved → visible without going through Intro/Loop first).
        /// </summary>
        protected void PlayOutroOnly()
        {
            if (_owner == null || !CanDrive) return;
            StopRoutine();
            _routine = _owner.StartCoroutine(CoOutroHide());
        }

        // ── Coroutines ────────────────────────────────────────────────────
        IEnumerator CoIntroLoop()
        {
            yield return CoPhase(AnimState.Intro, Anim.introDuration, Anim.introCurve);
            yield return CoLoop();
        }

        IEnumerator CoLoop()
        {
            CurrentState = AnimState.Loop;
            if (Anim.loopPeriod <= 0f) { SetActive(1f); yield break; }
            int count = 0;
            while (Anim.loopCount == 0 || count < Anim.loopCount)
            {
                for (float t = 0f; t < Anim.loopPeriod; t += Time.deltaTime)
                {
                    SetActive(Anim.loopCurve.Evaluate(t / Anim.loopPeriod));
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
            if (duration <= 0f) { SetActive(curve.Evaluate(1f)); yield break; }
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                SetActive(curve.Evaluate(t / duration));
                yield return null;
            }
            SetActive(curve.Evaluate(1f));
        }

        // ── Property writes (via per-character material instances) ─────────
        void SetActive(float value)
        {
            _mats?.SetFloat(ShaderPropertyId, value);
        }

        // Reset the animated property to its rest value (0 = inactive). With material
        // instances there is no property block to clear — we just write idle back.
        void ResetProperty()
        {
            _mats?.SetFloat(ShaderPropertyId, 0f);
        }

        void FinishToIdle()
        {
            SetActive(0f);   // writes the rest value (0) onto the material instances
            CurrentState = AnimState.Idle;
        }

        void StopRoutine()
        {
            if (_routine != null && _owner != null) _owner.StopCoroutine(_routine);
            _routine = null;
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // Concrete FX blocks — each just specifies its shader property + Settings.
    // Adding a new VFX feature in the future = copy this template (~15 lines).
    // ─────────────────────────────────────────────────────────────────────

    [System.Serializable]
    public class ZLZ_UpgradeFX : ZLZ_FXBlockBase
    {
        [SerializeField] ZLZ_UpgradeSettings _settings;

        static readonly int s_PropId = Shader.PropertyToID("_UpgradeActive");

        protected override int                     ShaderPropertyId => s_PropId;
        protected override ZLZ_FXAnimationSettings Anim             => _settings != null ? _settings.animation : _fallback;

        /// <summary>Activate if idle, deactivate if playing.</summary>
        public void ToggleUpgrade()
        {
            if (CurrentState == AnimState.Idle) Activate(); else Deactivate();
        }
    }

    [System.Serializable]
    public class ZLZ_GetHitFX : ZLZ_FXBlockBase
    {
        [SerializeField] ZLZ_GetHitSettings _settings;

        static readonly int s_PropId = Shader.PropertyToID("_GetHitStrength");

        protected override int                     ShaderPropertyId => s_PropId;
        protected override ZLZ_FXAnimationSettings Anim             => _settings != null ? _settings.animation : _fallback;

        /// <summary>Convenience alias for Activate — semantically clearer for hit reactions.</summary>
        public void Hit() => Activate();
    }

    [System.Serializable]
    public class ZLZ_IndicatorFX : ZLZ_FXBlockBase
    {
        [SerializeField] ZLZ_IndicatorSettings _settings;

        static readonly int s_PropId = Shader.PropertyToID("_IndicatorStrength");

        protected override int                     ShaderPropertyId => s_PropId;
        protected override ZLZ_FXAnimationSettings Anim             => _settings != null ? _settings.animation : _fallback;

        /// <summary>Activate if idle, deactivate if playing — useful for lock-on / aim toggle.</summary>
        public void ToggleIndicator()
        {
            if (CurrentState == AnimState.Idle) Activate(); else Deactivate();
        }
    }

    [System.Serializable]
    public class ZLZ_DissolveFX : ZLZ_FXBlockBase
    {
        [SerializeField] ZLZ_DissolveSettings _settings;

        static readonly int s_PropId = Shader.PropertyToID("_DissolveValue");

        protected override int                     ShaderPropertyId => s_PropId;
        protected override ZLZ_FXAnimationSettings Anim             => _settings != null ? _settings.animation : _fallback;

        /// <summary>Plays the dissolve-out animation (Intro: 0 → 1, then holds).</summary>
        public void Dissolve() => Activate();

        /// <summary>Plays the restore animation (Outro: 1 → 0, back to visible).</summary>
        public void Restore() => Deactivate();

        /// <summary>
        /// Plays the spawn-in animation: animates from dissolved (1) to visible (0)
        /// using the Outro curve and duration. Call <c>SetInstant(1f)</c> beforehand
        /// (or set the material's _DissolveValue to 1 in the Inspector) if you want
        /// the character to start fully invisible before this is triggered.
        /// </summary>
        public void Spawn() => PlayOutroOnly();
    }

    [System.Serializable]
    public class ZLZ_DitherFX : ZLZ_FXBlockBase
    {
        // ── Component-level configuration (replaces per-material setup) ──────
        [SerializeField] bool          _enabled              = false;
        [SerializeField] bool          _enableCameraNearFade = false;
        [SerializeField, Range(0f, 20f)] float _cameraNearDistance = 1f;
        [SerializeField, Range(0f, 20f)] float _cameraFarDistance  = 1.25f;
        [SerializeField] bool               _receiveOcclusionFade = false;
        [SerializeField] ZLZ_OcclusionLevel _occlusionLevel       = ZLZ_OcclusionLevel.Soft;
        [SerializeField] ZLZ_DitherSettings _settings;

        // ── Cached shader IDs / keywords ─────────────────────────────────────
        static readonly int s_PropId               = Shader.PropertyToID("_DitherAlpha");
        static readonly int s_CameraEnableId       = Shader.PropertyToID("_DitherCameraEnable");
        static readonly int s_CameraNearId         = Shader.PropertyToID("_DitherCameraNear");
        static readonly int s_CameraFarId          = Shader.PropertyToID("_DitherCameraFar");
        static readonly int s_OcclusionAlphaId     = Shader.PropertyToID("_DitherOcclusionAlpha");

        protected override int                     ShaderPropertyId => s_PropId;
        protected override ZLZ_FXAnimationSettings Anim             => _settings != null ? _settings.animation : _fallback;

        // Disabled Dither is a true hard-off: the dither shader path is always compiled
        // (the _DITHER keyword was removed), but with Enable off no drive method may turn
        // dither on, and the automatic systems (camera-near fade, occlusion) gate on it too.
        protected override bool CanDrive => _enabled;

        // ── Public accessors ─────────────────────────────────────────────────
        public bool          Enabled                => _enabled;
        public bool                EnableCameraNearFade => _enableCameraNearFade;
        public float               CameraNearDistance   => _cameraNearDistance;
        public float               CameraFarDistance    => _cameraFarDistance;
        public bool                ReceiveOcclusionFade => _receiveOcclusionFade;
        public ZLZ_OcclusionLevel  OcclusionLevel       => _occlusionLevel;
        public ZLZ_DitherSettings  SettingsAsset        => _settings;

        /// <summary>Plays the fade-out animation (Intro: 0 → 1 = invisible, then holds).</summary>
        public void Hide() => Activate();

        /// <summary>Plays the fade-in animation (Outro: 1 → 0 = visible).</summary>
        public void Show() => Deactivate();

        /// <summary>
        /// Plays the spawn-in animation: animates from invisible (1) to visible (0)
        /// using the Outro curve and duration. Call <c>SetInstant(1f)</c> beforehand
        /// if you want the character to start fully invisible before this is triggered.
        /// </summary>
        public void Spawn() => PlayOutroOnly();

        // Writes the Camera Near Fade params onto the character's material instances so
        // they live per-character instead of on the shared material asset. Called by
        // ZLZ_CharacterVFX.LateUpdate while camera fade is enabled. Camera fade only runs
        // in Play mode (suppressed in the editor via _ZLZ_RuntimeActive), so these never
        // need to be baked into the .mat.
        internal void WriteCameraFadeParams(ZLZ_MaterialOverride mats)
        {
            mats.SetFloat(s_CameraEnableId, _enableCameraNearFade ? 1f : 0f);
            mats.SetFloat(s_CameraNearId,   _cameraNearDistance);
            mats.SetFloat(s_CameraFarId,    _cameraFarDistance);
        }

        // Clears the camera-near-fade enable flag on the instances. Called when camera
        // fade (or all of Dither) turns off so a stale / baked _DitherCameraEnable=1 can't
        // keep the character fading — the dither path is always compiled.
        internal void ClearCameraFade(ZLZ_MaterialOverride mats)
        {
            mats.SetFloat(s_CameraEnableId, 0f);
        }

        // Resets every per-character dither value to its "off" baseline on the freshly
        // instanced materials. Guards against non-zero dither values baked into a source
        // material (the dither shader path is always compiled, so such values would
        // otherwise dither the character even when Dither is disabled).
        internal void ResetRuntimeState(ZLZ_MaterialOverride mats)
        {
            mats.SetFloat(s_PropId,           0f);   // _DitherAlpha (manual Hide/Show)
            mats.SetFloat(s_OcclusionAlphaId, 0f);   // _DitherOcclusionAlpha
            mats.SetFloat(s_CameraEnableId,   0f);   // _DitherCameraEnable
        }

        // ── Editor-time preview (works without Awake/Init) ────────────────────
        //
        // Writes _DitherAlpha via a MaterialPropertyBlock so users can drag a slider in
        // edit mode and see the dither pattern update live in Scene view. This is the ONE
        // place that intentionally still uses an MPB instead of a material instance: it
        // runs in EDIT mode, where instancing materials would leak "(Instance)" assets and
        // dirty the scene. The Unity 6 "MPB wipes SH → character renders dark" problem only
        // applies in Play, so a transient, immediately-cleared edit-mode MPB here is safe.
        //
        // Independent from the runtime state machine — does NOT touch CurrentState or any
        // coroutine. Use ClearEditorPreview to restore.

        /// <summary>Live preview: set _DitherAlpha on every ZLZ-Dither material via MPB (edit mode only).</summary>
        public static void SetEditorPreviewAlpha(Renderer[] renderers, float alpha)
        {
            if (renderers == null) return;
            var mpb = new MaterialPropertyBlock();
            foreach (var r in renderers)
            {
                if (r == null) continue;
                var mats = r.sharedMaterials;
                bool eligible = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] != null && mats[i].HasProperty(s_PropId)) { eligible = true; break; }
                }
                if (!eligible) continue;

                r.GetPropertyBlock(mpb);
                mpb.SetFloat(s_PropId, alpha);
                r.SetPropertyBlock(mpb);
            }
        }

        /// <summary>Clears the editor-preview MPB override on all renderers.</summary>
        public static void ClearEditorPreview(Renderer[] renderers)
        {
            if (renderers == null) return;
            foreach (var r in renderers)
                if (r != null) r.SetPropertyBlock(null);
        }

        // ── Occlusion alpha hook (called by ZLZ_OcclusionFader) ───────────────
        //
        // _DitherOcclusionAlpha is written onto the character's material instances by
        // ZLZ_CharacterVFX.WriteOcclusionAlpha. The shader combines it with _DitherAlpha
        // (animation) and Camera Near Fade using max(), so OcclusionFade and Hide/Show
        // animations layer without conflict.

        /// <summary>Shader property ID for the per-character occlusion alpha (driven by ZLZ_OcclusionFader).</summary>
        public static int OcclusionAlphaShaderId => s_OcclusionAlphaId;
    }

    // ─────────────────────────────────────────────────────────────────────
    // Darken (per-character) — NOT an FX block because the semantics differ:
    // material default for _TargetDarkenLocal is 1 (follows global). Exclude pushes
    // local=0 on the character's material instances; Include writes 1 back so the
    // character follows the scene-wide darken again. Animation is handled at the scene
    // level by ZLZ_DarkenManager (animates _TargetDarkenGlobal).
    // ─────────────────────────────────────────────────────────────────────

    [System.Serializable]
    public class ZLZ_DarkenFX
    {
        static readonly int s_PropId = Shader.PropertyToID("_TargetDarkenLocal");

        [SerializeField, HideInInspector] bool _excluded;

        /// <summary>True if this character is opting out of the scene-wide darken.</summary>
        public bool IsExcluded => _excluded;

        ZLZ_MaterialOverride _mats;

        internal void Init(ZLZ_MaterialOverride mats)
        {
            _mats = mats;
            // Re-apply persisted exclude state (e.g., after Refresh or scene load).
            if (_excluded) _mats?.SetFloat(s_PropId, 0f);
        }

        internal void OnHostDisable() { Follow(); }
        internal void Cleanup()       { Follow(); }

        /// <summary>Stay bright when global darken activates (local = 0).</summary>
        public void Exclude()
        {
            _excluded = true;
            _mats?.SetFloat(s_PropId, 0f);
        }

        /// <summary>Follow the scene-wide darken again (local back to material default 1).</summary>
        public void Include()
        {
            _excluded = false;
            Follow();
        }

        public void SetExcluded(bool excluded)
        {
            if (excluded) Exclude(); else Include();
        }

        // Restore _TargetDarkenLocal to the material default (1 = follow global darken).
        void Follow() => _mats?.SetFloat(s_PropId, 1f);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Reusable Intro / Loop / Outro animation block.
    // ─────────────────────────────────────────────────────────────────────

#if UNITY_EDITOR
    // ─────────────────────────────────────────────────────────────────────
    // ZLZ_DitherFX deliberately has NO custom drawer — it inherits the one-line
    // ZLZ_FXBlockBaseDrawer below, so the component shows only its Settings asset
    // reference (same pattern as Upgrade / GetHit / Indicator / Dissolve). All the
    // per-character Dither config (Enabled, Camera Near Fade, Receive Occlusion) is
    // edited in ONE place: the Character Dashboard's Dither section. Darken is the
    // sole exception — it keeps its own drawer for a reason (see ZLZ_DarkenFXDrawer).
    // ─────────────────────────────────────────────────────────────────────
    // Inline every FX block as a single line — each block has exactly one user-facing
    // field (Settings asset), so a foldout would only waste a click. The label is the
    // field name in ZLZ_CharacterVFX (e.g., "Upgrade", "GetHit").
    [CustomPropertyDrawer(typeof(ZLZ_FXBlockBase), useForChildren: true)]
    public class ZLZ_FXBlockBaseDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var settingsProp = property.FindPropertyRelative("_settings");
            if (settingsProp != null)
                EditorGUI.PropertyField(position, settingsProp, label);
            else
                EditorGUI.LabelField(position, label, new GUIContent("(no settings field)"));
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
            => EditorGUIUtility.singleLineHeight;
    }

    // Darken doesn't have its own Settings asset (animation lives on the scene Manager).
    // The drawer mimics other FX rows: [label] [Manager-reference field] [Exclude toggle]
    // so users can see at a glance where the animation settings come from, and click
    // the Manager field to ping the scene object.
    [CustomPropertyDrawer(typeof(ZLZ_DarkenFX))]
    public class ZLZ_DarkenFXDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var excludedProp = property.FindPropertyRelative("_excluded");
            if (excludedProp == null) { EditorGUI.LabelField(position, label, new GUIContent("(invalid)")); return; }

            EditorGUI.BeginProperty(position, label, property);

            const float toggleWidth = 74f;
            const float spacing     = 4f;

            var labelRect   = new Rect(position.x, position.y, EditorGUIUtility.labelWidth, position.height);
            var managerRect = new Rect(position.x + EditorGUIUtility.labelWidth, position.y,
                                       position.width - EditorGUIUtility.labelWidth - toggleWidth - spacing, position.height);
            var toggleRect  = new Rect(managerRect.xMax + spacing, position.y, toggleWidth, position.height);

            EditorGUI.LabelField(labelRect, label);

            // Read-only Manager reference (click to ping in Hierarchy).
#if UNITY_2022_2_OR_NEWER
            var manager = Object.FindFirstObjectByType<ZLZ_DarkenManager>(FindObjectsInactive.Include);
#else
            var manager = Object.FindObjectOfType<ZLZ_DarkenManager>(true);
#endif
            bool wasEnabled = GUI.enabled;
            GUI.enabled = false;
            EditorGUI.ObjectField(managerRect, manager, typeof(ZLZ_DarkenManager), allowSceneObjects: true);
            GUI.enabled = wasEnabled;

            EditorGUI.BeginChangeCheck();
            bool next = GUI.Toggle(toggleRect, excludedProp.boolValue,
                new GUIContent(" Exclude", "Stay bright when global darken activates."));
            if (EditorGUI.EndChangeCheck())
            {
                excludedProp.boolValue = next;
                // Apply immediately at runtime so MPB updates without waiting for re-Init.
                if (Application.isPlaying &&
                    property.serializedObject.targetObject is ZLZ_CharacterVFX vfx)
                    vfx.Darken.SetExcluded(next);
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
            => EditorGUIUtility.singleLineHeight;
    }
#endif

    [System.Serializable]
    public class ZLZ_FXAnimationSettings
    {
        [Header("Loop Count  |  0 = Infinite   ·   1+ = Auto Outro after N cycles")]
        [Min(0)] public int             loopCount     = 0;

        [Space(4)]
        [Header("Intro")]
        [Min(0f)] public float          introDuration = 0.4f;
        [Tooltip("Active level over intro (0 = off → 1 = full)")]
        public AnimationCurve           introCurve    = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Loop")]
        [Tooltip("Pulse period in seconds. Set to 0 for a steady glow with no pulse.")]
        [Min(0f)] public float          loopPeriod    = 1.5f;
        [Tooltip("Active level over one pulse cycle")]
        public AnimationCurve           loopCurve     = new AnimationCurve(
            new Keyframe(0f,   1f,    0f, 0f),
            new Keyframe(0.5f, 0.75f, 0f, 0f),
            new Keyframe(1f,   1f,    0f, 0f));

        [Header("Outro")]
        [Min(0f)] public float          outroDuration = 0.4f;
        [Tooltip("Active level over outro (1 = full → 0 = off)")]
        public AnimationCurve           outroCurve    = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
    }
}
