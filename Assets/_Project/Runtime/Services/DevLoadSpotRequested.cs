namespace FishGame.Runtime
{
    // EventBus message; host dev panel -> SceneFlow (M3-D31). SceneFlow loads the spot's scene through NetworkSceneManager on the host,
    // debug builds only; clients ignore it and follow NGO scene sync.
    public readonly struct DevLoadSpotRequested
    {
        public readonly string SpotId;

        public DevLoadSpotRequested(string spotId)
        {
            SpotId = spotId;
        }
    }
}
