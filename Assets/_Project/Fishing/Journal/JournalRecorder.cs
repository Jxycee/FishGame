// Authority: local only (every peer). Scene object on "Fishing". Applies every CatchAnnounced (App EventBus) to the local SaveService.
// Every peer applies every announcement it receives; late joiners journal only what they witness (spec 12.10).
// Sends nothing over the network (no RPCs). Toasts are published as keys (Fishing cannot reference UI) and stay local.
using System;
using System.Collections;
using System.Globalization;
using FishGame.Core;
using FishGame.Data;
using FishGame.Runtime;
using UnityEngine;

namespace FishGame.Fishing
{
    [DisallowMultipleComponent]
    public sealed class JournalRecorder : MonoBehaviour
    {
        private const float ToastSeconds = 4f;
        internal const float FlushDelaySeconds = 3f;

        private const string ToastNewKey = "toast.journal_new";
        private const string ToastBestKey = "toast.journal_best";
        private const string ToastUnlockKey = "toast.journal_unlock";

        private readonly LogOnce _log = new LogOnce();
        private IDisposable _subscription;
        private SaveService _pendingSave;
        private Coroutine _flushRoutine;

        private void OnEnable()
        {
            ServiceRegistry app = ServiceRoots.App;
            if (app == null || !app.TryGet(out EventBus bus))
            {
                if (_log.ShouldLog("no-bus")) Debug.Log("FG SAVE JournalRecorder: no App EventBus; catches are not journaled in this scene.", this);
                return;
            }
            _subscription = bus.Subscribe<CatchAnnounced>(HandleCatchAnnounced);
        }

        private void OnDisable()
        {
            if (_subscription != null)
            {
                _subscription.Dispose();
                _subscription = null;
            }
            // Leaving the scene (or quitting) must not lose a pending write; AppBootstrap also saves on quit as a second net.
            // The pending coroutine is stopped by Unity on disable, so clear the handle and flush now.
            _flushRoutine = null;
            if (_pendingSave != null)
            {
                _pendingSave.FlushIfDirty();
                _pendingSave = null;
            }
        }

        // The journal write is debounced: the first change in a quiet period starts a FlushDelaySeconds timer and every change until it
        // fires rides on the same write, so a busy session costs one disk flush per window instead of one per catch on every peer.
        // At most FlushDelaySeconds of journal can be lost to a hard crash; scene unload and quit flush at once.
        private void ScheduleFlush(SaveService save)
        {
            if (_pendingSave != null && _pendingSave != save) _pendingSave.FlushIfDirty(); // service replaced mid-window: do not strand its write
            save.MarkDirty();
            _pendingSave = save;
            if (_flushRoutine == null) _flushRoutine = StartCoroutine(FlushAfterDelay());
        }

        private IEnumerator FlushAfterDelay()
        {
            yield return new WaitForSecondsRealtime(FlushDelaySeconds);
            _flushRoutine = null;
            if (_pendingSave != null)
            {
                _pendingSave.FlushIfDirty();
                _pendingSave = null;
            }
        }

        private void HandleCatchAnnounced(CatchAnnounced msg)
        {
            CatchAnnouncement a = msg.Announcement;
            if (!JournalRules.ShouldJournal(a.Kind, a.Catch.IsJunk)) return;

            // Resolved per announcement (cheap) so a service replaced by a test is honoured.
            ServiceRegistry app = ServiceRoots.App;
            if (app == null || !app.TryGet(out ContentRegistry registry) || !app.TryGet(out SaveService save))
            {
                if (_log.ShouldLog("no-services")) Debug.Log("FG SAVE JournalRecorder: no ContentRegistry or SaveService in App; catch not journaled.", this);
                return;
            }
            if (a.Catch.ItemIndex >= registry.Species.Count)
            {
                if (_log.ShouldLog("bad-index")) Debug.Log("FG SAVE JournalRecorder: species index " + a.Catch.ItemIndex + " is out of range; catch not journaled.", this);
                return;
            }
            FishSpeciesDef def = registry.SpeciesAt(a.Catch.ItemIndex);

            bool changed = Record(save.Current, def.Id, def.DisplayName, ResolveSpotId(registry), a, msg.IsLocalCatcher, out string key, out string arg);
            if (changed)
            {
                save.NotifyChanged();   // the UI follows immediately
                ScheduleFlush(save);    // the disk write is deferred (see ScheduleFlush)
            }
            if (key != null && app.TryGet(out EventBus bus)) bus.Publish(new ToastRequested(key, arg, ToastSeconds));
        }

        // The serialized spot id of the loaded world (valid even before the first NetworkVariable sync), else the spot of this scene.
        private string ResolveSpotId(ContentRegistry registry)
        {
            ServiceRegistry game = ServiceRoots.Game;
            if (game != null && game.TryGet(out IWorldContext world) && !string.IsNullOrEmpty(world.SpotId)) return world.SpotId;
            if (registry.TryGetSpotByScene(gameObject.scene.name, out SpotDef spot)) return spot.Id;
            return null;
        }

        // Pure part of the handler (no Unity calls): applies the announcement to the save and picks at most one local toast by priority
        // new > personal best > unlock. Returns JournalUpdate.Changed; toastKey null means no toast.
        internal static bool Record(MetaSave save, string speciesId, string speciesName, string spotId, in CatchAnnouncement a, bool isLocalCatcher,
                                    out string toastKey, out string toastArg)
        {
            JournalUpdate u = JournalRules.Apply(save, speciesId, spotId, a.Kind, a.Catch.IsJunk, isLocalCatcher,
                                                 a.Catch.WeightG, a.Catch.LengthMm, a.Catch.Quality);
            toastKey = null;
            toastArg = null;
            if (u.FirstCatch)
            {
                toastKey = ToastNewKey;
                toastArg = speciesName;
            }
            else if (u.PersonalBest)
            {
                toastKey = ToastBestKey;
                toastArg = speciesName + " " + (a.Catch.WeightG / 1000f).ToString("0.0", CultureInfo.InvariantCulture) + " kg";
            }
            else if (u.LevelAfter > u.LevelBefore)
            {
                toastKey = ToastUnlockKey;
                toastArg = speciesName;
            }
            return u.Changed;
        }
    }
}
