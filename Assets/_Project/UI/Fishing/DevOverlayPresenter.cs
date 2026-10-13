using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using FishGame.Core;
using FishGame.Data;
using FishGame.Fishing;
using FishGame.Presentation;
using FishGame.Runtime;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UIElements;

namespace FishGame.UI
{
    // Authority: client-only (local UI), dev builds only (Debug.isDebugBuild, checked at runtime; never #if). Keys come from IDevInput only.
    // Reads FishingRodController.Local (Machine getters), the local FishingRodNetwork NetVars, and on the host FishingHost.Current.Ledger.
    // Its only writes are the C 7.26 ones: the owner's SelectRodRpc / SelectBaitRpc (the host decides and mirrors the loadout back, so the
    // overlay shows the NetVar truth), the HOST-only statics FishingDebugSettings.ForcedBiteWait / TwitchyHook / ForcedSpeciesId
    // (F6, F7, F10), and a local ripple (F9). RunSeed is displayed only (ruling G-Q5). Spec 9.11 (last bullet), 17.2 item 5.
    // Keys: F3 overlay, F4 next rod, F5 next bait, F6 forced bite (host), F7 twitchy hook (host), F9 ripple at the crosshair,
    // F10 cycle the forced species None -> each species -> None (host only, not while paused). F8 is reserved for M3.
    internal sealed class DevOverlayPresenter : IHudExtension
    {
        private const float RefreshSeconds = 0.1f;      // 10 Hz, and only while visible
        private const float LastActionSeconds = 4f;
        private const float ForcedBiteOnSeconds = 0.1f;
        private const float RayLength = 100f;

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static readonly DevKey[] Keys =
        {
            DevKey.ToggleOverlay, DevKey.NextRod, DevKey.NextBait, DevKey.ToggleForcedBite, DevKey.ToggleTwitchy, DevKey.RippleAtCrosshair,
            DevKey.CycleForcedSpecies,
        };

        private readonly StringBuilder _sb = new StringBuilder(768);
        private readonly LogOnce _log = new LogOnce();

        private bool _bound;
        private bool _visible;                  // survives scene loads: this presenter lives in the App root
        private Label _label;
        private IDevInput _dev;
        private ContentRegistry _registry;
        private float _refreshTimer;
        private string _lastAction = string.Empty;
        private float _lastActionTimer;

        public void Bind(VisualElement root, ServiceRegistry app)
        {
            // Never throws: UiFlowController's bind loop has no try/catch (T2 review).
            try
            {
                if (_bound) Unbind();
                if (!Debug.isDebugBuild) return;   // belt and braces: the hook is guarded too
                BindCore(root, app);
            }
            catch (Exception exception)
            {
                Unbind();
                Debug.LogException(exception);
            }
        }

        private void BindCore(VisualElement root, ServiceRegistry app)
        {
            if (root == null || app == null) return;
            _label = UiQuery.Req<Label>(root, "DevOverlay");
            _dev = app.TryGet(out IInputService input) ? input as IDevInput : null;   // may be null: the keys then do nothing
            _registry = app.TryGet(out ContentRegistry registry) ? registry : null;
            _refreshTimer = RefreshSeconds;
            _bound = true;
            ApplyVisible();
        }

        // Never throws: UiFlowController's extension loop would skip the later extensions' work (an RPC during shutdown can throw here).
        public void Tick(float dt)
        {
            try
            {
                TickCore(dt);
            }
            catch (Exception exception)
            {
                if (_log.ShouldLog("tick:" + exception.GetType().Name)) Debug.LogException(exception);   // once per exception type, so a new kind is not hidden
            }
        }

        private void TickCore(float dt)
        {
            if (!_bound) return;

            if (_lastActionTimer > 0f)
            {
                _lastActionTimer -= dt;
                if (_lastActionTimer <= 0f) _lastAction = string.Empty;
            }

            // Keys work whether the overlay is shown or hidden. IDevInput is false outside dev builds, without focus and in the Menu context.
            if (_dev != null)
            {
                for (int i = 0; i < Keys.Length; i++)
                {
                    if (_dev.WasPressed(Keys[i])) HandleKey(Keys[i]);
                }
            }

            if (!_visible) return;
            _refreshTimer += dt;
            if (_refreshTimer < RefreshSeconds) return;
            _refreshTimer = 0f;
            Refresh();
        }

        public void Unbind()
        {
            if (_label != null) _label.EnableInClassList("hidden", true);
            _bound = false;
            _label = null;
            _dev = null;
            _registry = null;
            _lastAction = string.Empty;
            _lastActionTimer = 0f;
        }

        // ---- keys ----

        private void HandleKey(DevKey key)
        {
            switch (key)
            {
                case DevKey.ToggleOverlay:
                    _visible = !_visible;
                    ApplyVisible();
                    SetLast(_visible ? "overlay on" : "overlay off");
                    break;
                case DevKey.NextRod:
                    SelectNext(rod: true);
                    break;
                case DevKey.NextBait:
                    SelectNext(rod: false);
                    break;
                case DevKey.ToggleForcedBite:
                    if (!IsHost()) { SetLast(UiStrings.Get("dev.host_only")); break; }
                    FishingDebugSettings.ForcedBiteWait = FishingDebugSettings.ForcedBiteWait > 0f ? 0f : ForcedBiteOnSeconds;
                    SetLast("forcedBite=" + F2(FishingDebugSettings.ForcedBiteWait));
                    break;
                case DevKey.ToggleTwitchy:
                    if (!IsHost()) { SetLast(UiStrings.Get("dev.host_only")); break; }
                    FishingDebugSettings.TwitchyHook = !FishingDebugSettings.TwitchyHook;
                    SetLast("twitchy=" + OnOff(FishingDebugSettings.TwitchyHook));
                    break;
                case DevKey.RippleAtCrosshair:
                    RippleAtCrosshair();
                    break;
                case DevKey.CycleForcedSpecies:
                    CycleForcedSpecies();
                    break;
            }
        }

        // F10: None -> every species in index order -> None (host only; nothing while the solo game is paused). The rule lives in
        // FishingDebugSettings.CycleForcedSpecies so EditMode tests cover it.
        private void CycleForcedSpecies()
        {
            var ids = new List<string>();
            if (_registry != null)
            {
                int count = _registry.Species.Count;
                for (int i = 0; i < count; i++) ids.Add(_registry.SpeciesAt((ushort)i).Id);
            }

            switch (FishingDebugSettings.CycleForcedSpecies(IsHost(), IsPaused(), ids))
            {
                case ForcedSpeciesKeyResult.Changed:
                    SetLast("forcedSpecies=" + ForcedSpeciesText());
                    break;
                case ForcedSpeciesKeyResult.HostOnly:
                    SetLast(UiStrings.Get("dev.host_only"));
                    break;
                case ForcedSpeciesKeyResult.NoSpecies:
                    SetLast("no species in the registry");
                    break;
            }
        }

        private static string ForcedSpeciesText()
        {
            string id = FishingDebugSettings.ForcedSpeciesId;
            return string.IsNullOrEmpty(id) ? "none" : id;
        }

        private static bool IsPaused()
        {
            ServiceRegistry game = ServiceRoots.Game;
            return game != null && game.TryGet(out IPauseState pause) && pause.Paused;
        }

        // Host check by the NetworkManager, never by FishingHost.Current.Ledger (null before the first server tick).
        private static bool IsHost()
        {
            NetworkManager nm = NetworkManager.Singleton;
            return nm != null && nm.IsServer;
        }

        // F4 / F5. The host refuses while a cast is active (FishingHostLedger.SelectRod / SelectBait) and the NetVar mirror is the truth,
        // so the owner pre-checks Idle (nothing sent otherwise) and the LOADOUT line shows what the host actually applied.
        private void SelectNext(bool rod)
        {
            FishingRodController controller = FishingRodController.Local;
            FishingRodNetwork net = controller != null ? controller.GetComponent<FishingRodNetwork>() : null;
            if (net == null || !net.IsSpawned || controller.Machine == null) { SetLast(UiStrings.Get("dev.no_rod")); return; }
            if (controller.Machine.State != RodState.Idle) { SetLast(UiStrings.Get("dev.refused_cast")); return; }
            if (_registry == null) { SetLast("no content registry"); return; }

            int count = rod ? _registry.Rods.Count : _registry.Baits.Count;
            if (count <= 0) { SetLast(rod ? "no rods in the registry" : "no baits in the registry"); return; }

            if (rod)
            {
                ushort next = (ushort)NextIndex(net.NetRodIndex.Value, count);
                net.SelectRodRpc(next);
                SetLast("rod request: " + RodName(next));
            }
            else
            {
                byte next = (byte)NextIndex(net.NetSelectedBait.Value, count);
                net.SelectBaitRpc(next);
                SetLast("bait request: " + BaitName(next));
            }
        }

        internal static int NextIndex(int current, int count) => count <= 0 ? 0 : (current + 1) % count;

        // F9: the same first-hit rule as casting (CastProbe.CastMask, triggers included), so a dock above the water blocks it.
        private void RippleAtCrosshair()
        {
            Camera cam = Camera.main;
            RippleField field = RippleField.Current;
            if (cam != null && field != null)
            {
                Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
                if (Physics.Raycast(ray, out RaycastHit hit, RayLength, CastProbe.CastMask, QueryTriggerInteraction.Collide)
                    && hit.collider.gameObject.layer == Layers.Water)
                {
                    field.Spawn(hit.point.x, hit.point.z, RippleMath.Bite);
                    SetLast("ripple at " + F1(hit.point.x) + ", " + F1(hit.point.z));
                    return;
                }
            }
            SetLast(UiStrings.Get("dev.no_water"));
        }

        private void SetLast(string text)
        {
            _lastAction = text ?? string.Empty;
            _lastActionTimer = LastActionSeconds;
            if (_visible) _refreshTimer = RefreshSeconds;   // repaint on the next tick, not up to 0.1 s later
        }

        private void ApplyVisible()
        {
            if (_label != null) _label.EnableInClassList("hidden", !_visible);
        }

        // ---- overlay text ----

        private void Refresh()
        {
            if (_label == null) return;
            _sb.Clear();

            FishingRodController controller = FishingRodController.Local;
            RodStateMachine machine = controller != null ? controller.Machine : null;
            if (machine != null)
            {
                AppendRod(machine);
                AppendFight(machine);
                if (machine.HasResult)
                {
                    AppendBite(machine);
                    AppendRoll(machine.Roll);
                }
            }

            FishingRodNetwork net = controller != null ? controller.GetComponent<FishingRodNetwork>() : null;
            if (net != null) AppendLoadout(net);

            AppendHost();

            if (_lastAction.Length > 0) _sb.Append("LAST ").Append(_lastAction).Append('\n');
            if (_sb.Length == 0) _sb.Append("(no local rod)");
            _label.text = _sb.ToString().TrimEnd('\n');
        }

        private void AppendRod(RodStateMachine m)
        {
            _sb.Append("ROD state=").Append(m.State).Append(" t=").Append(F1(m.StateTime))
                .Append(" castId=").Append(m.CastId.ToString(Inv))
                .Append(" assist=").Append(OnOff(m.Assist))
                .Append(" recall=").Append(m.Recalling ? "yes" : "no").Append('\n');
        }

        private void AppendFight(RodStateMachine m)
        {
            FightSim fight = m.Fight;
            if (fight == null) return;
            FightState s = fight.State;
            if (fight.IsJunk)
            {
                _sb.Append("FIGHT junk line=").Append(F1(s.LineLength)).Append(" el=").Append(F1(s.Elapsed))
                    .Append(" slack=").Append(F2(s.SlackTimer)).Append('\n');
                return;
            }

            _sb.Append("FIGHT phase=").Append(s.Phase).Append(" dir=").Append(((int)s.BurstDir).ToString(Inv))
                .Append(" back=").Append(s.Back ? "1" : "0")
                .Append(" strain=").Append(F2(s.Strain)).Append(" tension=").Append(F2(s.Tension)).Append(" tire=").Append(F2(s.Tire))
                .Append(" p=").Append(F2(fight.PullWave))
                .Append(" line=").Append(F1(s.LineLength)).Append("m lat=").Append(F2(s.Lateral))
                .Append(" el=").Append(F1(s.Elapsed)).Append("s over=").Append(F2(s.SnapTimer))
                .Append(" recov=").Append(s.NearSnapRecoveries.ToString(Inv)).Append(" max=").Append(F2(s.MaxTension)).Append('\n');
        }

        private void AppendBite(RodStateMachine m)
        {
            FishingRoll r = m.Roll;
            _sb.Append("BITE timer=").Append(F1(m.BiteTimer)).Append('/').Append(F1(m.BiteWait)).Append("s nibbles=")
                .Append(r.NibbleCount.ToString(Inv)).Append(" hookWindow=").Append(F2(r.HookWindow)).Append('\n');
        }

        private void AppendRoll(FishingRoll r)
        {
            _sb.Append("ROLL ").Append(ItemName(r.Catch.IsJunk, r.Catch.ItemIndex)).Append(' ')
                .Append(r.Catch.WeightG.ToString(Inv)).Append(" g ").Append(r.Catch.Quality)
                .Append(" $").Append(r.Catch.ValueCoins.ToString(Inv)).Append(" t=").Append(F2(r.WeightT))
                .Append(" size=").Append(r.SizeClass.ToString(Inv)).Append(" pity=").Append(r.Pity ? '1' : '0')
                .Append(" seed=0x").Append(r.FightSeed.ToString("X8", Inv)).Append('\n');
        }

        private void AppendLoadout(FishingRodNetwork net)
        {
            ushort count = net.NetSelectedBaitCount.Value;
            _sb.Append("LOADOUT rod=").Append(RodName(net.NetRodIndex.Value))
                .Append(" bait=").Append(BaitName(net.NetSelectedBait.Value))
                .Append(" x").Append(count == ushort.MaxValue ? "inf" : count.ToString(Inv)).Append('\n');
        }

        private void AppendHost()
        {
            NetworkManager nm = NetworkManager.Singleton;
            if (nm == null) return;
            if (!nm.IsServer)
            {
                _sb.Append("HOST n/a (client)\n");
                return;
            }

            FishingHost host = FishingHost.Current;
            FishingHostLedger ledger = host != null ? host.Ledger : null;
            if (host == null || ledger == null)
            {
                _sb.Append("HOST ledger not started");
                if (host != null) _sb.Append(" runSeed=0x").Append(host.RunSeed.ToString("X16", Inv));
                _sb.Append(" forcedBite=").Append(F2(FishingDebugSettings.ForcedBiteWait))
                    .Append(" twitchy=").Append(OnOff(FishingDebugSettings.TwitchyHook))
                    .Append(" species=").Append(ForcedSpeciesText()).Append('\n');
                return;
            }

            _sb.Append("HOST casts=").Append(ledger.HostCastCounter.ToString(Inv))
                .Append(" now=").Append(ledger.Now.ToString("0.0", Inv)).Append("s catches=").Append(ledger.CatchLog.Count.ToString(Inv))
                .Append(" runSeed=0x").Append(ledger.RunSeed.ToString("X16", Inv))
                .Append(" forcedBite=").Append(F2(FishingDebugSettings.ForcedBiteWait))
                .Append(" twitchy=").Append(OnOff(FishingDebugSettings.TwitchyHook))
                .Append(" species=").Append(ForcedSpeciesText()).Append('\n');

            var rods = FishingRodNetwork.Active;
            for (int i = 0; i < rods.Count; i++)
            {
                FishingRodNetwork rod = rods[i];
                if (rod == null || !rod.IsSpawned) continue;
                _sb.Append("  slot ").Append(rod.Player != null ? rod.Player.Slot.ToString(Inv) : "?")
                    .Append(" client ").Append(rod.OwnerClientId.ToString(Inv)).Append(": ");
                if (ledger.TryGetActive(rod.OwnerClientId, out ActiveCast cast))
                    _sb.Append(cast.Phase == CastPhase.Hooked ? "Hooked" : "Waiting").Append(" castId ").Append(cast.CastId.ToString(Inv));
                else
                    _sb.Append("idle");
                _sb.Append('\n');
            }
        }

        // ---- names and formatting ----

        // Bounds-checked instead of try/catch: a stale or mid-sync NetVar index shows its number and never throws from a dev overlay.
        private string RodName(ushort index)
        {
            if (_registry == null || index >= _registry.Rods.Count) return "#" + index.ToString(Inv);
            return _registry.RodAt(index).DisplayName;
        }

        private string BaitName(byte index)
        {
            if (_registry == null || index >= _registry.Baits.Count) return "#" + index.ToString(Inv);
            return _registry.BaitAt(index).DisplayName;
        }

        private string ItemName(bool isJunk, ushort index)
        {
            if (_registry == null) return "?";
            try
            {
                string name = _registry.DisplayNameOf(isJunk, index);
                return string.IsNullOrEmpty(name) ? "?" : name;
            }
            catch (ArgumentOutOfRangeException)
            {
                return "?";
            }
        }

        private static string F1(float value) => value.ToString("0.0", Inv);
        private static string F2(float value) => value.ToString("0.00", Inv);
        private static string OnOff(bool value) => value ? "on" : "off";
    }
}
