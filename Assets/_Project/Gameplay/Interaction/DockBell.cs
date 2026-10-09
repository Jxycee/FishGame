// Authority: host validates and rings (ServerInteract). The ring cue is broadcast to ClientsAndHost by RingRpc,
// which only the server may invoke. Cooldown uses NetworkManager.ServerTime.Time: the server records it in
// ServerInteract (authoritative), and RingRpc records it on every peer so clients can grey the prompt locally.
// A client records the ring about RTT/2 late, so its prompt greys slightly longer than the server's cooldown (cosmetic only).
using System;
using FishGame.Core;
using FishGame.Runtime;
using Unity.Netcode;
using UnityEngine;

namespace FishGame.Gameplay
{
    public sealed class DockBell : NetworkInteractable
    {
        public const float CooldownSeconds = 1.0f;

        private const string BellChildName = "Bell";
        private const float SwingSeconds = 0.5f;
        private const float SwingDegrees = 18f;

        private double m_lastRingTime = double.NegativeInfinity;
        private float m_swingRemaining;
        private Transform m_bell;
        private Quaternion m_bellRest = Quaternion.identity;

        // Increments on every peer (host and clients) each time RingRpc runs.
        public int LocalRingCount { get; private set; }

        // Local, raised on every peer including the host.
        public event Action Rang;

        private void Awake()
        {
            m_bell = transform.Find(BellChildName);
            if (m_bell != null)
            {
                m_bellRest = m_bell.localRotation;
            }
        }

        private void Update()
        {
            if (m_bell == null || m_swingRemaining <= 0f)
            {
                return;
            }

            m_swingRemaining -= Time.deltaTime;
            if (m_swingRemaining <= 0f)
            {
                m_swingRemaining = 0f;
                m_bell.localRotation = m_bellRest;
                return;
            }

            float t = 1f - (m_swingRemaining / SwingSeconds);
            float angle = Mathf.Sin(t * Mathf.PI * 3f) * SwingDegrees * (1f - t);
            m_bell.localRotation = m_bellRest * Quaternion.Euler(angle, 0f, 0f);
        }

        public override string GetPrompt(ulong clientId)
        {
            return "Ring bell";
        }

        public override bool CanInteract(ulong clientId)
        {
            return NowServerTime() - m_lastRingTime >= CooldownSeconds;
        }

        public override void ServerInteract(ulong clientId)
        {
            if (!IsServer)
            {
                return;
            }

            if (!CanInteract(clientId))
            {
                return;
            }

            m_lastRingTime = NowServerTime();
            RingRpc();
        }

        [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
        private void RingRpc()
        {
            m_lastRingTime = NowServerTime();

            var app = ServiceRoots.App;
            if (app != null && app.TryGet<AudioBus>(out var audioBus) && audioBus != null)
            {
                audioBus.PlayAt(SfxIds.BellRing, transform.position);
            }

            m_swingRemaining = SwingSeconds;
            LocalRingCount++;
            if (Debug.isDebugBuild)
                Debug.Log($"[DockBell] rang (local count {LocalRingCount})");
            Rang?.Invoke();
        }

        private double NowServerTime()
        {
            var manager = NetworkManager;
            return manager != null ? manager.ServerTime.Time : 0.0;
        }
    }
}
