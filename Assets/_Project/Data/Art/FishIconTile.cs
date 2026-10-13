using System;

namespace FishGame.Data
{
    // Content data only (not networked). One tile of the baked journal icon atlas (T_FishIcons.png, written by FishIconBaker): the species id and
    // the tile's pixel rectangle in the atlas texture, origin bottom-left (the texture's own space). Public camelCase fields (M3-D76).
    [Serializable]
    public struct FishIconTile
    {
        public string id;
        public int x, y, w, h;
    }
}
