// Gameplay/World/EcologySession.cs - the session-level holder of the host's EcologyRunState (C 7.14, M3-D17, M3-D57).
// Authority: host only (host memory, never sent). Session-level holder of the EcologyRunState (seed, season, day, cast counter, catch
// ledger; M3-D17) that survives Spot loads. Cleared when the session ends (INetworkSession.Ended, subscribed here) and on SubsystemRegistration.
// Ruling CCR-D3: GetOrCreate returns the existing run state and ignores the seed; a dev seed change is Clear() + GetOrCreate(seed).
using FishGame.Core;
using FishGame.Runtime;
using UnityEngine;

namespace FishGame.Gameplay
{
    public static class EcologySession
    {
        private static EcologyRunState s_current;
        private static INetworkSession s_session;   // the session whose Ended we listen to (null in EditMode: ServiceRoots.App is null)

        public static EcologyRunState Current => s_current;

        public static EcologyRunState GetOrCreate(ulong runSeed)
        {
            // Stale guard: SubsystemRegistration runs once per Play Mode entry, not per PlayMode test. If a session ended without Ended
            // reaching us (App destroyed first), the run state belongs to a dead session: drop it.
            if (s_current != null && !ReferenceEquals(s_session, CurrentAppSession())) Clear();
            if (s_current != null) return s_current;
            var run = new EcologyRunState(runSeed);
            run.Season = WeatherDirector.RollSeason(runSeed);   // M3-D10: rolled once per session from the run seed; Day starts at 1 (T5)
            s_current = run;
            HookSessionEnd();
            return run;
        }

        public static void Clear()
        {
            s_current = null;
            if (s_session != null)
            {
                s_session.Ended -= HandleEnded;
                s_session = null;
            }
        }

        private static void HookSessionEnd()
        {
            if (s_session != null) return;
            INetworkSession session = CurrentAppSession();
            if (session != null)
            {
                s_session = session;
                s_session.Ended += HandleEnded;
            }
        }

        // null in EditMode (no App) - then s_session is null too and the stale guard matches.
        private static INetworkSession CurrentAppSession()
        {
            ServiceRegistry app = ServiceRoots.App;
            return app != null && app.TryGet(out INetworkSession session) ? session : null;
        }

        private static void HandleEnded(EndReason reason)
        {
            Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_current = null;
            s_session = null;
        }
    }
}
