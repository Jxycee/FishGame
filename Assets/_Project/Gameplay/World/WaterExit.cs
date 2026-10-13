using System.Collections.Generic;
using UnityEngine;

namespace FishGame.Gameplay
{
    // Authority: none (static world geometry). Since R70 only the shore ramp has one: the ramp foot (PlayerLocomotion, contract D36 and
    // M2-D30). Inside it a player in water wades instead of swimming, and a swimmer arriving while pushing toward TopPoint gets one small
    // upward kick (Core RampAssist). Needs exactly one trigger BoxCollider and a child named "Top" (added by LakeDockBuilder; no
    // RequireComponent on purpose, see WaterVolume). Ladders are LadderVolumes.
    public sealed class WaterExit : MonoBehaviour
    {
        public const string TopChildName = "Top";

        private static readonly List<WaterExit> s_all = new List<WaterExit>();
        private BoxCollider _box;
        private Transform _top;

        public static IReadOnlyList<WaterExit> All => s_all;

        public Vector3 TopPoint => _top != null ? _top.position : transform.position;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_all.Clear();
        }

        private void Awake()
        {
            _box = GetComponent<BoxCollider>();
            _top = transform.Find(TopChildName);
            if (_box == null)
            {
                Debug.LogWarning($"WaterExit '{name}' has no BoxCollider.", this);
            }

            if (_top == null)
            {
                Debug.LogWarning($"WaterExit '{name}' has no child '{TopChildName}'.", this);
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

        public static WaterExit FindContaining(Vector3 point)
        {
            for (int i = 0; i < s_all.Count; i++)
            {
                WaterExit exit = s_all[i];
                if (exit != null && exit.Contains(point))
                {
                    return exit;
                }
            }

            return null;
        }
    }
}
