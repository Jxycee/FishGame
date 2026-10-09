using System.Threading;
using System.Threading.Tasks;
using Unity.Netcode;

namespace FishGame.Networking
{
    // Authority: none (helper). Awaits NGO shutdown, one frame per Task.Yield on the Unity main thread.
    public static class NetworkManagerAwait
    {
        public static async Task WaitForShutdownAsync(NetworkManager nm, CancellationToken ct)
        {
            while (nm != null && nm.ShutdownInProgress)
            {
                ct.ThrowIfCancellationRequested();
                await Task.Yield();
            }
        }

        public static async Task ShutdownAsync(NetworkManager nm)
        {
            if (nm == null)
            {
                return;
            }
            if (!nm.ShutdownInProgress && (nm.IsListening || nm.IsServer || nm.IsClient))
            {
                nm.Shutdown();
            }
            await WaitForShutdownAsync(nm, CancellationToken.None);
        }
    }
}
