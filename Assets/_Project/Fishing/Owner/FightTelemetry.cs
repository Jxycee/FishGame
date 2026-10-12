// Authority: local only (never networked, no RPCs). Scene object on the "Fishing" root; Debug.isDebugBuild only (checked at runtime).
// Appends one CSV row per FightEnded of the local FishingRodController: a cast that reached Waiting, cancels and misses included, so the
// fun gate's cancel rate is computable. The row comes straight from FightEnded (FightRecorder captured the roll and the outcome);
// nothing is re-derived from the machine and FishCaught is never a row source. An IO error logs one warning per component lifetime.
using System;
using System.IO;
using FishGame.Core;
using FishGame.Runtime;
using UnityEngine;

namespace FishGame.Fishing
{
    public sealed class FightTelemetry : MonoBehaviour
    {
        private FishingRodController _attached;
        private bool _active;
        private bool _warned;

        public string FilePath => Path.Combine(Application.persistentDataPath, "fight-log" + InstanceProfile.Current.Suffix + ".csv");

        private void OnEnable()
        {
            if (!Debug.isDebugBuild) return;
            _active = true;
            FishingRodController.LocalChanged += HandleLocalChanged;
            Attach(FishingRodController.Local);
        }

        private void OnDisable()
        {
            if (!_active) return;
            _active = false;
            FishingRodController.LocalChanged -= HandleLocalChanged;
            Attach(null);
        }

        private void HandleLocalChanged(FishingRodController controller)
        {
            Attach(controller);
        }

        private void Attach(FishingRodController controller)
        {
            if (_attached == controller) return;
            if (_attached != null) _attached.FightEnded -= HandleFightEnded;
            _attached = controller;
            if (_attached != null) _attached.FightEnded += HandleFightEnded;
        }

        private void HandleFightEnded(FightEnded e)
        {
            var row = new FightLogRow
            {
                Profile = e.ProfileId,
                Species = e.ItemId,
                WeightG = e.WeightG,
                CastDist = e.CastDistance,
                CastToBite = e.CastToBite,
                FightSec = e.FightSeconds,
                Outcome = e.Outcome,
                Reason = e.Reason,
                NearSnapRecoveries = e.NearSnapRecoveries,
                MaxTension = e.MaxTension,
                Rod = e.RodId,
                Assist = e.Assist,
                Clean = e.CleanClaimed,
                Pity = e.Pity,
            };

            try
            {
                string path = FilePath;
                bool needsHeader = !File.Exists(path) || new FileInfo(path).Length == 0;
                File.AppendAllText(path, (needsHeader ? FightLogCsv.Header + "\n" : "") + FightLogCsv.Format(in row) + "\n");
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                if (_warned) return;
                _warned = true;
                Debug.LogWarning("FightTelemetry: could not write the fight log (" + ex.GetType().Name + "): " + ex.Message);
            }
        }
    }
}
