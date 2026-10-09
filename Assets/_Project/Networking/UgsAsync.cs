using System;
using System.Threading;
using System.Threading.Tasks;
using FishGame.Core;
using FishGame.Runtime;

namespace FishGame.Networking
{
    // Authority: none (local async helpers for the UGS path). The SDK calls cannot be aborted, so these helpers stop
    // waiting but never stop the work itself.
    internal static class UgsAsync
    {
        // Completes when `work` completes and rethrows its exception. Throws OperationCanceledException when `ct` is
        // cancelled first, and NetworkSessionException(Timeout) after NetworkConstants.AsyncTimeoutSeconds.
        internal static async Task WithTimeoutAsync(Task work, CancellationToken ct)
        {
            if (!work.IsCompleted)
            {
                using (var cts = CancellationTokenSource.CreateLinkedTokenSource(ct))
                {
                    var delay = Task.Delay(TimeSpan.FromSeconds(NetworkConstants.AsyncTimeoutSeconds), cts.Token);
                    var first = await Task.WhenAny(work, delay);
                    cts.Cancel(); // stops the timer when the work won
                    if (first != work)
                    {
                        Observe(work);
                        ct.ThrowIfCancellationRequested();
                        throw new NetworkSessionException(EndReason.Timeout, "Timed out waiting for Multiplayer Services.");
                    }
                }
            }

            await work;
        }

        internal static async Task<T> WithTimeoutAsync<T>(Task<T> work, CancellationToken ct)
        {
            await WithTimeoutAsync((Task)work, ct);
            return await work;
        }

        // True when `work` finished within `seconds`. Never throws (the work's own exception is not rethrown).
        internal static async Task<bool> WaitAtMostAsync(Task work, double seconds)
        {
            if (work.IsCompleted)
            {
                return true;
            }

            using (var cts = new CancellationTokenSource())
            {
                var first = await Task.WhenAny(work, Task.Delay(TimeSpan.FromSeconds(seconds), cts.Token));
                cts.Cancel();
                if (first != work)
                {
                    Observe(work);
                    return false;
                }

                return true;
            }
        }

        // Marks a fault as observed, so an abandoned task never raises UnobservedTaskException.
        private static void Observe(Task work)
        {
            work.ContinueWith(
                t => { _ = t.Exception; },
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }
    }
}
