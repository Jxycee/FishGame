using System;
using System.Collections.Generic;
using FishGame.Core;
using UnityEngine;

namespace FishGame.Runtime
{
    // Authority: none. Client-side presentation audio, generated locally at Bootstrap (AppBootstrap calls GenerateAll once) and cached.
    // Never networked. A recipe that fails to generate is skipped with a warning; its cue then stays silent (spec 11.7).
    public sealed class SfxGenerator
    {
        private readonly int _sampleRate;
        private readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>(StringComparer.Ordinal);

        public SfxGenerator(int sampleRate = SfxRecipes.SampleRate)
        {
            if (sampleRate <= 0) throw new ArgumentOutOfRangeException(nameof(sampleRate), sampleRate, "Sample rate must be positive.");
            _sampleRate = sampleRate;
        }

        public void GenerateAll(ulong seed)
        {
            IReadOnlyList<SfxRecipe> recipes = SfxRecipes.All;
            for (int i = 0; i < recipes.Count; i++)
            {
                SfxRecipe recipe = recipes[i];
                try
                {
                    int n = SfxRecipes.SampleCount(recipe, _sampleRate);
                    float[] data = SfxRecipes.Generate(recipe.Id, _sampleRate, seed);
                    if (data.Length != n)
                    {
                        Debug.LogWarning($"SFX '{recipe.Id}' produced {data.Length} samples, expected {n}; staying silent.");
                        continue;
                    }

                    AudioClip clip = AudioClip.Create(recipe.Id, n, 1, _sampleRate, false);
                    if (!clip.SetData(data, 0))
                    {
                        Debug.LogWarning($"SFX '{recipe.Id}' SetData failed; staying silent.");
                        continue;
                    }
                    _clips[recipe.Id] = clip;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"SFX '{recipe.Id}' failed to generate; staying silent. {ex.GetType().Name}: {ex.Message}");
                }
            }
        }

        public bool TryGetClip(string id, out AudioClip clip)
        {
            if (id != null && _clips.TryGetValue(id, out clip) && clip != null) return true;
            clip = null;
            return false;
        }
    }
}
