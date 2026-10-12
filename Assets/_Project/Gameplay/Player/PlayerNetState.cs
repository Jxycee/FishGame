using System;
using System.Collections.Generic;
using FishGame.Core;
using FishGame.Runtime;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace FishGame.Gameplay
{
    // Authority: owner writes NetPitchQ, NetSwimming and NetTool; host writes NetSlot and NetDisplayName. Every peer reads all five.
    // The host also raises ServerInitialized (server only) at the very end of ServerInit (T16, G2): per-player server init for other
    // components (PlayerCreel, FishingRodNetwork) hangs off it, because PlayerSpawner runs OnNetworkSpawn BEFORE ServerInit and calls only
    // one IServerPlayerInit.
    // Owner-only components (CharacterController, PlayerLocomotion, PlayerCamera, PlayerInteractor, and the Camera and
    // AudioListener on HeadPivot/Shake/Camera) are enabled here on the owner, never by the prefab. Remotes enable only the
    // CapsuleCollider (layer RemotePlayer) so local players bump remote bodies. Spec 7.2, 7.5; contract 7.12.
    public sealed class PlayerNetState : NetworkBehaviour, IServerPlayerInit
    {
        private const float PitchSendIntervalSeconds = 1f / 15f;
        private const string OfflineDefaultName = "Fisher";

        public readonly NetworkVariable<short> NetPitchQ = new NetworkVariable<short>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        public readonly NetworkVariable<byte> NetSlot = new NetworkVariable<byte>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public readonly NetworkVariable<FixedString32Bytes> NetDisplayName = new NetworkVariable<FixedString32Bytes>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public readonly NetworkVariable<bool> NetSwimming = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        public readonly NetworkVariable<byte> NetTool = new NetworkVariable<byte>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);   // ToolKind

        private static readonly List<PlayerNetState> s_active = new List<PlayerNetState>();
        private static PlayerNetState s_local;

        public static IReadOnlyList<PlayerNetState> Active => s_active;
        public static PlayerNetState Local => s_local;
        public static event Action<PlayerNetState> LocalPlayerChanged;
        public static event Action<PlayerNetState> Spawned;
        public static event Action<PlayerNetState> Despawned;

        public byte Slot => NetSlot.Value;
        public string DisplayName => NetDisplayName.Value.ToString();
        public bool IsSwimming => NetSwimming.Value;
        public ToolKind Tool => (ToolKind)NetTool.Value;

        // True after ServerInit has finished (server only); reset on despawn.
        public bool IsServerInitialized { get; private set; }

        public event Action<byte> SlotChanged;
        public event Action<string> DisplayNameChanged;

        // Every peer, the writer included (NetworkVariable raises OnValueChanged on the writer). Not raised for the initial value:
        // consumers read Tool at spawn, then subscribe (N5).
        public event Action<ToolKind> ToolChanged;

        // Server only. Raised once per ServerInit, after NetSlot (and, unless the owner already sent it, the name) is written.
        // A throwing subscriber cannot break PlayerSpawner (EventIsolation).
        public event Action<PlayerNetState> ServerInitialized;

        private PlayerBody _body;
        private PlayerActions _actions;
        private PlayerLocomotion _locomotion;
        private PlayerCamera _playerCamera;
        private PlayerInteractor _interactor;
        private CharacterController _characterController;
        private CapsuleCollider _capsule;
        private UnityEngine.Camera _povCamera;
        private AudioListener _listener;
        private bool _ownerRigActive;
        private bool _subscribed;
        private bool _ownerNameReceived;
        private ISettings _watchedSettings;
        private FixedString32Bytes _lastSubmittedName;
        private bool _hasSubmittedName;
        private short _lastSentPitch;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_active.Clear();
            s_local = null;
            LocalPlayerChanged = null;
            Spawned = null;
            Despawned = null;
        }

        private void Awake()
        {
            NetPitchQ.SetUpdateTraits(new NetworkVariableUpdateTraits { MinSecondsBetweenUpdates = PitchSendIntervalSeconds });
            _body = GetComponent<PlayerBody>();
            _actions = GetComponent<PlayerActions>();
            _locomotion = GetComponent<PlayerLocomotion>();
            _playerCamera = GetComponent<PlayerCamera>();
            _interactor = GetComponent<PlayerInteractor>();
            _characterController = GetComponent<CharacterController>();
            _capsule = GetComponent<CapsuleCollider>();
            Transform cameraTransform = transform.Find(PlayerRigPaths.Camera);
            if (cameraTransform != null)
            {
                _povCamera = cameraTransform.GetComponent<UnityEngine.Camera>();
                _listener = cameraTransform.GetComponent<AudioListener>();
            }
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            _ownerNameReceived = false;
            gameObject.layer = IsOwner ? Layers.Player : Layers.RemotePlayer;
            if (!s_active.Contains(this))
            {
                s_active.Add(this);
            }

            NetSlot.OnValueChanged += HandleSlotChanged;
            NetDisplayName.OnValueChanged += HandleNameChanged;
            NetTool.OnValueChanged += HandleToolChanged;
            _subscribed = true;

            if (_body != null)
            {
                _body.Bind(this, IsOwner);
            }

            if (IsOwner)
            {
                EnableOwnerRig();
                s_local = this;
                LocalPlayerChanged?.Invoke(this);
                SubmitLocalName();
                WatchSettings();
            }
            else
            {
                SetEnabled(_capsule, true);
            }

            Spawned?.Invoke(this);
        }

        public override void OnNetworkDespawn()
        {
            IsServerInitialized = false;
            Unsubscribe();
            UnwatchSettings();
            if (_body != null)
            {
                _body.Unbind();
            }

            if (_ownerRigActive)
            {
                DisableOwnerRig();
            }

            SetEnabled(_capsule, false);
            s_active.Remove(this);
            if (s_local == this)
            {
                s_local = null;
                LocalPlayerChanged?.Invoke(null);
            }

            Despawned?.Invoke(this);
            base.OnNetworkDespawn();
        }

        public override void OnDestroy()
        {
            // Safety net when an object is destroyed without a despawn callback (domain teardown in tests).
            Unsubscribe();
            UnwatchSettings();
            s_active.Remove(this);
            if (s_local == this)
            {
                s_local = null;
                LocalPlayerChanged?.Invoke(null);
            }

            base.OnDestroy();
        }

        // Host only. Called by PlayerSpawner right after InstantiateAndSpawn.
        public void ServerInit(byte slot, string displayName)
        {
            if (!IsServer)
            {
                throw new InvalidOperationException("PlayerNetState.ServerInit is host-only.");
            }

            NetSlot.Value = slot;

            // On the host, OnNetworkSpawn (inside InstantiateAndSpawn) already ran the owner's SubmitNameRpc with the
            // current settings name. The roster copy passed here is older, so it must not overwrite that name.
            if (!_ownerNameReceived)
            {
                NetDisplayName.Value = NameWire.ToFixed(DisplayNameRules.Sanitize(displayName, slot));
            }

            // Last statement: NetSlot is written, so subscribers (PlayerCreel mirror, FishingRodNetwork) can read Slot.
            IsServerInitialized = true;
            EventIsolation.Invoke(ServerInitialized, this);
        }

        // Host only. Called by PlayerActions.SubmitNameRpc with an already sanitised name.
        public void ServerSetDisplayName(string sanitizedName)
        {
            if (!IsServer)
            {
                throw new InvalidOperationException("PlayerNetState.ServerSetDisplayName is host-only.");
            }

            _ownerNameReceived = true;
            NetDisplayName.Value = NameWire.ToFixed(sanitizedName);
        }

        // Owner only. Sends only on a change of at least 0.5 degrees (PitchQuant); the update traits cap the rate at 15 Hz.
        public void OwnerSetPitch(float pitchDeg)
        {
            if (!IsSpawned || !IsOwner)
            {
                return;
            }

            short q = PitchQuant.Quantize(pitchDeg);
            if (!PitchQuant.ShouldSend(_lastSentPitch, q))
            {
                return;
            }

            _lastSentPitch = q;
            NetPitchQ.Value = q;
        }

        // Owner only.
        public void OwnerSetSwimming(bool swimming)
        {
            if (!IsSpawned || !IsOwner)
            {
                return;
            }

            if (NetSwimming.Value != swimming)
            {
                NetSwimming.Value = swimming;
            }
        }

        // Owner only. Ignored when unchanged (the NetworkVariable would not send anyway, but this also skips the local event).
        public void OwnerSetTool(ToolKind tool)
        {
            if (!IsSpawned || !IsOwner)
            {
                return;
            }

            byte value = (byte)tool;
            if (NetTool.Value != value)
            {
                NetTool.Value = value;
            }
        }

        private void EnableOwnerRig()
        {
            _ownerRigActive = true;
            _lastSentPitch = NetPitchQ.Value;
            SetEnabled(_capsule, false);
            SetEnabled(_characterController, true);
            SetEnabled(_locomotion, true);
            SetEnabled(_playerCamera, true);
            SetEnabled(_interactor, true);
            SetEnabled(_povCamera, true);
            SetEnabled(_listener, true);
        }

        private void DisableOwnerRig()
        {
            _ownerRigActive = false;
            SetEnabled(_interactor, false);
            SetEnabled(_locomotion, false);
            SetEnabled(_playerCamera, false);
            SetEnabled(_characterController, false);
            SetEnabled(_listener, false);
            SetEnabled(_povCamera, false);
        }

        private void SubmitLocalName()
        {
            _hasSubmittedName = false;
            SubmitNameIfChanged();
        }

        // Owner only. A rename in Settings during a session re-sends the (sanitised) name; the host updates NetDisplayName.
        private void WatchSettings()
        {
            UnwatchSettings();
            ServiceRegistry app = ServiceRoots.App;
            if (app != null && app.TryGet(out ISettings settings))
            {
                _watchedSettings = settings;
                _watchedSettings.Changed += HandleSettingsChanged;
            }
        }

        private void UnwatchSettings()
        {
            if (_watchedSettings != null)
            {
                _watchedSettings.Changed -= HandleSettingsChanged;
                _watchedSettings = null;
            }
        }

        private void HandleSettingsChanged(SettingsData data)
        {
            if (IsSpawned && IsOwner)
            {
                SubmitNameIfChanged();
            }
        }

        private void SubmitNameIfChanged()
        {
            string displayName = OfflineDefaultName;
            ServiceRegistry app = ServiceRoots.App;
            if (app != null && app.TryGet(out ISettings settings) && settings.Current != null)
            {
                displayName = settings.Current.displayName;
            }

            FixedString32Bytes wire = NameWire.ToFixed(DisplayNameRules.Sanitize(displayName, Slot));
            if (_hasSubmittedName && _lastSubmittedName.Equals(wire))
            {
                return;
            }

            if (_actions != null)
            {
                _hasSubmittedName = true;
                _lastSubmittedName = wire;
                _actions.SubmitNameRpc(wire);
            }
        }

        private void Unsubscribe()
        {
            if (!_subscribed)
            {
                return;
            }

            NetSlot.OnValueChanged -= HandleSlotChanged;
            NetDisplayName.OnValueChanged -= HandleNameChanged;
            NetTool.OnValueChanged -= HandleToolChanged;
            _subscribed = false;
        }

        private void HandleSlotChanged(byte previous, byte current)
        {
            SlotChanged?.Invoke(current);
        }

        private void HandleNameChanged(FixedString32Bytes previous, FixedString32Bytes current)
        {
            DisplayNameChanged?.Invoke(current.ToString());
        }

        private void HandleToolChanged(byte previous, byte current)
        {
            EventIsolation.Invoke(ToolChanged, (ToolKind)current);
        }

        private static void SetEnabled(Behaviour behaviour, bool on)
        {
            if (behaviour != null)
            {
                behaviour.enabled = on;
            }
        }

        private static void SetEnabled(Collider collider, bool on)
        {
            if (collider != null)
            {
                collider.enabled = on;
            }
        }
    }
}
