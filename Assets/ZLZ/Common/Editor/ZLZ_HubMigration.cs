using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ZLZ.Hub
{
    // ─────────────────────────────────────────────────────────────────────────
    //  Moves a pre-restructure install into Assets/ZLZ.
    //
    //  Unity's package importer matches by GUID and overwrites files WHERE THEY
    //  ALREADY ARE - it never relocates them to the path recorded in the package.
    //  So a customer who bought before the restructure keeps the old folders no
    //  matter how many updates they install, and the only way to converge is to
    //  move the folders in their project.
    //
    //  One AssetDatabase.MoveAsset per folder does that properly : Unity carries
    //  the .meta files, keeps every GUID, and re-points every reference in every
    //  scene, prefab and material. Anything the customer put inside our folder
    //  travels with it, which is the point - their work must not be left behind
    //  in an orphaned folder.
    // ─────────────────────────────────────────────────────────────────────────
    internal static class ZLZ_HubMigration
    {
        const string k_NewRoot = "Assets/ZLZ";

        // Historical folder names, oldest layout first. Facts about our own past
        // releases, so a fixed list is the honest way to express them.
        static readonly (string From, string To)[] k_Moves =
        {
            ("Assets/ZLZ_AnimeShader",       k_NewRoot + "/AnimeShader"),
            ("Assets/ZLZ_EnvironmentShader", k_NewRoot + "/EnvironmentShader"),
            ("Assets/ZLZ_Common",            k_NewRoot + "/Common"),
        };

        public static List<(string From, string To)> Pending()
        {
            var list = new List<(string From, string To)>();
            foreach (var move in k_Moves)
                if (AssetDatabase.IsValidFolder(move.From)) list.Add(move);
            return list;
        }

        public static void Run()
        {
            var pending = Pending();
            if (pending.Count == 0) return;

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("ZLZ Hub",
                    "Leave Play mode first - moving assets while the game is running is not safe.", "OK");
                return;
            }

            // A move rewrites references inside open scenes. Unsaved edits would be
            // rewritten in memory and then lost on the next scene load.
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            // No "are you sure". This is the layout every project is meant to end up
            // on, the banner already said what it does, and a confirmation box would
            // dress a routine tidy-up as something risky. The guards that remain are
            // the ones that protect data, not the ones that ask permission twice.
            if (!AssetDatabase.IsValidFolder(k_NewRoot))
                AssetDatabase.CreateFolder("Assets", "ZLZ");

            var failures = new List<string>();
            var moved    = new List<string>();

            foreach (var move in pending)
            {
                if (MoveInto(move.From, move.To, failures)) moved.Add(move.To);
            }

            AssetDatabase.Refresh();

            foreach (var path in moved) Debug.Log($"[ZLZ Hub] Moved to {path}");

            if (failures.Count > 0)
            {
                var sb = new StringBuilder();
                sb.AppendLine("Some files could not be moved:");
                sb.AppendLine();
                foreach (var f in failures) sb.AppendLine("  " + f);
                sb.AppendLine();
                sb.AppendLine("Everything else moved. Move the rest by hand, or restore from version control.");

                Debug.LogWarning("[ZLZ Hub] " + sb);
                EditorUtility.DisplayDialog("ZLZ Hub", sb.ToString(), "OK");
                return;
            }

            if (moved.Count > 0) Debug.Log("[ZLZ Hub] Folder layout is now Assets/ZLZ.");
        }

        // Moves a folder to its new home, MERGING when something is already there.
        //
        // A plain MoveAsset refuses a destination that exists, and that destination
        // routinely does exist : files added in the new version have no GUID in the
        // customer's project, so Unity puts them at the package's recorded path while
        // everything it recognises stays behind at the old one. The customer is then
        // holding both folders and the one operation that would reunite them is the
        // one that will not run. So move the CONTENTS instead, recursing wherever
        // both sides have a folder of the same name.
        static bool MoveInto(string from, string to, List<string> failures)
        {
            if (!AssetDatabase.IsValidFolder(from)) return false;

            if (!AssetDatabase.IsValidFolder(to))
            {
                string error = AssetDatabase.MoveAsset(from, to);
                if (string.IsNullOrEmpty(error)) return true;

                failures.Add($"{from}  ({error})");
                return false;
            }

            bool movedAnything = false;

            // Sub-folders first, so a name that exists on both sides merges rather
            // than colliding.
            foreach (var child in AssetDatabase.GetSubFolders(from))
            {
                string leaf = Path.GetFileName(child);
                if (MoveInto(child, to + "/" + leaf, failures)) movedAnything = true;
            }

            // Then the loose files at this level.
            foreach (var guid in AssetDatabase.FindAssets(string.Empty, new[] { from }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path) || AssetDatabase.IsValidFolder(path)) continue;

                // FindAssets searches the whole subtree ; anything deeper has already
                // travelled with its own folder.
                if (Path.GetDirectoryName(path)?.Replace('\\', '/') != from) continue;

                string target = to + "/" + Path.GetFileName(path);
                string error  = AssetDatabase.MoveAsset(path, target);

                if (string.IsNullOrEmpty(error)) { movedAnything = true; continue; }
                failures.Add($"{path}  ({error})");
            }

            // An emptied folder left behind would keep the migration banner up for
            // ever, so clear it - but only once it really is empty.
            if (AssetDatabase.GetSubFolders(from).Length == 0 &&
                AssetDatabase.FindAssets(string.Empty, new[] { from }).Length == 0)
            {
                AssetDatabase.DeleteAsset(from);
            }

            return movedAnything;
        }
    }
}
