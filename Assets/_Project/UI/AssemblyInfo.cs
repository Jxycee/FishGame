// Assembly marker (T1). Keeps this asmdef compiling before its first type exists. Do not delete.
// InternalsVisibleTo (T21 review): PlayMode tests call UiFlowController's internal OpenJournal, HandleBack and ModalOpen directly.
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("FishGame.Tests.PlayMode")]

namespace FishGame.UI { internal static class AssemblyMarker { } }
