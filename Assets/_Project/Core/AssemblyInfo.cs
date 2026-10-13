// Assembly marker (T1). Keeps this asmdef compiling before its first type exists. Do not delete.
// InternalsVisibleTo (T7, C 7.7; R72): FightSim test seams (ForcePhase, ForceState, DrawCount, Wave, RunSpeedMult, TimeEps).
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("FishGame.Tests.EditMode")]

namespace FishGame.Core { internal static class AssemblyMarker { } }
