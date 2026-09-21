using System;
using System.IO;
using UnityEngine;

// Event-only diagnostics. Callers own the opt-in; never run from ready-cue polling.
public static class DefensiveBlockDiagnostics
{
    public static string LogPath { get; private set; }
    static bool warned;
    static string Folder => Path.Combine(Application.persistentDataPath, "Diagnostics");

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetSession() { LogPath = null; warned = false; }

    public static void Write(string message, UnityEngine.Object context)
    {
        string record = $"[DefensiveBlock] {DateTime.UtcNow:O} frame={Time.frameCount} " +
            $"scene='{UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}' {message}";
        Debug.Log(record, context);
        if (warned) return;
        try
        {
            if (LogPath == null)
            {
                Directory.CreateDirectory(Folder);
                LogPath = Path.Combine(Folder, $"DefensiveBlock-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid().ToString("N").Substring(0, 6)}.log");
                Debug.Log("[DefensiveBlock] Session log: " + LogPath, context);
            }
            File.AppendAllText(LogPath, record + Environment.NewLine);
        }
        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
        {
            warned = true;
            Debug.LogWarning("[DefensiveBlock] File logging unavailable; Console logging continues. " + e.Message, context);
        }
    }

    public static void OpenFolder()
    {
        try { Directory.CreateDirectory(Folder); Application.OpenURL(new Uri(Folder + Path.DirectorySeparatorChar).AbsoluteUri); }
        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
        { Debug.LogWarning("[DefensiveBlock] Cannot open log folder: " + e.Message); }
    }
}
