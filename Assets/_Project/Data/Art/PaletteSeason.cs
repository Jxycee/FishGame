using System;
using UnityEngine;

namespace FishGame.Data
{
    // Content data only (not networked). One season of the 16-slot art palette. Owned by T13.
    [Serializable]
    public sealed class PaletteSeason
    {
        public string name;
        public Color[] colors;
    }
}
