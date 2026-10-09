// Not networked; runs once at boot on every peer. Dev-only auto-start from command-line tokens,
// MPPM player tags (CurrentPlayer.ReadOnlyTags) and the editor "Auto Solo" pref.
// TryAutoStart does nothing unless Debug.isDebugBuild (editor or Development Build). Auto Solo applies only in the main editor (MPPM clones share EditorPrefs).
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FishGame.Core;
using FishGame.Runtime;
using UnityEngine;
#if UNITY_EDITOR
using Unity.Multiplayer.PlayMode;
using UnityEditor;
#endif

namespace FishGame.App
{
    public static class DevStartup
    {
        public static IReadOnlyList<string> CollectArgs()
        {
            var args = new List<string>(Environment.GetCommandLineArgs());
#if UNITY_EDITOR
            try
            {
                var tags = CurrentPlayer.ReadOnlyTags();
                if (tags != null)
                {
                    foreach (var tag in tags)
                    {
                        if (string.IsNullOrWhiteSpace(tag)) continue;
                        args.AddRange(tag.Split((char[])null, StringSplitOptions.RemoveEmptyEntries));
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("DevStartup: could not read MPPM player tags: " + e.Message);
            }
#endif
            return args;
        }

        public static void TryAutoStart(INetworkSession session, DevFlags flags)
        {
            if (!Debug.isDebugBuild) return;
            if (session == null || flags == null) return;

            if (flags.Solo)
            {
                Observe("-dev-solo", () => session.StartLocalAsync(false, CancellationToken.None));
                return;
            }
            if (flags.HostDirect)
            {
                Observe("-dev-host-direct", () => session.StartLocalAsync(true, CancellationToken.None));
                return;
            }
            if (flags.HasJoinDirect)
            {
                string address = flags.JoinDirectAddress;
                ushort port = flags.JoinDirectPort;
                Observe("-dev-join-direct", () => session.JoinDirectAsync(address, port, CancellationToken.None));
                return;
            }
#if UNITY_EDITOR
            if (EditorPrefs.GetBool(DevPrefKeys.AutoSolo, false) && IsMainEditor())
            {
                Observe("Auto Solo", () => session.StartLocalAsync(false, CancellationToken.None));
            }
#endif
        }

#if UNITY_EDITOR
        private static bool IsMainEditor()
        {
            try
            {
                return CurrentPlayer.IsMainEditor;
            }
            catch (Exception)
            {
                return false; // unknown -> never auto-solo (a clone must not start a second solo host)
            }
        }
#endif

        // Start tasks fault with NetworkSessionException; the UI already learns of failures from Ended (toast via SceneFlow), so only log here.
        private static async void Observe(string label, Func<Task> start)
        {
            try
            {
                await start();
            }
            catch (Exception e)
            {
                Debug.LogWarning("DevStartup: " + label + " did not connect (" + e.GetType().Name + "): " + e.Message);
            }
        }
    }
}
