// Authority: none (local view). What Presentation and UI draw, on every peer: the owner's live view from FishingRodController,
// remotes from NetRodState smoothed in FishingRodNetwork. Presentation reads it and never writes gameplay state (C 2.8 #17).
using FishGame.Core;
using UnityEngine;

namespace FishGame.Fishing
{
    public struct RodViewState
    {
        public RodState State;
        public byte Flags;            // RodWireQuant.Flag*
        public float Lateral, Tension;
        public Vector3 BobberPos;
        public uint CastId;
        public bool IsOwner;
        public bool RodOut;
    }
}
