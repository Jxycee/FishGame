// Runtime. Authority: local client only. The one input seam gameplay and UI read (spec 8.1). Implemented by PlayerInputAdapter and FakeInputService.
using System;
using FishGame.Core;

namespace FishGame.Runtime
{
    public interface IInputService
    {
        InputContext Context { get; }               // MenuOpen ? Menu : GameplayContext
        InputContext GameplayContext { get; set; }  // written by PlayerLocomotion (Free/Swimming/Climbing in M1)
        bool MenuOpen { get; set; }                 // written by UiFlowController (any modal or main menu visible)
        PlayerIntent Intent { get; }                // routed, recomputed once per frame before gameplay Update
        FightInput Fight { get; }
        // True the frame any control bound to the Journal action went down, in EVERY context (also Menu, where Intent.JournalPressed is
        // routed away). Honours rebinding. Only the journal UI uses it, to close itself with the key that opens it.
        bool JournalPressedAnyContext { get; }
        bool HasFocus { get; }
        InputDeviceKind LastDevice { get; }
        event Action<InputContext> ContextChanged;
        void SetRumble(float low, float high);      // 0..1; no-op without a gamepad
        string GetBindingDisplayString(string actionPath); // e.g. "E" for InputActionNames.Interact on keyboard; "" if unknown
    }
}
