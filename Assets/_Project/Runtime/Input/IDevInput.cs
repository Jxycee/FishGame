// Runtime. Authority: local client only. Implemented by PlayerInputAdapter (reads Keyboard.current F3, F4, F5, F6, F7, F9, F10) and FakeInputService.
// Always false unless UnityEngine.Debug.isDebugBuild, the window has focus and the context is not Menu.
// Consumers resolve it null-safely: ServiceRoots.App.Get<IInputService>() as IDevInput.
namespace FishGame.Runtime
{
    public interface IDevInput
    {
        bool WasPressed(DevKey key);
    }
}
