// Assembly marker (T1). Keeps this asmdef compiling before its first type exists. Do not delete.
// InternalsVisibleTo (T17, ruling E-Q2): EditMode tests reach the internal wire seams (CatchAnnouncement.FromWire).
// PlayMode (slot takeover follow-up): the takeover test drives the internal FishingHost handlers (HandleSelectRod / HandleSelectBait).
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("FishGame.Tests.EditMode")]
[assembly: InternalsVisibleTo("FishGame.Tests.PlayMode")]

namespace FishGame.Fishing { internal static class AssemblyMarker { } }
