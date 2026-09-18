#if UNITY_EDITOR
// Assets/ZLZ/AnimeShader/Runtime/ZLZ_AnimePaths.cs
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ZLZ.AnimeShader
{
    // ─────────────────────────────────────────────────────────────────────────
    //  Where this package actually lives, asked rather than assumed.
    //
    //  A .unitypackage update matches by GUID and overwrites files WHERE THEY
    //  ALREADY ARE - it does not move them to the path recorded in the package.
    //  So a customer who bought the package before the folder restructure keeps
    //  Assets/ZLZ_AnimeShader forever, while a fresh import lands in
    //  Assets/ZLZ/AnimeShader. Both layouts are live in the wild at once, and any
    //  hardcoded path is wrong for half of them - silently, because a bad path
    //  compiles fine and only shows up when a bake writes a folder into thin air.
    //
    //  Also covers the customer who simply drags our folder somewhere else, which
    //  hardcoded paths never did.
    // ─────────────────────────────────────────────────────────────────────────
    // public, not internal : the Hub provider lives in the Editor folder, which Unity
    // compiles into Assembly-CSharp-Editor - a different assembly from this one, so
    // internal would not reach it.
    public static class ZLZ_AnimePaths
    {
        // Only used if the anchor type cannot be located, which should not happen
        // while the package is installed at all.
        const string k_Fallback = "Assets/ZLZ/AnimeShader";

        static string _root;

        /// Package root, e.g. "Assets/ZLZ/AnimeShader" or "Assets/ZLZ_AnimeShader".
        public static string Root
        {
            get
            {
                // Re-resolves if the cached folder is gone : moving the package (the
                // Hub's own migration button does exactly that) would otherwise leave
                // this pointing at a path that no longer exists until a domain reload.
                if (!string.IsNullOrEmpty(_root) && AssetDatabase.IsValidFolder(_root)) return _root;

                // A failed resolve is never cached : it can mean "asked too early"
                // (mid-serialization) rather than "not there", and caching the fallback
                // would freeze a wrong root in for the rest of the session.
                string resolved = Resolve();
                if (string.IsNullOrEmpty(resolved)) return k_Fallback;

                _root = resolved;
                return _root;
            }
        }

        public static string Folder(string relative) => Root + "/" + relative;

        /// Path to a folder directly under the package root, created if missing.
        public static string EnsureFolder(string folderName)
        {
            string full = Folder(folderName);
            if (AssetDatabase.IsValidFolder(full)) return full;
            if (AssetDatabase.IsValidFolder(Root)) AssetDatabase.CreateFolder(Root, folderName);
            return full;
        }

        static string Resolve()
        {
            // Ask Unity which file a type we own was compiled from. Not a filename
            // search (a customer may well have their own ZLZ_CharacterDashboard.cs)
            // and not a GUID baked into code (that dies the moment a file is deleted
            // and re-added).
            MonoScript[] scripts;
            try
            {
                scripts = MonoImporter.GetAllRuntimeMonoScripts();
            }
            catch (UnityException)
            {
                // Unity refuses this call while it is serializing (rebuilding a saved
                // window layout, for one). No answer yet, so let the caller fall back
                // and ask again once the editor is idle.
                return null;
            }

            foreach (var script in scripts)
            {
                if (script == null || script.GetClass() != typeof(ZLZ_CharacterDashboard)) continue;

                string path = AssetDatabase.GetAssetPath(script);          // <root>/Runtime/ZLZ_CharacterDashboard.cs
                if (string.IsNullOrEmpty(path)) continue;

                string dir = Path.GetDirectoryName(path);                  // <root>/Runtime
                if (string.IsNullOrEmpty(dir)) continue;
                dir = dir.Replace('\\', '/');

                int cut = dir.LastIndexOf('/');
                return cut > 0 ? dir.Substring(0, cut) : dir;
            }

            return null;
        }
    }
}
#endif
