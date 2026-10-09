using FishGame.Core;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace FishGame.Gameplay
{
    // Authority: owner sends intents (SendTo.Server, InvokePermission Owner); host sends replies (SendTo.Owner,
    // InvokePermission Server). Every non-fishing player RPC lives here and each one sets InvokePermission explicitly
    // (spec 6.7, 7.6). The host validates every intent; the owner only asks. NGO enforces InvokePermission on receive.
    public sealed class PlayerActions : NetworkBehaviour
    {
        public const float InteractRangeSlack = 1.0f;

        private PlayerNetState _state;
        private PlayerLocomotion _locomotion;

        private void Awake()
        {
            _state = GetComponent<PlayerNetState>();
            _locomotion = GetComponent<PlayerLocomotion>();
        }

        // Runs on the host. The owner submits its display name from OnNetworkSpawn.
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void SubmitNameRpc(FixedString32Bytes name, RpcParams rpcParams = default)
        {
            if (_state == null)
            {
                return;
            }

            // Round-trip through NameWire so the roster copy (FixedString32Bytes in PlayerSlot) can never overflow.
            string sanitized = NameWire.ToFixed(DisplayNameRules.Sanitize(name.ToString(), _state.Slot)).ToString();
            _state.ServerSetDisplayName(sanitized);
            SessionState session = SessionState.Current;
            if (session != null)
            {
                session.ServerRenameClient(OwnerClientId, sanitized);
            }
        }

        // Runs on the host when the owner presses Interact. The host decides; no line-of-sight check (spec 7.6).
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void InteractRpc(NetworkBehaviourReference target, RpcParams rpcParams = default)
        {
            if (!target.TryGet(out NetworkInteractable interactable) || interactable == null)
            {
                return;
            }

            ulong sender = rpcParams.Receive.SenderClientId;
            if (!interactable.CanInteract(sender))
            {
                return;
            }

            // The host's (interpolated) copy of the sender's head.
            Vector3 eye = transform.position + Vector3.up * MovementMath.EyeHeight;
            if (Vector3.Distance(eye, interactable.InteractPoint) > interactable.Range + InteractRangeSlack)
            {
                return;
            }

            interactable.ServerInteract(sender);
        }

        // Runs on the owner when the host teleports this player. Only the owner may move its own root.
        [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
        public void TeleportRpc(Vector3 position, float yaw)
        {
            if (_locomotion != null)
            {
                _locomotion.ApplyTeleport(position, yaw);
            }
        }
    }
}
