using UnityEngine;

namespace FishGame.Data
{
    // Content data only (not networked). Peers refer to spots by ContentRegistry index, never by asset reference.
    // M1 fields; M3 adds mapOrder and zone; M4 adds more.
    public sealed class SpotDef : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private string sceneName;
        [SerializeField] private int mapOrder;
        [SerializeField] private FishingZoneDef zone;

        public string Id => id;
        public string DisplayName => displayName;
        public string SceneName => sceneName;
        public int MapOrder => mapOrder;
        public FishingZoneDef Zone => zone;

        // Used by the T15 ContentBuilder and by tests. Sets only the M1 fields: mapOrder and zone are left untouched.
        public void Configure(string id, string displayName, string sceneName)
        {
            this.id = id;
            this.displayName = displayName;
            this.sceneName = sceneName;
        }

        // M3 (T9): all five fields.
        public void Configure(string id, string displayName, string sceneName, int mapOrder, FishingZoneDef zone)
        {
            Configure(id, displayName, sceneName);
            this.mapOrder = mapOrder;
            this.zone = zone;
        }
    }
}
