using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ZLZ.Hub
{
    // ─────────────────────────────────────────────────────────────────────────
    //  Every URP read/write the Hub does goes through here, so the renderer
    //  matrix, the checklist and the bulk buttons can never drift apart.
    //
    //  The install path mirrors ZLZ_CharacterDashboard.SO_SetupAll() exactly
    //  (create instance -> AddObjectToAsset -> append to m_RendererFeatures).
    //  That code is already shipping and proven; a second, cleverer way of
    //  doing the same thing in the same package is how renderer assets get
    //  corrupted.
    // ─────────────────────────────────────────────────────────────────────────
    internal class ZLZ_RendererEntry
    {
        public UniversalRendererData Data;
        public string                Path;
        public string                ShortName;
        public bool                  IsActive;        // the renderer the project renders with right now
        public bool                  IsRecommended;   // a camera actually renders the game through this one
    }

    internal static class ZLZ_HubUrp
    {
        // ── Scanning ──────────────────────────────────────────────────────────

        // Scans the whole project, not Assets/Settings. Customers routinely keep
        // their pipeline assets somewhere else, and a Hub that silently misses
        // them reports "all set" on a project that is not set up at all.
        public static List<ZLZ_RendererEntry> ScanRenderers()
        {
            var active      = GetActiveRendererData();
            var recommended = DefaultRenderersInUse();
            var list        = new List<ZLZ_RendererEntry>();

            foreach (var guid in AssetDatabase.FindAssets("t:UniversalRendererData"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);

                // URP ships a template UniversalRendererData inside its own package
                // folder, and FindAssets returns it like any other. Packages are
                // read-only : offering it as an install target hands the customer a
                // checkbox that can only ever fail.
                if (!path.StartsWith("Assets/", StringComparison.Ordinal)) continue;

                var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
                if (data == null) continue;

                list.Add(new ZLZ_RendererEntry
                {
                    Data          = data,
                    Path          = path,
                    ShortName     = ShortenRendererName(System.IO.Path.GetFileNameWithoutExtension(path)),
                    IsActive      = data == active,
                    IsRecommended = recommended.Count == 0 || recommended.Contains(data),
                });
            }

            // Active first, then alphabetical - the column the customer cares about
            // most should not move around as they add renderers.
            return list.OrderByDescending(e => e.IsActive).ThenBy(e => e.ShortName).ToList();
        }

        public static List<UniversalRenderPipelineAsset> ScanPipelineAssets()
        {
            var list = new List<UniversalRenderPipelineAsset>();
            foreach (var guid in AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.StartsWith("Assets/", StringComparison.Ordinal)) continue;   // read-only package asset

                var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
                if (asset != null) list.Add(asset);
            }
            return list;
        }

        // Which renderers a camera actually renders the game through.
        //
        // A URP asset's renderer list holds more than the one it renders with : ZLZ's
        // own planar reflection and grass features each point a private camera at a
        // renderer further down that list. Installing character features into those
        // costs fill rate on a target that never shows a character, so only the
        // DEFAULT renderer of each pipeline asset in use counts as a real target.
        //
        // Returning an empty set means "could not tell" - the caller then treats
        // every renderer as recommended rather than recommending nothing.
        static HashSet<UniversalRendererData> DefaultRenderersInUse()
        {
            var set = new HashSet<UniversalRendererData>();

            AddDefaultRenderer(set, GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset);
            AddDefaultRenderer(set, GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset);

            // Every quality level can carry its own pipeline asset, and a project that
            // ships three tiers wants all three set up, not just the one selected in
            // the editor right now.
            try
            {
                int levels = QualitySettings.names?.Length ?? 0;
                for (int i = 0; i < levels; i++)
                    AddDefaultRenderer(set, QualitySettings.GetRenderPipelineAssetAt(i) as UniversalRenderPipelineAsset);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ZLZ Hub] Could not read quality level pipeline assets. {e.Message}");
            }

            return set;
        }

        static void AddDefaultRenderer(HashSet<UniversalRendererData> set, UniversalRenderPipelineAsset asset)
        {
            if (asset == null) return;

            var so   = new SerializedObject(asset);
            var list = so.FindProperty("m_RendererDataList");
            if (list == null || list.arraySize == 0) return;

            int idx = so.FindProperty("m_DefaultRendererIndex")?.intValue ?? 0;
            if (idx < 0 || idx >= list.arraySize) idx = 0;

            if (list.GetArrayElementAtIndex(idx).objectReferenceValue is UniversalRendererData data)
                set.Add(data);
        }

        // Everything in one URP asset's Renderer List, in list order, with the slot
        // number a feature's Renderer Index would use to refer to it.
        public static List<(int Index, string Name, bool IsDefault)> RendererListOf(UniversalRenderPipelineAsset asset)
        {
            var rows = new List<(int, string, bool)>();
            if (asset == null) return rows;

            var so   = new SerializedObject(asset);
            var list = so.FindProperty("m_RendererDataList");
            if (list == null) return rows;

            int def = so.FindProperty("m_DefaultRendererIndex")?.intValue ?? 0;

            for (int i = 0; i < list.arraySize; i++)
            {
                var entry = list.GetArrayElementAtIndex(i).objectReferenceValue;
                string name = entry != null
                    ? System.IO.Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(entry))
                    : "(missing)";

                rows.Add((i, name, i == def));
            }

            return rows;
        }

        public static UniversalRendererData GetActiveRendererData()
        {
            var urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urpAsset == null) return null;

            var so   = new SerializedObject(urpAsset);
            var list = so.FindProperty("m_RendererDataList");
            if (list == null || list.arraySize == 0) return null;

            int idx = so.FindProperty("m_DefaultRendererIndex")?.intValue ?? 0;
            if (idx < 0 || idx >= list.arraySize) idx = 0;
            return list.GetArrayElementAtIndex(idx).objectReferenceValue as UniversalRendererData;
        }

        // "URP-HighFidelity-Renderer"     -> "HighFidelity"
        // "UniversalRendererData_Grass"   -> "Grass"
        // Column headers have well under 60px, so every bit of boilerplate that
        // appears in all of them carries no information and has to go.
        static string ShortenRendererName(string fileName)
        {
            string s = fileName;

            foreach (var prefix in k_Prefixes)
                if (s.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                { s = s.Substring(prefix.Length); break; }

            foreach (var suffix in k_Suffixes)
                if (s.Length > suffix.Length && s.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                { s = s.Substring(0, s.Length - suffix.Length); break; }

            s = s.Trim('-', '_', ' ');
            return string.IsNullOrEmpty(s) ? fileName : s;
        }

        static readonly string[] k_Prefixes = { "UniversalRendererData", "UniversalRenderer", "URP-", "URP_", "URP " };
        static readonly string[] k_Suffixes = { "-Renderer", "_Renderer", "RendererData", "Renderer", "Data" };

        // ── Feature state ─────────────────────────────────────────────────────

        public static ScriptableRendererFeature Find(UniversalRendererData data, Type featureType)
        {
            if (data == null || featureType == null) return null;
            var features = data.rendererFeatures;
            for (int i = 0; i < features.Count; i++)
            {
                var f = features[i];
                if (f != null && featureType.IsInstanceOfType(f)) return f;
            }
            return null;
        }

        // Present-but-switched-off is a real state in these packages : several
        // features ship pre-installed and disabled. The matrix has to show that
        // as "not on", or the customer ticks nothing and nothing renders.
        public static bool IsEnabled(UniversalRendererData data, Type featureType)
        {
            var f = Find(data, featureType);
            return f != null && f.isActive;
        }

        public static bool IsPresent(UniversalRendererData data, Type featureType)
            => Find(data, featureType) != null;

        // ── Mutation ──────────────────────────────────────────────────────────

        // Adds the feature if it is not there. An already-present feature is left
        // exactly as it is - including switched off, which for several ZLZ features
        // is the correct installed state. Re-running install must never flip one on
        // behind the customer's back.
        public static void Install(UniversalRendererData data, ZLZ_HubFeature feature)
        {
            if (data == null || feature?.FeatureType == null) return;
            if (Find(data, feature.FeatureType) != null) return;

            var created = ScriptableObject.CreateInstance(feature.FeatureType) as ScriptableRendererFeature;
            if (created == null)
            {
                Debug.LogError($"[ZLZ Hub] {feature.FeatureType.Name} is not a ScriptableRendererFeature.");
                return;
            }

            created.name = string.IsNullOrEmpty(feature.AssetName) ? feature.FeatureType.Name : feature.AssetName;
            AssetDatabase.AddObjectToAsset(created, data);

            // URP keeps two lists in lock-step : the features themselves and a map of
            // their sub-asset local file ids. Growing only the first leaves the map
            // short, and URP's own delete path then removes the same index from both -
            // which throws "Retrieving array element that was out of bounds" the first
            // time a customer deletes the feature from the renderer inspector.
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(created, out _, out long localId);

            var so           = new SerializedObject(data);
            var featuresProp = so.FindProperty("m_RendererFeatures");
            featuresProp.arraySize++;
            featuresProp.GetArrayElementAtIndex(featuresProp.arraySize - 1).objectReferenceValue = created;

            var mapProp = so.FindProperty("m_RendererFeatureMap");
            if (mapProp != null)
            {
                mapProp.arraySize++;
                mapProp.GetArrayElementAtIndex(mapProp.arraySize - 1).longValue = localId;
            }

            so.ApplyModifiedPropertiesWithoutUndo();

            feature.Configure?.Invoke(created);
            SetFeatureActive(created, feature.DefaultActive);

            EditorUtility.SetDirty(data);
        }

        // Takes the feature out of the renderer entirely. Whatever was tuned on it is
        // gone, so callers must say so before running this.
        public static void Remove(UniversalRendererData data, ZLZ_HubFeature feature)
        {
            var existing = Find(data, feature?.FeatureType);
            if (existing == null) return;

            var so   = new SerializedObject(data);
            var list = so.FindProperty("m_RendererFeatures");
            var map  = so.FindProperty("m_RendererFeatureMap");

            for (int i = list.arraySize - 1; i >= 0; i--)
            {
                var element = list.GetArrayElementAtIndex(i);
                if (element.objectReferenceValue != existing) continue;

                // Nulling first is required : on an object-reference array the first
                // delete only clears the slot, it does not shorten the array.
                element.objectReferenceValue = null;
                list.DeleteArrayElementAtIndex(i);

                // Same index out of the map, so the two stay the same length - the
                // contract URP's own remove path relies on.
                if (map != null && i < map.arraySize) map.DeleteArrayElementAtIndex(i);
            }

            so.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.RemoveObjectFromAsset(existing);
            // Fully qualified : "using System" is in scope, so a bare Object is
            // ambiguous between System.Object and UnityEngine.Object.
            UnityEngine.Object.DestroyImmediate(existing, true);
            EditorUtility.SetDirty(data);
        }

        static void SetFeatureActive(ScriptableRendererFeature feature, bool active)
        {
            if (feature == null || feature.isActive == active) return;

            var so = new SerializedObject(feature);
            var p  = so.FindProperty("m_Active");
            if (p == null) return;

            p.boolValue = active;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(feature);
        }

        public static void Save()
        {
            AssetDatabase.SaveAssets();
        }
    }
}
