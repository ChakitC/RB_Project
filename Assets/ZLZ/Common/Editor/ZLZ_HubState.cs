using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ZLZ.Hub
{
    // ─────────────────────────────────────────────────────────────────────────
    //  Hub state lives in ProjectSettings/ZLZ_Hub.json, not EditorPrefs.
    //
    //  EditorPrefs is per-machine : on a team project every programmer would get
    //  the welcome window again, and "already set up" would be a per-person
    //  opinion rather than a fact about the project. ProjectSettings travels with
    //  the repo, which is what these flags actually describe.
    // ─────────────────────────────────────────────────────────────────────────
    [Serializable]
    internal class ZLZ_HubProductState
    {
        public string id;

        // The only thing worth remembering : whether this project has ever been
        // shown the Hub. Everything else the window needs - is it set up, what is
        // installed where - is read back from the renderers themselves, so it can
        // never disagree with reality or depend on a version string somebody has
        // to keep accurate by hand.
        public bool shown;
    }

    // Only facts about the PROJECT live here, because this file is meant to be
    // committed. How many times one person opened the window, or whether they
    // answered a review prompt, is about the person - keeping that here made the
    // file change on almost every domain reload and turned every commit into a
    // diff nobody wanted to read.
    [Serializable]
    internal class ZLZ_HubStateData
    {
        public List<ZLZ_HubProductState> products = new List<ZLZ_HubProductState>();
    }

    internal static class ZLZ_HubState
    {
        const string k_FileName   = "ZLZ_Hub.json";
        const string k_DateFormat = "yyyy-MM-dd";

        static ZLZ_HubStateData _data;

        static string FilePath
        {
            get
            {
                string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
                return Path.Combine(projectRoot, "ProjectSettings", k_FileName);
            }
        }

        public static ZLZ_HubStateData Data
        {
            get
            {
                if (_data != null) return _data;

                try
                {
                    if (File.Exists(FilePath))
                        _data = JsonUtility.FromJson<ZLZ_HubStateData>(File.ReadAllText(FilePath));
                }
                catch (Exception e)
                {
                    // A corrupt state file must never stop the customer from using
                    // the package - worst case they see the welcome flow twice.
                    Debug.LogWarning($"[ZLZ Hub] Could not read {k_FileName}, starting fresh. {e.Message}");
                }

                if (_data == null) _data = new ZLZ_HubStateData();
                return _data;
            }
        }

        public static void Save()
        {
            try
            {
                string json = JsonUtility.ToJson(Data, true);

                // Writing an identical file still updates its timestamp, and some
                // version control front-ends read that as a change. Nothing should
                // appear in a diff unless something actually differs.
                if (File.Exists(FilePath) && File.ReadAllText(FilePath) == json) return;

                string dir = Path.GetDirectoryName(FilePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(FilePath, json);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ZLZ Hub] Could not write {k_FileName}. {e.Message}");
            }
        }

        public static ZLZ_HubProductState For(string id)
        {
            var list = Data.products;
            for (int i = 0; i < list.Count; i++)
                if (list[i].id == id) return list[i];

            var created = new ZLZ_HubProductState { id = id };
            list.Add(created);
            return created;
        }
    }
}
