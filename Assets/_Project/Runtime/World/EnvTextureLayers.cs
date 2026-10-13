// Runtime/World/EnvTextureLayers.cs: layer indices of the R74 env detail texture array (Content/Textures/T_EnvDetailArray.png, imported by
// TextureArrayPostprocessor). Written into uv1.y by SmoothMeshBuilder; FishGame/Toon samples the array at round(uv1.y). Authority: none (data).
// Layer 0 is neutral (0.5 detail = no change), so meshes without uv1.y render untextured.
namespace FishGame.Runtime
{
    public static class EnvTextureLayers
    {
        public const byte None = 0, Grass = 1, Foliage = 2, Wood = 3, Bark = 4, Sand = 5, Rock = 6, Cloth = 7;
        public const byte Face = 8;    // the player's dot eyes and smile, one tile on the head's +Z cube face (not a tiling texture)
        public const int Count = 16;   // 4 x 4 tiles; 9..15 reserved (neutral)
    }
}
