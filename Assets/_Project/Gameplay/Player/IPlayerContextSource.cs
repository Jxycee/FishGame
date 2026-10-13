// Authority: none (interface). Implemented by components on the player root that want a gameplay context (fishing now, M5 carry later)
// without Gameplay referencing them. PlayerLocomotion (owner only) caches GetComponents<IPlayerContextSource>() and is still the ONLY writer
// of IInputService.GameplayContext: it takes the first ENABLED source whose RequestedContext is not Free and lets
// GameplayContextArbiter apply the precedence (Climbing > Swimming > request > Free). C 7.16, M2-D1.
using FishGame.Core;

namespace FishGame.Gameplay
{
    public interface IPlayerContextSource
    {
        // Free = no request.
        InputContext RequestedContext { get; }

        // Belt and braces next to the router's RodFighting row: true locks movement even before the router has seen the new context.
        bool LocksMovement { get; }
    }
}
