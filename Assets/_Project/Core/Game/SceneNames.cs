// Core/Game/SceneNames.cs: scene names and build order (spec 4.6, contract 5.2). Bootstrap is always build index 0.
using System;

namespace FishGame.Core
{
    public static class SceneNames
    {
        public const string Bootstrap = "Bootstrap";
        public const string Menu = "Menu";
        public const string SpotLakeDock = "Spot_LakeDock";
        public const string SpotPrefix = "Spot_";
        public static readonly string[] BuildOrder = { Bootstrap, Menu, SpotLakeDock };

        // True when sceneName starts with SpotPrefix (ordinal, case-sensitive). Null -> false.
        public static bool IsGameplay(string sceneName)
        {
            return sceneName != null && sceneName.StartsWith(SpotPrefix, StringComparison.Ordinal);
        }
    }
}
