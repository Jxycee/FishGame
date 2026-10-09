using System.Threading;
using System.Threading.Tasks;
using FishGame.Runtime;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;

namespace FishGame.Networking
{
    // Authority: none (client-side service setup). Process-wide and lazy: only UgsNetworkSession calls it, on the first
    // online action, so Solo and dev-direct never reach UGS (spec 6.3).
    internal static class UgsBootstrap
    {
        private static Task s_initTask;

        // Enter Play Mode without a domain reload keeps statics; start each play session clean.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_initTask = null;
        }

        // Initializes Unity Services once per process (cached task), then signs in anonymously if needed.
        // A faulted initialization clears the cache, so the next online action retries.
        internal static async Task EnsureSignedInAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            if (s_initTask == null)
            {
                s_initTask = InitializeAsync();
            }

            var init = s_initTask;
            try
            {
                await init;
            }
            catch
            {
                if (ReferenceEquals(s_initTask, init))
                {
                    s_initTask = null;
                }

                throw;
            }

            ct.ThrowIfCancellationRequested();

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }
        }

        private static Task InitializeAsync()
        {
            if (UnityServices.State == ServicesInitializationState.Initialized)
            {
                return Task.CompletedTask;
            }

            var options = new InitializationOptions();
            var profile = InstanceProfile.Current;

            // MPPM clones already get a per-clone profile from "-editor-mode -name <x>" (F4). Only -dev-profile
            // instances (several standalone builds on one machine) set it explicitly. Name is sanitized to
            // [a-z0-9_-], max 30 chars, which SetProfile accepts.
            if (!string.IsNullOrEmpty(profile.Name) && !profile.IsMppmClone)
            {
                options.SetProfile(profile.Name);
            }

            return UnityServices.InitializeAsync(options);
        }
    }
}
