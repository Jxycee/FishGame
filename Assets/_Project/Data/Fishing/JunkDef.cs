using FishGame.Core;
using UnityEngine;

namespace FishGame.Data
{
    // Content data only (not networked). One junk item (old boot, tin can). Owned by T13.
    public sealed class JunkDef : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private int valueCoins;
        [SerializeField] private BodyShape bodyShape;
        [SerializeField] private Color color;

        public string Id => id;
        public string DisplayName => displayName;
        public int ValueCoins => valueCoins;
        public BodyShape BodyShape => bodyShape;
        public Color Color => color;

        // Used by the content builder and by tests.
        public void Configure(string id, string displayName, int valueCoins, BodyShape bodyShape, Color color)
        {
            this.id = id;
            this.displayName = displayName;
            this.valueCoins = valueCoins;
            this.bodyShape = bodyShape;
            this.color = color;
        }

        public JunkData ToCore()
        {
            return new JunkData { ValueCoins = valueCoins };
        }
    }
}
