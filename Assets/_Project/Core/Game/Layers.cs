// Core/Game/Layers.cs: physics layer indices (spec 4.6, contract 5.3). Layer 4 is Unity's built-in "Water".
// ProjectSettingsBuilder (T15) writes UserLayerNames into UserLayerIndices; the two arrays are index-aligned.
namespace FishGame.Core
{
    public static class Layers
    {
        public const int Default = 0;
        public const int Water = 4;          // built-in name "Water" (already present)
        public const int Player = 6;         // local owner's root (CharacterController)
        public const int RemotePlayer = 7;   // remote roots (CapsuleCollider)
        public const int Ground = 8;         // lakebed
        public const int Shore = 9;          // banks, rocks, trees
        public const int Structure = 10;     // dock, posts, ladders, ramp
        public const int Interactable = 11;  // DockBell collider
        public const int Fish = 12;          // M5, named now
        public const int Ragdoll = 13;       // M5, named now
        public static readonly int[] UserLayerIndices = { 6, 7, 8, 9, 10, 11, 12, 13 };
        public static readonly string[] UserLayerNames = { "Player", "RemotePlayer", "Ground", "Shore", "Structure", "Interactable", "Fish", "Ragdoll" };
        public static int Mask(int layer) => 1 << layer;
    }
}
