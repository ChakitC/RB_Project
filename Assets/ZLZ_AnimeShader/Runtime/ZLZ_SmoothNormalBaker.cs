// Assets/ZLZ_AnimeShader/Runtime/ZLZ_SmoothNormalBaker.cs
// Pure bake logic — no MenuItem, no Editor UI.
// Called exclusively by ZLZ_CharacterDashboard.
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ZLZ.AnimeShader
{
    public static class ZLZ_SmoothNormalBaker
    {
        // UV channel that stores smooth normals (must match shader)
        public const int UV_CHANNEL = 7;

        // ---------------------------------------------------------------
        // Bake — compute smooth normals and store in UV_CHANNEL
        // ---------------------------------------------------------------
        public static void BakeMesh(Mesh mesh)
        {
            Vector3[] verts = mesh.vertices;
            Vector3[] norms = mesh.normals;
            Vector4[] tangs = mesh.tangents;

            if (norms == null || norms.Length == 0)
            {
                Debug.LogWarning($"[ZLZ] {mesh.name} has no normals — skipped");
                return;
            }
            if (tangs == null || tangs.Length == 0)
            {
                Debug.LogWarning($"[ZLZ] {mesh.name} has no tangents — skipped. Enable Read/Write in model import settings.");
                return;
            }

            mesh.RecalculateNormals();
            norms = mesh.normals;

            // 1. Group vertices sharing the same position
            var posToNorms = new Dictionary<Vector3, List<Vector3>>();
            for (int i = 0; i < verts.Length; i++)
            {
                Vector3 pos = RoundVector(verts[i]);
                if (!posToNorms.ContainsKey(pos)) posToNorms[pos] = new List<Vector3>();
                posToNorms[pos].Add(norms[i]);
            }

            // 2. Average normals per group
            var posToSmooth = new Dictionary<Vector3, Vector3>();
            foreach (var kv in posToNorms)
            {
                Vector3 avg = Vector3.zero;
                foreach (var n in kv.Value) avg += n;
                posToSmooth[kv.Key] = avg.normalized;
            }

            // 3. Transform to tangent space, store XY in UV
            var uvData = new List<Vector2>(verts.Length);
            for (int i = 0; i < verts.Length; i++)
            {
                Vector3 sn = posToSmooth[RoundVector(verts[i])];
                Vector3 n  = norms[i].normalized;
                Vector3 t  = ((Vector3)tangs[i]).normalized;
                Vector3 b  = Vector3.Cross(n, t) * tangs[i].w;
                uvData.Add(new Vector2(Vector3.Dot(sn, t), Vector3.Dot(sn, b)));
            }

            mesh.SetUVs(UV_CHANNEL, uvData);
            ZLZ_SmoothNormalCache.Store(mesh, uvData);
            EditorUtility.SetDirty(mesh);
            Debug.Log($"[ZLZ] Baked: {mesh.name}");
        }

        // ---------------------------------------------------------------
        // Reset — clear UV_CHANNEL back to zero
        // ---------------------------------------------------------------
        public static void ResetMesh(Mesh mesh)
        {
            var empty = new List<Vector2>(mesh.vertexCount);
            for (int i = 0; i < mesh.vertexCount; i++) empty.Add(Vector2.zero);
            mesh.SetUVs(UV_CHANNEL, empty);
            ZLZ_SmoothNormalCache.Remove(mesh);
            EditorUtility.SetDirty(mesh);
            Debug.Log($"[ZLZ] Reset: {mesh.name}");
        }

        // ---------------------------------------------------------------
        // IsMeshBaked — ground-truth check directly from UV data
        // ---------------------------------------------------------------
        public static bool IsMeshBaked(Mesh mesh)
        {
            var uvs = new List<Vector2>();
            mesh.GetUVs(UV_CHANNEL, uvs);
            if (uvs.Count == 0) return false;
            foreach (var uv in uvs)
                if (uv.sqrMagnitude > 0.0001f) return true;
            return false;
        }

        // ---------------------------------------------------------------
        // SyncMaterialFlag — auto enable/disable _OUTLINESMOOTHNORMAL on all ZLZ materials
        // ---------------------------------------------------------------
        public static void SyncMaterialFlag(GameObject root, bool baked)
        {
            var seen = new HashSet<int>();
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            {
                foreach (Material mat in r.sharedMaterials)
                {
                    if (mat == null || !seen.Add(mat.GetInstanceID())) continue;
                    if (!mat.HasProperty("_OutlineSmoothNormal")) continue;

                    mat.SetFloat("_OutlineSmoothNormal", baked ? 1f : 0f);
                    if (baked)
                    {
                        mat.EnableKeyword("_OUTLINESMOOTHNORMAL_ON");
                        mat.DisableKeyword("_OUTLINESMOOTHNORMAL_OFF");
                    }
                    else
                    {
                        mat.DisableKeyword("_OUTLINESMOOTHNORMAL_ON");
                        mat.EnableKeyword("_OUTLINESMOOTHNORMAL_OFF");
                    }
                    EditorUtility.SetDirty(mat);
                }
            }
        }

        // ---------------------------------------------------------------
        // Helper
        // ---------------------------------------------------------------
        static Vector3 RoundVector(Vector3 v)
        {
            const float m = 10000f;
            return new Vector3(
                Mathf.Round(v.x * m) / m,
                Mathf.Round(v.y * m) / m,
                Mathf.Round(v.z * m) / m);
        }
    }
}
#endif