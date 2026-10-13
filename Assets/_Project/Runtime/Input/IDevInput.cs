// Runtime. Authority: local client only. Implemented by PlayerInputAdapter (reads Keyboard.current F3, F4, F5, F6, F7, F8, F9, F10) and FakeInputService.
// Always false unless UnityEngine.Debug.isDebugBuild, the window has focus and the context is not Menu (this goes for WasPressed AND IsHeld,
// so losing focus or opening a menu reads as "released").
// Consumers resolve it null-safely: ServiceRoots.App.Get<IInputService>() as IDevInput.
namespace FishGame.Runtime
{
    public interface IDevInput
    {
        bool WasPressed(DevKey key);
        bool IsHeld(DevKey key);    // G-CR3 (M3-D89): the F8 scrub is "held", not a press
    }
}
