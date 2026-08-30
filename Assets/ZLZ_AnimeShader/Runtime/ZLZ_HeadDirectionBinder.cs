// Assets/ZLZ_AnimeShader/Runtime/ZLZ_HeadDirectionBinder.cs
// Runtime only — UI and headBone are managed by ZLZ_CharacterDashboard.
// Do NOT add a Custom Inspector here.
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ZLZ.AnimeShader
{
    [ExecuteAlways]
    public class ZLZ_HeadDirectionBinder : MonoBehaviour
    {
        public enum HeadForwardAxis { Z_Positive, Z_Negative, X_Positive, X_Negative, Y_Positive, Y_Negative }
        public enum HeadRightAxis   { X_Positive, X_Negative, Z_Positive, Z_Negative, Y_Positive, Y_Negative }
        public enum EditorPreviewMode { LivePreviewToMaterial, Disabled }

        // headBone is set by ZLZ_CharacterDashboard
        [HideInInspector] public Transform        headBone;
        [HideInInspector] public HeadForwardAxis  forwardAxis = HeadForwardAxis.Z_Positive;
        [HideInInspector] public HeadRightAxis    rightAxis   = HeadRightAxis.X_Positive;
        [HideInInspector] public bool             axesCalibrated = false;
        [HideInInspector] public EditorPreviewMode editorPreviewMode = EditorPreviewMode.LivePreviewToMaterial;

        static readonly int ID_Center  = Shader.PropertyToID("_HeadCenterWS");
        static readonly int ID_Forward = Shader.PropertyToID("_HeadForwardWS");
        static readonly int ID_Right   = Shader.PropertyToID("_HeadRightWS");

        Renderer[] _renderers;
        // Flat cache of every target material. In Play mode r.materials instantiates per-renderer
        // copies AND allocates a fresh array on every access — resolving it once here keeps
        // LateUpdate allocation-free. Rebuilt only when the renderer set is (re)resolved.
        Material[] _materials;

        // Dirty-check state: skip the per-material writes when the head bone hasn't moved
        // (idle/standing NPCs, paused game). Only enforced in Play mode.
        bool    _hasLastApplied;
        Vector4 _lastCenter, _lastForward, _lastRight;

        // ==================================================================
        //  Auto-Detect Axes  (called from Dashboard)
        // ==================================================================
        public void AutoDetectAxes()
        {
            if (headBone == null)
            {
                Debug.LogWarning("[ZLZ] AutoDetect: headBone is not assigned.", this);
                return;
            }
            forwardAxis    = BestForwardAxis(transform.forward);
            rightAxis      = BestRightAxis(transform.right);
            axesCalibrated = true;
            Debug.Log($"[ZLZ] AutoDetect \u2192 Forward={forwardAxis}, Right={rightAxis}", this);
#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
#endif
            if (editorPreviewMode == EditorPreviewMode.LivePreviewToMaterial)
                ApplyHeadData();
        }

        HeadForwardAxis BestForwardAxis(Vector3 target)
        {
            var c = new (HeadForwardAxis axis, Vector3 ws)[]
            {
                (HeadForwardAxis.Z_Positive,  headBone.forward),
                (HeadForwardAxis.Z_Negative, -headBone.forward),
                (HeadForwardAxis.X_Positive,  headBone.right),
                (HeadForwardAxis.X_Negative, -headBone.right),
                (HeadForwardAxis.Y_Positive,  headBone.up),
                (HeadForwardAxis.Y_Negative, -headBone.up),
            };
            return BestMatch(c, target).axis;
        }

        HeadRightAxis BestRightAxis(Vector3 target)
        {
            var c = new (HeadRightAxis axis, Vector3 ws)[]
            {
                (HeadRightAxis.X_Positive,  headBone.right),
                (HeadRightAxis.X_Negative, -headBone.right),
                (HeadRightAxis.Z_Positive,  headBone.forward),
                (HeadRightAxis.Z_Negative, -headBone.forward),
                (HeadRightAxis.Y_Positive,  headBone.up),
                (HeadRightAxis.Y_Negative, -headBone.up),
            };
            return BestMatch(c, target).axis;
        }

        static (T axis, Vector3 ws) BestMatch<T>((T axis, Vector3 ws)[] candidates, Vector3 target)
        {
            int bestIdx = 0; float bestDot = float.NegativeInfinity;
            for (int i = 0; i < candidates.Length; i++)
            {
                float d = Mathf.Clamp(Vector3.Dot(candidates[i].ws.normalized, target.normalized), -1f, 1f);
                if (d > bestDot) { bestDot = d; bestIdx = i; }
            }
            return candidates[bestIdx];
        }

        // ==================================================================
        //  Direction helpers
        // ==================================================================
        public Vector3 GetForwardWS()
        {
            if (headBone == null) return Vector3.forward;
            switch (forwardAxis)
            {
                default:
                case HeadForwardAxis.Z_Positive: return  headBone.forward;
                case HeadForwardAxis.Z_Negative: return -headBone.forward;
                case HeadForwardAxis.X_Positive: return  headBone.right;
                case HeadForwardAxis.X_Negative: return -headBone.right;
                case HeadForwardAxis.Y_Positive: return  headBone.up;
                case HeadForwardAxis.Y_Negative: return -headBone.up;
            }
        }

        public Vector3 GetRightWS()
        {
            if (headBone == null) return Vector3.right;
            switch (rightAxis)
            {
                default:
                case HeadRightAxis.X_Positive: return  headBone.right;
                case HeadRightAxis.X_Negative: return -headBone.right;
                case HeadRightAxis.Z_Positive: return  headBone.forward;
                case HeadRightAxis.Z_Negative: return -headBone.forward;
                case HeadRightAxis.Y_Positive: return  headBone.up;
                case HeadRightAxis.Y_Negative: return -headBone.up;
            }
        }

        // ==================================================================
        //  Gizmo
        // ==================================================================
#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            if (!this || headBone == null) return;
            try
            {
                const float len = 0.12f;
                Vector3 origin = headBone.position;
                Gizmos.color = new Color(0.2f, 1f, 0.3f, 0.9f);
                Gizmos.DrawLine(origin, origin + GetForwardWS() * len);
                Gizmos.DrawSphere(origin + GetForwardWS() * len, 0.004f);
                Gizmos.color = new Color(1f, 0.25f, 0.25f, 0.9f);
                Gizmos.DrawLine(origin, origin + GetRightWS() * len);
                Gizmos.DrawSphere(origin + GetRightWS() * len, 0.004f);
            }
            catch { }
        }
#endif

        // ==================================================================
        //  Lifecycle
        // ==================================================================
        void OnEnable()
        {
            _renderers      = null;
            _materials      = null;
            _hasLastApplied = false;
            if (!Application.isPlaying && editorPreviewMode == EditorPreviewMode.Disabled) return;
            ApplyHeadData();
        }

        void OnDisable() => ClearHeadData();
        void LateUpdate() => ApplyHeadData();

        // ==================================================================
        //  Apply / Clear
        // ==================================================================
        void ApplyHeadData()
        {
            if (headBone == null) return;
            if (!Application.isPlaying && editorPreviewMode == EditorPreviewMode.Disabled) return;

            EnsureRenderers();
            if (_renderers == null) return;

            Vector4 center  = new Vector4(headBone.position.x, headBone.position.y, headBone.position.z, 1f);
            Vector3 fwdWS   = GetForwardWS();
            Vector3 rightWS = GetRightWS();
            Vector4 forward = new Vector4(fwdWS.x,   fwdWS.y,   fwdWS.z,   0f);
            Vector4 right   = new Vector4(rightWS.x, rightWS.y, rightWS.z, 0f);

            if (Application.isPlaying)
            {
                // Runtime hot path: write to the cached instanced materials (resolved once, no
                // per-frame r.materials alloc) and skip entirely when the head bone hasn't moved.
                if (_materials == null) RebuildMaterialCache();

                bool moved = !_hasLastApplied || center != _lastCenter || forward != _lastForward || right != _lastRight;
                if (!moved) return;
                _lastCenter = center; _lastForward = forward; _lastRight = right; _hasLastApplied = true;

                for (int i = 0; i < _materials.Length; i++)
                {
                    Material mat = _materials[i];
                    if (mat == null) continue;
                    mat.SetVector(ID_Center,  center);
                    mat.SetVector(ID_Forward, forward);
                    mat.SetVector(ID_Right,   right);
                }
            }
            else
            {
                // Editor preview: re-read sharedMaterials every frame so material swaps / axis
                // tweaks show live. Not perf-critical (editor only), so no caching or dirty-check.
                foreach (Renderer r in _renderers)
                {
                    if (r == null) continue;
                    Material[] mats = r.sharedMaterials;
                    if (mats == null) continue;
                    foreach (Material mat in mats)
                    {
                        if (mat == null) continue;
                        mat.SetVector(ID_Center,  center);
                        mat.SetVector(ID_Forward, forward);
                        mat.SetVector(ID_Right,   right);
                    }
                }
            }
        }

        void ClearHeadData()
        {
            if (Application.isPlaying)
            {
                if (_materials == null) return;
                for (int i = 0; i < _materials.Length; i++)
                {
                    Material mat = _materials[i];
                    if (mat == null) continue;
                    try { mat.SetVector(ID_Center, Vector4.zero); mat.SetVector(ID_Forward, Vector4.zero); mat.SetVector(ID_Right, Vector4.zero); } catch { }
                }
            }
            else
            {
                if (_renderers == null) return;
                foreach (Renderer r in _renderers)
                {
                    if (r == null) continue;
                    Material[] mats = r.sharedMaterials;
                    if (mats == null) continue;
                    foreach (Material mat in mats)
                    {
                        if (mat == null) continue;
                        try { mat.SetVector(ID_Center, Vector4.zero); mat.SetVector(ID_Forward, Vector4.zero); mat.SetVector(ID_Right, Vector4.zero); } catch { }
                    }
                }
            }
            _hasLastApplied = false;
        }

        void EnsureRenderers()
        {
            if (_renderers != null && _renderers.Length > 0) return;
            var dashboard = GetComponentInParent<ZLZ_CharacterDashboard>();
            GameObject root = dashboard != null ? dashboard.gameObject : transform.root.gameObject;
            _renderers = root.GetComponentsInChildren<Renderer>(true);
            _materials = null;   // invalidate the play-mode cache so it rebuilds against the new set
        }

        // Play-mode only: resolve the flat material list once. The single r.materials access here is
        // what instantiates the per-renderer copies — the same instances ZLZ_CharacterVFX writes to.
        void RebuildMaterialCache()
        {
            var list = new List<Material>();
            if (_renderers != null)
            {
                foreach (Renderer r in _renderers)
                {
                    if (r == null) continue;
                    Material[] mats = r.materials;
                    if (mats == null) continue;
                    foreach (Material mat in mats)
                        if (mat != null) list.Add(mat);
                }
            }
            _materials      = list.ToArray();
            _hasLastApplied = false;   // force a re-apply onto the freshly resolved material set
        }
    }
}