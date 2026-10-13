using System;

namespace FishGame.Data
{
    // Content data only (not networked). One entry of a zone's junk list: the item and its draw weight (spec 12.3).
    // Mapped to JunkEntry inside FishingZoneDef.ToCore. Public camelCase fields (M3-D76). Owned by T9 (M3).
    [Serializable]
    public struct JunkWeight
    {
        public JunkDef junk;
        public float weight;
    }
}
