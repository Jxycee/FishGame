using FishGame.Core;
using FishGame.Runtime;
using Unity.Netcode;
using UnityEngine;

namespace FishGame.Gameplay
{
    // Authority: owner only (enabled by PlayerNetState). The prompt is local display; a press is only a request sent to the
    // host through PlayerActions.InteractRpc, and the host decides (CanInteract, range, ServerInteract). Spec 7.7.
    // InteractPromptChanged.Prompt is the raw GetPrompt text without the key glyph (T14 composes the key).
    public sealed class PlayerInteractor : MonoBehaviour
    {
        public const float CastRange = 3.0f, CastRadius = 0.15f;

        public NetworkInteractable Current { get; private set; }

        private PlayerNetState _net;
        private PlayerActions _actions;
        private PlayerCamera _playerCamera;
        private EventBus _publishedTo;
        private string _lastPrompt;
        private bool _lastCanInteract;
        private bool _hasPublished;

        private void Awake()
        {
            _net = GetComponent<PlayerNetState>();
            _actions = GetComponent<PlayerActions>();
            _playerCamera = GetComponent<PlayerCamera>();
        }

        private void OnEnable()
        {
            Current = null;
            _hasPublished = false;
            _lastPrompt = null;
            _lastCanInteract = false;
            _publishedTo = null;
        }

        private void Update()
        {
            IInputService input = ResolveInput();
            UnityEngine.Camera view = _playerCamera != null ? _playerCamera.Camera : null;

            // Interactables are live in Free, RodReady and RodWaiting with no menu open (spec 8.2, InputContextRouter.InteractLive;
            // M2 G5 replaces the M1 "Free only" gate so the rod being out does not block the bell). Not while Swimming: there Interact only climbs at exits.
            bool eligible = input != null && _net != null && view != null
                && InputContextRouter.InteractLive(input.GameplayContext) && !input.MenuOpen;

            NetworkInteractable found = null;
            if (eligible)
            {
                Transform origin = view.transform;
                if (Physics.SphereCast(origin.position, CastRadius, origin.forward, out RaycastHit hit,
                        CastRange, Layers.Mask(Layers.Interactable), QueryTriggerInteraction.Ignore))
                {
                    found = hit.collider.GetComponentInParent<NetworkInteractable>();
                    if (found != null && !found.IsSpawned)
                    {
                        found = null;
                    }
                }
            }

            Current = found;
            ulong localId = _net != null ? _net.OwnerClientId : 0UL;
            string prompt = found != null ? found.GetPrompt(localId) : null;
            bool canInteract = found != null && found.CanInteract(localId);
            Publish(prompt, canInteract);

            // One request per press; no hold-to-interact.
            if (eligible && found != null && canInteract && input.Intent.InteractPressed
                && _actions != null && _actions.IsSpawned)
            {
                _actions.InteractRpc(new NetworkBehaviourReference(found));
            }
        }

        private void OnDisable()
        {
            Current = null;
            if (_hasPublished && _publishedTo != null)
            {
                _publishedTo.Publish(new InteractPromptChanged(null, false));
            }

            _hasPublished = false;
            _lastPrompt = null;
            _lastCanInteract = false;
            _publishedTo = null;
        }

        private void Publish(string prompt, bool canInteract)
        {
            if (_hasPublished && prompt == _lastPrompt && canInteract == _lastCanInteract)
            {
                return;
            }

            EventBus bus = ResolveBus();
            if (bus == null)
            {
                return;
            }

            _lastPrompt = prompt;
            _lastCanInteract = canInteract;
            _hasPublished = true;
            _publishedTo = bus;
            bus.Publish(new InteractPromptChanged(prompt, canInteract));
        }

        private static IInputService ResolveInput()
        {
            ServiceRegistry app = ServiceRoots.App;
            return app != null && app.TryGet(out IInputService input) ? input : null;
        }

        private static EventBus ResolveBus()
        {
            ServiceRegistry app = ServiceRoots.App;
            return app != null && app.TryGet(out EventBus bus) ? bus : null;
        }
    }
}
