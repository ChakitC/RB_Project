using System;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

/// <summary>Opt-in native XML results across the Test Runner's domain reloads.</summary>
[InitializeOnLoad]
public static class AIContextTestEvidence
{
    const string Key = "RB.AIContext.TestEvidencePath";
    static AIContextTestEvidence()
    {
        var path = SessionState.GetString(Key, "");
        if (!string.IsNullOrEmpty(path)) Register(path);
    }
    public static void Arm(string path)
    {
        SessionState.SetString(Key, path);
        Register(path);
    }
    public static void Disarm() => SessionState.EraseString(Key);
    static void Register(string path)
    {
        // Unity's serialized ResultsSavingCallbacks already implements the native XML writer.
        var type = typeof(TestRunnerApi).Assembly.GetType(
            "UnityEditor.TestTools.TestRunner.CommandLineTest.ResultsSavingCallbacks", true);
        var callback = ScriptableObject.CreateInstance(type);
        type.GetField("m_ResultFilePath").SetValue(callback, path);
        var api = ScriptableObject.CreateInstance<TestRunnerApi>();
        api.RegisterCallbacks<ICallbacks>((ICallbacks)callback);
    }
}
