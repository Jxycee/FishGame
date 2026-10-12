// Authority: test-only (no networked state). Drives the real App through Bootstrap -> Menu -> solo host -> leave for PlayMode tests.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FishGame.App;
using FishGame.Core;
using FishGame.Gameplay;
using FishGame.Networking;
using FishGame.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FishGame.Tests.PlayMode
{
    public static class PlayModeHarness
    {
        public const float DefaultTimeoutSeconds = 10f;
        private const float SessionTimeoutSeconds = 20f;
        private const float TeardownTimeoutSeconds = 20f;
        private const float RecoveryTimeoutSeconds = 10f;
        private const string ResetSceneName = "FG_TestReset";

        public static IEnumerator LoadBootstrapAndWaitForMenu()
        {
            // AppBootstrap's duplicate guard would destroy the NEW App if a stale one survived, so refuse to continue on a leak.
            // Recover first (fresh empty scene, other scenes unloaded) so tests after the failing one start clean.
            string leak = null;
            if (AppBootstrap.Current != null)
            {
                leak = "A previous test leaked the App (AppBootstrap.Current is not null); fix the first failing test's teardown first";
            }
            else if (Unity.Netcode.NetworkManager.Singleton != null)
            {
                leak = "A previous test leaked a NetworkManager (NetworkManager.Singleton is not null)";
            }
            else if (ServiceRoots.Game != null)
            {
                leak = "A previous test leaked ServiceRoots.Game (a gameplay scene's GameScope survived)";
            }
            if (leak != null)
            {
                var stale = AppBootstrap.Current;
                if (stale != null)
                {
                    try
                    {
                        stale.DestroyForTests();
                    }
                    catch (Exception e)
                    {
                        TestContext.WriteLine("leak recovery: DestroyForTests threw: " + e.GetBaseException().Message);
                    }
                }
                yield return RecoverFromLeakedScenes();
                Assert.Fail(leak);
            }

            AppBootstrap.SuppressDevAutoStart = true;
            SceneManager.LoadScene(SceneNames.Bootstrap, LoadSceneMode.Single);
            yield return WaitUntil(
                () => AppBootstrap.Current != null && SceneManager.GetActiveScene().name == SceneNames.Menu,
                SessionTimeoutSeconds,
                "Bootstrap did not reach the Menu scene");
            yield return null; // let Menu objects and UiFlowController's activeSceneChanged handling settle
        }

        public static IEnumerator StartSoloAndWaitForPlayer()
        {
            var app = AppBootstrap.Current;
            Assert.That(app, Is.Not.Null, "StartSoloAndWaitForPlayer: no App; call LoadBootstrapAndWaitForMenu first");

            var local = app.Services.Get<LocalNetworkSession>();
            local.Port = NetworkConstants.TestPort;
            var session = app.Services.Get<INetworkSession>();
            Assert.That(session.State, Is.EqualTo(ConnState.Offline), "Solo start requested while the session is not Offline");

            var cts = new CancellationTokenSource();
            yield return Await(session.StartLocalAsync(false, cts.Token), SessionTimeoutSeconds, cts);
            cts.Dispose();
            yield return WaitUntil(
                () => PlayerNetState.Local != null && SceneManager.GetActiveScene().name == SceneNames.SpotLakeDock,
                SessionTimeoutSeconds,
                "Local player never spawned in Spot_LakeDock after solo start");
            yield return null; // one frame for owner-side OnEnable work (locomotion, camera, SceneCameraSwitch)
        }

        public static IEnumerator LeaveAndWaitForMenu()
        {
            var app = AppBootstrap.Current;
            Assert.That(app, Is.Not.Null, "LeaveAndWaitForMenu: no App");
            var session = app.Services.Get<INetworkSession>();
            var nm = app.NetworkManager;

            yield return Await(session.LeaveAsync(), SessionTimeoutSeconds);
            yield return WaitUntil(
                () => session.State == ConnState.Offline
                      && !nm.ShutdownInProgress
                      && !nm.IsListening
                      && PlayerNetState.Local == null
                      && SceneManager.GetActiveScene().name == SceneNames.Menu,
                SessionTimeoutSeconds,
                "Did not return to an idle Menu after leave");
            yield return null;
        }

        // Never asserts before DestroyForTests: a failing teardown must still remove the App, or every later test is poisoned.
        public static IEnumerator TearDownApp()
        {
            try
            {
                string problem = null;
                var app = AppBootstrap.Current;
                if (app != null)
                {
                    INetworkSession session = null;
                    if (app.Services != null && app.Services.TryGet<INetworkSession>(out var found))
                    {
                        session = found;
                    }

                    if (session != null && session.State != ConnState.Offline)
                    {
                        Task leave = null;
                        try
                        {
                            leave = session.LeaveAsync();
                        }
                        catch (Exception e)
                        {
                            problem = "LeaveAsync threw: " + e.GetBaseException().Message;
                        }

                        if (leave != null)
                        {
                            double leaveDeadline = Time.realtimeSinceStartupAsDouble + TeardownTimeoutSeconds;
                            while (!leave.IsCompleted && Time.realtimeSinceStartupAsDouble < leaveDeadline)
                            {
                                yield return null;
                            }

                            if (!leave.IsCompleted)
                            {
                                problem = $"LeaveAsync did not complete within {TeardownTimeoutSeconds} s";
                            }
                            else if (leave.IsFaulted)
                            {
                                problem = "LeaveAsync faulted: " + leave.Exception.GetBaseException().Message;
                            }
                        }
                    }

                    app.DestroyForTests();
                }

                double deadline = Time.realtimeSinceStartupAsDouble + TeardownTimeoutSeconds;
                while ((AppBootstrap.Current != null || Unity.Netcode.NetworkManager.Singleton != null || PlayerNetState.Active.Count > 0
                        || ServiceRoots.App != null || ServiceRoots.Game != null)
                       && Time.realtimeSinceStartupAsDouble < deadline)
                {
                    yield return null;
                }
                yield return null;
                yield return null;

                if (problem == null && AppBootstrap.Current != null)
                {
                    problem = "AppBootstrap.Current is still set after DestroyForTests";
                }
                if (problem == null && Unity.Netcode.NetworkManager.Singleton != null)
                {
                    problem = "NetworkManager.Singleton is still set after DestroyForTests";
                }
                if (problem == null && PlayerNetState.Active.Count > 0)
                {
                    problem = $"{PlayerNetState.Active.Count} PlayerNetState(s) still registered after DestroyForTests";
                }
                if (problem == null && ServiceRoots.App != null)
                {
                    problem = "ServiceRoots.App is still set after DestroyForTests";
                }
                if (problem == null && ServiceRoots.Game != null)
                {
                    problem = "ServiceRoots.Game is still set (gameplay scene not unloaded)";
                }
                if (ServiceRoots.Game != null || ServiceRoots.App != null)
                {
                    // Recover so later tests start clean; the failure below still reports the original problem.
                    yield return RecoverFromLeakedScenes();
                }
                if (problem != null)
                {
                    Assert.Fail("TearDownApp: " + problem);
                }
            }
            finally
            {
                // Reset even if DestroyForTests, NGO shutdown, or an Assert below throws, so the flag cannot leak into the next test.
                AppBootstrap.SuppressDevAutoStart = false;
            }
        }

        // Puts a fresh empty scene in place, unloads every other loaded scene, then waits (bounded) for the service roots to clear.
        // Used only after a leak was detected; the caller still fails the test with the original message.
        private static IEnumerator RecoverFromLeakedScenes()
        {
            double deadline = Time.realtimeSinceStartupAsDouble + RecoveryTimeoutSeconds;

            Scene reset = SceneManager.CreateScene(ResetSceneName);
            SceneManager.SetActiveScene(reset);

            var toUnload = new List<Scene>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded && scene.handle != reset.handle)
                {
                    toUnload.Add(scene);
                }
            }

            foreach (Scene scene in toUnload)
            {
                if (!scene.isLoaded)
                {
                    continue;
                }

                AsyncOperation op = null;
                try
                {
                    op = SceneManager.UnloadSceneAsync(scene);
                }
                catch (Exception e)
                {
                    TestContext.WriteLine("leak recovery: could not unload scene '" + scene.name + "': " + e.GetBaseException().Message);
                }

                while (op != null && !op.isDone && Time.realtimeSinceStartupAsDouble < deadline)
                {
                    yield return null;
                }
            }

            while ((ServiceRoots.Game != null || ServiceRoots.App != null) && Time.realtimeSinceStartupAsDouble < deadline)
            {
                yield return null;
            }

            // Last resort so the next test is not poisoned by a stale static; the caller still reports the leak.
            if (ServiceRoots.Game != null)
            {
                ServiceRoots.SetGame(null);
            }
            if (ServiceRoots.App != null)
            {
                ServiceRoots.SetApp(null);
            }
            yield return null;
        }

        // Records Warning/Error/Exception/Assert log entries while alive. Wrap a region in using and call AssertNoWarnings at its end.
        public sealed class LogCapture : IDisposable
        {
            // The only tolerated line: Unity licensing noise that appears on machines without a signed-in Hub session.
            private const string BenignLicensingSubstring = "Access token is unavailable";

            private readonly object _gate = new object();
            private readonly List<string> _entries = new List<string>();
            private bool _disposed;

            public LogCapture()
            {
                Application.logMessageReceivedThreaded += OnLog;
            }

            public int Count
            {
                get
                {
                    lock (_gate)
                    {
                        return _entries.Count;
                    }
                }
            }

            private void OnLog(string condition, string stackTrace, LogType type)
            {
                if (type == LogType.Log)
                {
                    return;
                }
                if (condition != null && condition.Contains(BenignLicensingSubstring))
                {
                    return;
                }

                lock (_gate)
                {
                    _entries.Add("[" + type + "] " + condition);
                }
            }

            public void AssertNoWarnings(string context)
            {
                string[] snapshot;
                lock (_gate)
                {
                    snapshot = _entries.ToArray();
                }

                if (snapshot.Length > 0)
                {
                    Assert.Fail(context + ": " + snapshot.Length + " warning/error log entr" + (snapshot.Length == 1 ? "y" : "ies")
                                + " captured:\n" + string.Join("\n", snapshot));
                }
            }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }
                _disposed = true;
                Application.logMessageReceivedThreaded -= OnLog;
            }
        }

        public static IEnumerator WaitUntil(Func<bool> condition, float timeoutSeconds, string failMessage)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + timeoutSeconds;
            while (!condition())
            {
                if (Time.realtimeSinceStartupAsDouble >= deadline)
                {
                    Assert.Fail($"{failMessage} (timed out after {timeoutSeconds} s)");
                }
                yield return null;
            }
        }

        // Frames AND realtime: a fixed sleep proves little on a slow machine, a frame count alone proves little on a fast one.
        public static IEnumerator WaitFrames(int frames, float minRealtimeSeconds)
        {
            if (frames <= 0 && minRealtimeSeconds <= 0f)
            {
                yield break;
            }

            int startFrame = Time.frameCount;
            double startTime = Time.realtimeSinceStartupAsDouble;
            while (Time.frameCount - startFrame < frames || Time.realtimeSinceStartupAsDouble - startTime < minRealtimeSeconds)
            {
                yield return null;
            }
        }

        // Game-time wait with a realtime guard, so a paused or zero timeScale fails fast instead of hanging.
        public static IEnumerator WaitGameSeconds(float seconds, float realtimeTimeoutSeconds, string failMessage)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + realtimeTimeoutSeconds;
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                if (Time.realtimeSinceStartupAsDouble >= deadline)
                {
                    Assert.Fail($"{failMessage} (game time {elapsed:F3} of {seconds} s after {realtimeTimeoutSeconds} s realtime; timeScale {Time.timeScale})");
                }
                yield return null;
                elapsed += Time.deltaTime;
            }
        }

        public static IEnumerator Await(Task task, float timeoutSeconds, CancellationTokenSource cancelOnTimeout = null)
        {
            Assert.That(task, Is.Not.Null, "Await: task is null");
            double deadline = Time.realtimeSinceStartupAsDouble + timeoutSeconds;
            while (!task.IsCompleted)
            {
                if (Time.realtimeSinceStartupAsDouble >= deadline)
                {
                    cancelOnTimeout?.Cancel();
                    // Observe a late fault so it cannot surface as an unobserved task exception during a later test.
                    task.ContinueWith(t => { var unused = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
                    Assert.Fail($"Task did not complete within {timeoutSeconds} s");
                }
                yield return null;
            }

            if (task.IsFaulted)
            {
                Assert.Fail("Task faulted: " + task.Exception.GetBaseException());
            }
            if (task.IsCanceled)
            {
                Assert.Fail("Task was cancelled");
            }
        }
    }
}
