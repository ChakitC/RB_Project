using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

// Opt-in result export survives the domain reload of Play Mode tests.
[InitializeOnLoad]
public static class StateHubTestResults
{
    const string Pending = "RB.StateHub.TestResults.Pending";
    const string Report = "RB.StateHub.TestResults.Report";
    static TestRunnerApi api;
    static readonly Callbacks callbacks = new();

    static StateHubTestResults() { if (SessionState.GetBool(Pending, false)) Register(); }
    public static void Arm()
    {
        SessionState.SetBool(Pending, true);
        SessionState.SetString(Report, "Waiting for Unity Test Runner.\n");
        Register();
    }
    public static string Read() => SessionState.GetString(Report, "Not armed.");
    static void Register()
    {
        if (api != null) return;
        api = ScriptableObject.CreateInstance<TestRunnerApi>();
        api.RegisterCallbacks(callbacks);
    }
    static void Append(string line) => SessionState.SetString(Report, Read() + line + "\n");

    sealed class Callbacks : ICallbacks
    {
        public void RunStarted(ITestAdaptor tests) { if (SessionState.GetBool(Pending, false)) Append("Run started: " + tests.Name); }
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result)
        {
            if (!SessionState.GetBool(Pending, false) || result.HasChildren) return;
            Append(result.Test.FullName + ": " + result.ResultState + "\n" + result.Message + "\n" + result.StackTrace);
        }
        public void RunFinished(ITestResultAdaptor result)
        {
            if (!SessionState.GetBool(Pending, false)) return;
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../../BuildArtifacts/StateHub-Runner.xml"));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            TestRunnerApi.SaveResultToFile(result, path);
            Append("Completed: " + result.ResultState + "; XML: " + path);
            SessionState.SetBool(Pending, false);
        }
    }
}
