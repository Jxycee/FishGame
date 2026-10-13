using FishGame.Runtime;
using UnityEngine.UIElements;

namespace FishGame.UI
{
    // Authority: client-only (local UI). Seam for HUD add-ons (T25 fishing HUD, T26 dev overlay). UiFlowController binds
    // attached extensions at the end of BindScreens, ticks them every Update while a game screen is up, and unbinds them in
    // OnDestroy in reverse attach order.
    internal interface IHudExtension
    {
        void Bind(VisualElement root, ServiceRegistry app);
        void Tick(float dt);
        void Unbind();
    }
}
