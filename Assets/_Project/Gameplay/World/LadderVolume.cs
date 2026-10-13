using System.Collections.Generic;
using UnityEngine;

namespace FishGame.Gameplay
{
    // Authority: none (static world geometry, present on every peer). A walk-in ladder (spec 7.3, R70; contract M2-D29): while the owner's
    // capsule centre is inside this trigger box, PlayerLocomotion climbs. Nothing here is networked: the owner's movement replicates through
    // its owner-authority NetworkTransform like all locomotion, and no ladder state goes on the wire (no NetClimbing in M2, M2-D32).
    // Needs one BoxCollider (isTrigger) and no rotation on this object or its parents. Built by LakeDockBuilder on layer Default, so
    // CastProbe's water mask never sees it. No RequireComponent on purpose (see WaterVolume).
    public sealed class LadderVolume : MonoBehaviour
    {
        // A point no more than this far below the top face counts as "at the top" (the re-entry grace, R70).
        public const float TopEpsilon = 0.05f;

        private static readonly List<LadderVolume> s_all = new List<LadderVolume>();
        private BoxCollider _box;

        public static IReadOnlyList<LadderVolume> All => s_all;

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
                Debug.LogWarning($"LadderVolume '{name}' has no BoxCollider.", this);
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

        // Local-space test against the BoxCollider's centre and size (not Collider.bounds, which can be stale right after a scene opens in an
        // EditMode test). Points on a face count as inside.
        public bool Contains(Vector3 worldPoint)
        {
            if (!TryLocal(worldPoint, out Vector3 d, out Vector3 half))
            {
                return false;
            }

            return Mathf.Abs(d.x) <= half.x && Mathf.Abs(d.y) <= half.y && Mathf.Abs(d.z) <= half.z;
        }

        // True when the point is level with or above the top face, or within TopEpsilon below it. Read on the frame a climb ends.
        public bool IsAtOrAboveTop(Vector3 worldPoint)
        {
            return TryLocal(worldPoint, out Vector3 d, out Vector3 half) && d.y >= half.y - TopEpsilon;
        }

        public static LadderVolume FindContaining(Vector3 worldPoint)
        {
            for (int i = 0; i < s_all.Count; i++)
            {
                LadderVolume volume = s_all[i];
                if (volume != null && volume.Contains(worldPoint))
                {
                    return volume;
                }
            }

            return null;
        }

        private bool TryLocal(Vector3 worldPoint, out Vector3 delta, out Vector3 half)
        {
            if (_box == null)
            {
                _box = GetComponent<BoxCollider>();
            }

            if (_box == null)
            {
                delta = Vector3.zero;
                half = Vector3.zero;
                return false;
            }

            delta = transform.InverseTransformPoint(worldPoint) - _box.center;
            half = _box.size * 0.5f;
            return true;
        }
    }
}
