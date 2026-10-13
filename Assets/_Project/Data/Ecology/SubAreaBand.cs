using System;
using FishGame.Core;

namespace FishGame.Data
{
    // Content data only (not networked). One shore-distance sub-area of a zone (spec 12.5): minShoreM <= distance < maxShoreM.
    // maxShoreM <= 0 on the LAST band means open-ended (FishingZoneDef.ToCore turns it into +Infinity). Public camelCase fields
    // (M3-D76, the M2 SfxCueRow precedent). Owned by T9 (M3).
    [Serializable]
    public struct SubAreaBand
    {
        public string id;
        public float minShoreM, maxShoreM, junkChance;

        // Copies the values as given; the zone decides open-endedness.
        public SubAreaBandData ToCore()
        {
            return new SubAreaBandData { MinShoreM = minShoreM, MaxShoreM = maxShoreM, JunkChance = junkChance };
        }
    }
}
