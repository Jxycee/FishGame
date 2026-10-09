// Implemented by PlayerNetState (T11). PlayerSpawner calls it on the host right after the player object is spawned.
namespace FishGame.Gameplay
{
    public interface IServerPlayerInit
    {
        void ServerInit(byte slot, string displayName);
    }
}
