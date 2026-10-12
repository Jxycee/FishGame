using System;
using System.Collections.Generic;
using FishGame.Core;
using UnityEngine;

namespace FishGame.Data
{
    // One mixing row of the cue table. maxDistance 0 means the AudioBus default (30).
    [Serializable]
    public struct SfxCueRow
    {
        public string id;
        public AudioCategory category;
        public float baseVolume, minDistance, maxDistance, minInterval;
        public bool spatial;
    }

    // Content data only (not networked). Mixing overrides for SfxRecipes by id; the clip length and loop flag always stay with Core.
    // Applied by AppBootstrap through AudioBus.ApplyCueTable. Owned by T13.
    public sealed class SfxCueTableDef : ScriptableObject
    {
        [SerializeField] private List<SfxCueRow> rows = new List<SfxCueRow>();

        public IReadOnlyList<SfxCueRow> Rows => rows;

        // Used by the content builder and by tests.
        public void Configure(IEnumerable<SfxCueRow> rows)
        {
            this.rows = rows == null ? new List<SfxCueRow>() : new List<SfxCueRow>(rows);
        }

        // A new list in row order. Id, LengthSeconds and Loop come from the Core recipe; the six mixing fields from the row.
        // Rows whose id is unknown to SfxRecipes are skipped.
        public IReadOnlyList<SfxRecipe> ToRecipes()
        {
            var result = new List<SfxRecipe>(rows.Count);
            for (int i = 0; i < rows.Count; i++)
            {
                SfxCueRow row = rows[i];
                if (!SfxRecipes.TryGet(row.id, out SfxRecipe core)) continue;
                result.Add(new SfxRecipe(core.Id, core.LengthSeconds, core.Loop, row.category, row.baseVolume, row.spatial,
                    row.maxDistance, row.minInterval, row.minDistance));
            }
            return result;
        }
    }
}
