// Assembly marker (T1). Keeps this asmdef compiling before its first type exists. Do not delete.
// InternalsVisibleTo (T16 review follow-up): PlayMode tests reach PlayerNetState.HandleToolChanged and SessionState.Upsert.
// M3 T2 (M3-D70): EditMode tests reach the internal NameWire and SessionText (the name-truncation test).
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("FishGame.Tests.PlayMode")]
[assembly: InternalsVisibleTo("FishGame.Tests.EditMode")]

namespace FishGame.Gameplay { internal static class AssemblyMarker { } }
