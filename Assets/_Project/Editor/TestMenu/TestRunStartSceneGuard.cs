// Keeps Test Runner runs out of Bootstrap (D30). Owned by T15.
// PlayMode runs: test-framework 1.8.0 raises RunStarted only after entering play mode (PlayModeRunTask follows EnterPlayModeTask),
// which is too late. So ExitingEditMode clears the start scene when the runner's init scene (its controller object) is open.
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace FishGame.EditorTools
{
    [InitializeOnLoad]
    public static class TestRunStartSceneGuard
    {
        // PlaymodeTestsController.kPlaymodeTestControllerName (internal in test-framework 1.8.0).
        const string TestControllerObjectName = "Code-based tests runner";

        static TestRunStartSceneGuard()
        {
            // Callbacks are not serialized, so they are re-registered after every domain reload.
            TestRunnerApi.RegisterTestCallback(new Callbacks());
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingEditMode && GameObject.Find(TestControllerObjectName) != null)
                PlayModeStartScene.Clear();
        }

        sealed class Callbacks : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun) => PlayModeStartScene.Clear();
            public void RunFinished(ITestResultAdaptor result) => PlayModeStartScene.Apply();
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) { }
        }
    }
}
