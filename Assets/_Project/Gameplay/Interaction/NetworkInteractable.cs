// Authority: host decides (ServerInteract). Clients only display GetPrompt and evaluate CanInteract to grey the prompt.
using Unity.Netcode;
using UnityEngine;

namespace FishGame.Gameplay
{
    public abstract class NetworkInteractable : NetworkBehaviour
    {
        // Owner sphere-cast range in metres (spec 7.7). The server allows Range + 1.0 m from the sender's head.
        public virtual float Range => 3.0f;

        // Server range-check target. PlayerActions.InteractRpc (T11) measures distance to this point.
        public virtual Vector3 InteractPoint => transform.position;

        // Local display only, without the key glyph, for example "Ring bell".
        public abstract string GetPrompt(ulong clientId);

        // Server truth. Also evaluated locally to grey the prompt.
        public virtual bool CanInteract(ulong clientId) => true;

        // Server only. Implementations must ignore calls when !IsServer.
        public abstract void ServerInteract(ulong clientId);
    }
}
