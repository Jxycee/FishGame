using System.Collections.Generic;
using UnityEngine;

namespace FishGame.Gameplay
{
    // Authority: none (static world geometry, identical on every peer). Registry of water trigger boxes.
    // PlayerLocomotion uses FindContaining for swim and wade detection (contract D25: collider bounds, not OnTrigger).
    // Needs exactly one trigger BoxCollider on the same GameObject (added by SceneBuilder; no RequireComponent on purpose,
    // so a builder that adds this script first can never end up with a second, solid box collider).
    public sealed class WaterVolume : MonoBehaviour
    {
        private static readonly List<WaterVolume> s_all = new List<WaterVolume>();
        private BoxCollider _box;

        public static IReadOnlyList<WaterVolume> All => s_all;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_all.Clear();
        }

        private void Awake()
        {
            _box = GetComponent<BoxCollider>();
            if (_box == null)
            {
                Debug.LogWarning($"WaterVolume '{name}' has no BoxCollider.", this);
            }
        }

        private void OnEnable()
        {
            if (!s_all.Contains(this))
            {
                s_all.Add(this);
            }
        }

        private void OnDisable()
        {
            s_all.Remove(this);
        }

        // World-space AABB of the BoxCollider.
        public bool Contains(Vector3 point)
        {
            if (_box == null)
            {
                _box = GetComponent<BoxCollider>();
            }

            return _box != null && _box.bounds.Contains(point);
        }

        public static WaterVolume FindContaining(Vector3 point)
        {
            for (int i = 0; i < s_all.Count; i++)
            {
                WaterVolume volume = s_all[i];
                if (volume != null && volume.Contains(point))
                {
                    return volume;
                }
            }

            return null;
        }
    }
}
