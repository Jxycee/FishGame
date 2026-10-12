namespace FishGame.Core
{
    public readonly struct SfxRecipe
    {
        public readonly string Id;
        public readonly float LengthSeconds;
        public readonly bool Loop;
        public readonly AudioCategory Category;
        public readonly float BaseVolume;
        public readonly bool Spatial;
        public readonly float MaxDistance;   // 0 means the AudioBus default (30)
        public readonly float MinInterval;
        public readonly float MinDistance;   // AudioSource.minDistance (spatial full-volume radius); 1 for every M1 recipe

        // M1 constructor: MinDistance 1.
        public SfxRecipe(string id, float lengthSeconds, bool loop, AudioCategory category,
            float baseVolume, bool spatial, float maxDistance, float minInterval)
            : this(id, lengthSeconds, loop, category, baseVolume, spatial, maxDistance, minInterval, 1f)
        {
        }

        // minDistance is LAST (ruling C-Q1): the first eight parameters mean the same in both constructors.
        public SfxRecipe(string id, float lengthSeconds, bool loop, AudioCategory category,
            float baseVolume, bool spatial, float maxDistance, float minInterval, float minDistance)
        {
            Id = id;
            LengthSeconds = lengthSeconds;
            Loop = loop;
            Category = category;
            BaseVolume = baseVolume;
            Spatial = spatial;
            MaxDistance = maxDistance;
            MinInterval = minInterval;
            MinDistance = minDistance;
        }
    }
}
