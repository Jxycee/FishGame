// Editor tooling (T1). Menu entry points that run the EditMode or PlayMode suites through the
// Test Runner API and save the NUnit XML result under Logs/FishGame. Not used at runtime.
using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace FishGame.EditorTools
{
    // Registered on every domain load: the Test Runner drops registered callbacks on domain reload,
    // and a PlayMode run reloads the domain. The pending run survives in UnityEditor.SessionState.
    [InitializeOnLoad]
    public static class TestMenu
    {
        public const string EditModeResultPath = "Logs/FishGame/editmode.xml";
        public const string PlayModeResultPath = "Logs/FishGame/playmode.xml";

        private const string PendingPathKey = "FishGame.TestMenu.PendingResultPath";
        private const string PendingModeKey = "FishGame.TestMenu.PendingModeName";

        static TestMenu()
        {
            TestRunnerApi.RegisterTestCallback(new ResultSaver());
        }

        [MenuItem("FishGame/Run EditMode Tests")]
        public static void RunEditMode()
        {
            Run(TestMode.EditMode, "EditMode", EditModeResultPath);
        }

        [MenuItem("FishGame/Run PlayMode Tests")]
        public static void RunPlayMode()
        {
            Run(TestMode.PlayMode, "PlayMode", PlayModeResultPath);
        }

        private static void Run(TestMode mode, string modeName, string relativePath)
        {
            UnityEditor.SessionState.SetString(PendingPathKey, relativePath);
            UnityEditor.SessionState.SetString(PendingModeKey, modeName);
            TestRunnerApi api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.Execute(new ExecutionSettings(new UnityEditor.TestTools.TestRunner.Api.Filter { testMode = mode }));
        }

        private sealed class ResultSaver : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun)
            {
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                string relativePath = UnityEditor.SessionState.GetString(PendingPathKey, string.Empty);
                if (string.IsNullOrEmpty(relativePath))
                {
                    // This run was not started from this menu (command line or Test Runner window).
                    return;
                }

                string modeName = UnityEditor.SessionState.GetString(PendingModeKey, "Unknown");
                UnityEditor.SessionState.EraseString(PendingPathKey);
                UnityEditor.SessionState.EraseString(PendingModeKey);

                string fullPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", relativePath));
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
                TestRunnerApi.SaveResultToFile(result, fullPath);
                Debug.Log("FG TESTS " + modeName + " passed=" + result.PassCount + " failed=" + result.FailCount);
            }

            public void TestStarted(ITestAdaptor test)
            {
            }

            public void TestFinished(ITestResultAdaptor result)
            {
            }
        }
    }
}
