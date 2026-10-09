// Runtime. Authority: local client only. Rebinding seam used by the settings UI (spec 8.3 rebinding).
using UnityEngine.InputSystem;

namespace FishGame.Runtime
{
    public interface IInputBindings
    {
        InputActionAsset Actions { get; }           // the runtime copy used by the adapter
        void SaveBindingOverrides();                // SaveBindingOverridesAsJson -> settings.bindingOverridesJson; settings.Save()
        void ResetAllBindingOverrides();            // RemoveAllBindingOverrides(); SaveBindingOverrides()
    }
}
