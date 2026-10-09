using UnityEngine;

namespace FishGame.Data
{
    // Content data only (not networked). Peers refer to spots by ContentRegistry index, never by asset reference.
    // M1 fields only; M3/M4 add more.
    public sealed class SpotDef : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private string sceneName;

        public string Id => id;
        public string DisplayName => displayName;
        public string SceneName => sceneName;

        // Used by the T15 ContentBuilder and by tests.
        public void Configure(string id, string displayName, string sceneName)
        {
            this.id = id;
            this.displayName = displayName;
            this.sceneName = sceneName;
        }
    }
}
