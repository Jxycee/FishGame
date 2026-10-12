// Assembly marker (T1). Keeps this asmdef compiling before its first type exists. Do not delete.
// InternalsVisibleTo (T16 review follow-up): PlayMode tests reach PlayerNetState.HandleToolChanged and SessionState.Upsert.
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("FishGame.Tests.PlayMode")]

namespace FishGame.Gameplay { internal static class AssemblyMarker { } }
