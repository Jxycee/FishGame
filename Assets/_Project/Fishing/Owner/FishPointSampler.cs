// Authority: owner only (plain C#). Where the fish is during a fight (spec 9.13): the candidate from FishPointMath each frame, probed for
// water every FishingConstants.FishPointSampleSeconds; a non-water candidate holds the last water point. y is the flat water height.
// The tip is the STATIC TipAnchor (BF-3): there is no tension -> bend -> fish point feedback.
using System;
using FishGame.Core;
using FishGame.Runtime;
using UnityEngine;

namespace FishGame.Fishing
{
    internal sealed class FishPointSampler
    {
        private readonly Func<float, float, bool> _isWater;
        private Vector3 _lastWater;
        private bool _valid = true;
        private float _sinceProbe;

        // `isWater` is the test seam; the default probes the physics world with CastProbe.
        public FishPointSampler(Func<float, float, bool> isWater = null)
        {
            _isWater = isWater ?? DefaultIsWater;
        }

        public void Begin(Vector3 landing)
        {
            _lastWater = landing;
            _valid = true;
            _sinceProbe = 0f;
        }

        public Vector3 Tick(float dt, float tipX, float tipZ, float castYawDeg, float lateral, float lateralMaxDeg, float lineLength)
        {
            FishPointMath.FishPoint(tipX, tipZ, castYawDeg, lateral, lateralMaxDeg, lineLength, out float x, out float z);
            var candidate = new Vector3(x, WaterSurface.FlatHeight, z);
            if (float.IsNaN(x) || float.IsInfinity(x) || float.IsNaN(z) || float.IsInfinity(z)) return _lastWater;

            _sinceProbe += dt;
            if (_sinceProbe >= FishingConstants.FishPointSampleSeconds)
            {
                _sinceProbe = 0f;
                if (_isWater(x, z))
                {
                    _valid = true;
                    _lastWater = candidate;
                }
                else
                {
                    _valid = false;
                }
            }

            return _valid ? candidate : _lastWater;
        }

        private static bool DefaultIsWater(float x, float z)
        {
            return CastProbe.Probe(x, z).FirstHitIsWater;
        }
    }
}
