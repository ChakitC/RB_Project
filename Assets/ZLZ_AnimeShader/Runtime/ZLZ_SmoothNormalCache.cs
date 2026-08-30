// Assets/ZLZ_AnimeShader/Runtime/ZLZ_SmoothNormalCache.cs
// Persists baked UV7 data so smooth normals survive FBX reimports and Unity restarts.
// Called only by ZLZ_SmoothNormalBaker — no UI or direct user interaction.
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ZLZ.AnimeShader
{
    [Serializable]
    class ZLZ_SmoothNormalEntry
    {
        public string   guid;
        public string   meshName;
        public Vector2[] data;
    }

    class ZLZ_SmoothNormalCache : ScriptableObject
    {
        const string PATH = "Assets/ZLZ_AnimeShader/Data/ZLZ_SmoothNormalCache.asset";

        public List<ZLZ_SmoothNormalEntry> entries = new List<ZLZ_SmoothNormalEntry>();

        // ── Load / create ─────────────────────────────────────────────────
        public static ZLZ_SmoothNormalCache Load()
            => AssetDatabase.LoadAssetAtPath<ZLZ_SmoothNormalCache>(PATH);

        public static ZLZ_SmoothNormalCache GetOrCreate()
        {
            var c = Load();
            if (c != null) return c;

            if (!AssetDatabase.IsValidFolder("Assets/ZLZ_AnimeShader/Data"))
                AssetDatabase.CreateFolder("Assets/ZLZ_AnimeShader", "Data");

            c = CreateInstance<ZLZ_SmoothNormalCache>();
            AssetDatabase.CreateAsset(c, PATH);
            return c;
        }

        // ── Write ─────────────────────────────────────────────────────────
        public static void Store(Mesh mesh, List<Vector2> uvData)
        {
            string guid = GetGuid(mesh);
            if (guid == null) return;

            var cache = GetOrCreate();
            var e = Find(cache, guid, mesh.name);
            if (e == null) { e = new ZLZ_SmoothNormalEntry { guid = guid, meshName = mesh.name }; cache.entries.Add(e); }
            e.data = uvData.ToArray();
            EditorUtility.SetDirty(cache);
        }

        public static void Remove(Mesh mesh)
        {
            string guid = GetGuid(mesh);
            if (guid == null) return;

            var cache = Load();
            if (cache == null) return;
            cache.entries.RemoveAll(e => e.guid == guid && e.meshName == mesh.name);
            EditorUtility.SetDirty(cache);
        }

        // ── Read / apply ──────────────────────────────────────────────────
        // Re-apply every cached entry to its mesh in memory (no save — FBX sub-assets
        // don't persist UV data, so we restore from cache on each domain reload instead).
        public static void ApplyAll()
        {
            var cache = Load();
            if (cache == null) return;

            foreach (var e in cache.entries)
            {
                if (string.IsNullOrEmpty(e.guid) || e.data == null) continue;
                string path = AssetDatabase.GUIDToAssetPath(e.guid);
                if (string.IsNullOrEmpty(path)) continue;

                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (asset is Mesh m && m.name == e.meshName)
                    {
                        m.SetUVs(ZLZ_SmoothNormalBaker.UV_CHANNEL, new List<Vector2>(e.data));
                        break;
                    }
                }
            }
        }

        // Also called from OnPostprocessModel (mesh reference comes from importer, not DB)
        public static void ApplyToMesh(string assetGuid, Mesh mesh)
        {
            var cache = Load();
            if (cache == null) return;
            var e = Find(cache, assetGuid, mesh.name);
            if (e?.data == null || e.data.Length == 0) return;
            mesh.SetUVs(ZLZ_SmoothNormalBaker.UV_CHANNEL, new List<Vector2>(e.data));
        }

        // ── Helpers ───────────────────────────────────────────────────────
        static string GetGuid(Mesh mesh)
        {
            string path = AssetDatabase.GetAssetPath(mesh);
            if (string.IsNullOrEmpty(path)) return null;
            string g = AssetDatabase.AssetPathToGUID(path);
            return string.IsNullOrEmpty(g) ? null : g;
        }

        static ZLZ_SmoothNormalEntry Find(ZLZ_SmoothNormalCache cache, string guid, string name)
            => cache.entries.Find(e => e.guid == guid && e.meshName == name);
    }

    // ── Restore UV7 on every domain reload / Unity startup ────────────────
    [InitializeOnLoad]
    static class ZLZ_SmoothNormalApplier
    {
        static ZLZ_SmoothNormalApplier()
        {
            // delayCall ensures AssetDatabase is fully loaded before we touch meshes
            EditorApplication.delayCall += ZLZ_SmoothNormalCache.ApplyAll;
        }
    }

    // ── Restore UV7 when any FBX is reimported ────────────────────────────
    // Running during import bakes UV7 into the artifact, so the mesh in the
    // artifact already has smooth normals — [InitializeOnLoad] is a safety net.
    class ZLZ_SmoothNormalPostprocessor : AssetPostprocessor
    {
        void OnPostprocessModel(GameObject go)
        {
            string guid = AssetDatabase.AssetPathToGUID(assetPath);
            if (string.IsNullOrEmpty(guid)) return;

            var cache = ZLZ_SmoothNormalCache.Load();
            if (cache == null) return;

            bool hit = false;
            foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (smr.sharedMesh != null) hit |= ApplyEntry(cache, guid, smr.sharedMesh);

            foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
                if (mf.sharedMesh != null) hit |= ApplyEntry(cache, guid, mf.sharedMesh);

            if (hit) Debug.Log($"[ZLZ] Smooth normals restored on reimport: {System.IO.Path.GetFileName(assetPath)}");
        }

        static bool ApplyEntry(ZLZ_SmoothNormalCache cache, string guid, Mesh mesh)
        {
            var e = cache.entries.Find(x => x.guid == guid && x.meshName == mesh.name);
            if (e?.data == null || e.data.Length == 0) return false;
            mesh.SetUVs(ZLZ_SmoothNormalBaker.UV_CHANNEL, new List<Vector2>(e.data));
            return true;
        }
    }
}
#endif
