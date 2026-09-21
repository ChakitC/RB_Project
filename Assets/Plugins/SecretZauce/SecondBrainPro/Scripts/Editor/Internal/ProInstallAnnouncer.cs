using System.IO;
using SecretZauce.SecondBrain.Editor;
using UnityEditor;
using UnityEngine;

namespace SecretZauce.SecondBrain.Pro.Editor
{
    /// <summary>
    /// Opens the Installer when a Pro install/update is detected.
    ///
    /// This assembly ships only with Pro, so simply running is proof that Pro is installed —
    /// there is no state machine to advance and no define to wait on. That is what lets the
    /// free side drop its Pro-presence tracking: the case it existed for (Pro imported into a
    /// project whose free setup had already finished) is now just this class running for the
    /// first time.
    ///
    /// The announcement stamp is project-scoped and includes both version and source-asset write
    /// time, so reinstalling the same Pro version still re-opens the Installer without opening it
    /// on every domain reload.
    /// </summary>
    [InitializeOnLoad]
    internal static class ProInstallAnnouncer
    {
        const string AnnouncedStampKeyPrefix = "SecondBrain.Pro.AnnouncedStamp.";
        const string ProAsmdefName           = "SecretZauce.SecondBrain.Pro.Editor";

        static ProInstallAnnouncer()
        {
            EditorApplication.delayCall += Announce;
        }

        static void Announce()
        {
            string current = SecondBrainProVersion.Current;
            if (string.IsNullOrEmpty(current))
                return;

            string currentStamp = BuildAnnouncementStamp(current);
            string normalizedProjectPath = Path.GetFullPath(Application.dataPath)
                .Replace('\\', '/')
                .ToLowerInvariant();
            string projectKey = Hash128.Compute(normalizedProjectPath).ToString();
            string prefsKey     = AnnouncedStampKeyPrefix + projectKey;

            if (EditorPrefs.GetString(prefsKey, string.Empty) == currentStamp)
                return;

            try
            {
                InstallerWindow.Open();
                EditorPrefs.SetString(prefsKey, currentStamp);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[SecondBrain] Could not open InstallerWindow: {ex.Message}");
                EditorPrefs.SetString(prefsKey, currentStamp);
            }
        }

        static string BuildAnnouncementStamp(string version)
        {
            var guids = AssetDatabase.FindAssets(
                $"{ProAsmdefName} t:AssemblyDefinitionAsset",
                new[] { "Assets", "Packages" });
            if (guids.Length == 0)
                return version;

            string asmdefPath = null;
            foreach (string guid in guids)
            {
                string candidatePath = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(candidatePath))
                    continue;
                if (Path.GetFileNameWithoutExtension(candidatePath) != ProAsmdefName)
                    continue;
                asmdefPath = candidatePath;
                break;
            }
            if (string.IsNullOrEmpty(asmdefPath))
                return version;

            string absolutePath = Path.GetFullPath(asmdefPath);
            if (!File.Exists(absolutePath))
                return version;

            long stamp = File.GetLastWriteTimeUtc(absolutePath).Ticks;
            if (stamp == 0)
                return version;
            return $"{version}|{stamp}";
        }
    }
}
