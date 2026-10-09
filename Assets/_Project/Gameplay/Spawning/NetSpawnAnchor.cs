// Authority: scene marker only (not networked). The host's PlayerSpawner reads All and spawns Prefab at each anchor
// with destroyWithScene: true. Clients never spawn from anchors. The list is process-local and is reset on
// SubsystemRegistration so it stays correct with domain reload disabled.
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace FishGame.Gameplay
{
    public sealed class NetSpawnAnchor : MonoBehaviour
    {
        private static readonly List<NetSpawnAnchor> s_all = new List<NetSpawnAnchor>();

        [SerializeField] private NetworkObject prefab;

        public NetworkObject Prefab => prefab;

        public static IReadOnlyList<NetSpawnAnchor> All => s_all;

        // Builder use (editor). The builder persists the field with SerializedObject + ApplyModifiedPropertiesWithoutUndo
        // (or marks the object dirty) before saving the scene.
        public void Configure(NetworkObject prefab)
        {
            this.prefab = prefab;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_all.Clear();
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
    }
}
