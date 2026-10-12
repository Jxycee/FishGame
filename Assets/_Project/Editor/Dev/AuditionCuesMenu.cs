// FishGame > Dev > Audition All Cues (C 6.5, T11). Authority: none (editor tool).
// Edit mode: generates every SfxRecipes.All recipe and checks the spec 11.7 rules; logs "FG AUDITION PASS count=N" or "FG AUDITION FAIL failed=N".
// Batchmode: -executeMethod FishGame.EditorTools.AuditionCuesMenu.Audition (exit code 1 on failure).
// Play mode: plays every cue once, 0.8 s apart (loops for 1.5 s) through the live AudioBus, then logs "FG AUDITION PLAYED count=N".
using System;
using System.Collections.Generic;
using FishGame.Core;
using FishGame.Runtime;
using UnityEditor;
using UnityEngine;

namespace FishGame.EditorTools
{
    public static class AuditionCuesMenu
    {
        private const ulong AuditionSeed = 20261011UL;
        private const double CueSpacingSeconds = 0.8;
        private const double LoopSeconds = 1.5;
        private const float SeamLimit = 0.05f;

        private static List<SfxRecipe> _queue;
        private static int _next;
        private static double _nextAt;
        private static AudioBus _bus;
        private static AudioLoop _loop;
        private static double _loopStopAt;
        private static int _played;

        [MenuItem("FishGame/Dev/Audition All Cues")]
        public static void Audition()
        {
            if (EditorApplication.isPlaying) StartPlayModeAudition();
            else RunEditModeChecks();
        }

        private static void RunEditModeChecks()
        {
            IReadOnlyList<SfxRecipe> all = SfxRecipes.All;
            int failed = 0;
            for (int i = 0; i < all.Count; i++)
            {
                SfxRecipe recipe = all[i];
                try
                {
                    string problem = Check(recipe);
                    if (problem != null)
                    {
                        failed++;
                        Debug.LogError("FG AUDITION " + recipe.Id + ": " + problem);
                    }
                }
                catch (Exception ex)
                {
                    failed++;
                    Debug.LogError("FG AUDITION " + recipe.Id + " threw " + ex.GetType().Name + ": " + ex.Message);
                }
            }
            if (failed == 0) Debug.Log("FG AUDITION PASS count=" + all.Count);
            else Debug.Log("FG AUDITION FAIL failed=" + failed);
            if (failed > 0 && Application.isBatchMode) EditorApplication.Exit(1);
        }

        // Returns null when the recipe meets the spec 11.7 rules, else the first problem.
        private static string Check(SfxRecipe recipe)
        {
            float[] s = SfxRecipes.Generate(recipe.Id, SfxRecipes.SampleRate, AuditionSeed);
            int expected = SfxRecipes.SampleCount(recipe, SfxRecipes.SampleRate);
            if (s.Length != expected) return "sample count " + s.Length + ", expected " + expected;
            float peak = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                if (float.IsNaN(s[i]) || float.IsInfinity(s[i])) return "NaN or Inf at sample " + i;
                float a = Math.Abs(s[i]);
                if (a > peak) peak = a;
            }
            if (!(peak < 1f)) return "peak " + peak + " is not below 1";
            if (recipe.Loop)
            {
                float seam = Math.Abs(s[s.Length - 1] - s[0]);
                if (!(seam < SeamLimit)) return "loop seam " + seam + " is not below " + SeamLimit;
            }
            return null;
        }

        private static void StartPlayModeAudition()
        {
            ServiceRegistry app = ServiceRoots.App;
            if (app == null || !app.TryGet<AudioBus>(out AudioBus bus) || bus == null)
            {
                Debug.LogWarning("FG AUDITION: no AudioBus in ServiceRoots.App; start the game from the Bootstrap scene first.");
                return;
            }
            if (_queue != null)
            {
                Debug.LogWarning("FG AUDITION: an audition is already running.");
                return;
            }
            _bus = bus;
            _queue = new List<SfxRecipe>(SfxRecipes.All);
            _next = 0;
            _played = 0;
            _loop = null;
            _nextAt = EditorApplication.timeSinceStartup;
            EditorApplication.update += Step;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private static void Step()
        {
            double now = EditorApplication.timeSinceStartup;
            if (_bus == null)
            {
                Finish();
                return;
            }
            if (_loop != null && now >= _loopStopAt)
            {
                try { _bus.StopLoop(_loop); }
                catch (Exception ex) { Debug.LogError("FG AUDITION stop loop threw " + ex.GetType().Name + ": " + ex.Message); }
                _loop = null;
            }
            if (now < _nextAt) return;
            if (_next >= _queue.Count)
            {
                if (_loop != null) return;   // let the last loop run out
                Debug.Log("FG AUDITION PLAYED count=" + _played);
                Finish();
                return;
            }
            SfxRecipe recipe = _queue[_next++];
            _nextAt = now + CueSpacingSeconds;
            try
            {
                if (recipe.Loop)
                {
                    _loop = _bus.StartLoop(recipe.Id, null, false);
                    _loopStopAt = now + LoopSeconds;
                    _nextAt = now + LoopSeconds + 0.1;   // the next cue waits for the loop to stop
                }
                else
                {
                    _bus.Play2D(recipe.Id, recipe.Category);
                }
                _played++;
            }
            catch (Exception ex)
            {
                Debug.LogError("FG AUDITION " + recipe.Id + " threw " + ex.GetType().Name + ": " + ex.Message);
            }
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingPlayMode) Finish();
        }

        private static void Finish()
        {
            EditorApplication.update -= Step;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            _queue = null;
            _bus = null;
            _loop = null;
        }
    }
}
