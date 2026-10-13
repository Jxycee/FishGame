// Assembly marker (T1). Keeps this asmdef compiling before its first type exists. Do not delete.
// InternalsVisibleTo PlayMode (T18/T19 review): AmbienceBed.StepWind is driven with a fixed delta time by WeatherFxTests.
// InternalsVisibleTo EditMode (T20): SaveService.Serialize / DeserializeOrNull / IsReadOnly are tested directly.
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("FishGame.Tests.PlayMode")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("FishGame.Tests.EditMode")]
namespace FishGame.Runtime { internal static class AssemblyMarker { } }
