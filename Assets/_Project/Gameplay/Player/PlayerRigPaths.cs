namespace FishGame.Gameplay
{
    // Authority: none (names only). Shared with PrefabBuilder (contract 6.3). Keep in sync with the PlayerFP hierarchy.
    // The fishing rig paths (T16, C 7.16) are built by PlayerFishingRigBuilder (T19). RodTipAnchor is STATIC (gameplay tip: the fish point
    // and TipToBobber read it); RodTipVisual is bent by RodPresenter and is where the line is drawn from (ruling BF-3).
    public static class PlayerRigPaths
    {
        public const string HeadPivot = "HeadPivot", Shake = "HeadPivot/Shake", Camera = "HeadPivot/Shake/Camera",
            Head = "HeadPivot/Head", Nose = "HeadPivot/Head/Nose", Body = "Body", Torso = "Body/Torso",
            ArmL = "Body/ArmL", ArmR = "Body/ArmR", ArmLMesh = "Body/ArmL/Mesh", ArmRMesh = "Body/ArmR/Mesh",
            LegL = "Body/LegL", LegR = "Body/LegR", LegLMesh = "Body/LegL/Mesh", LegRMesh = "Body/LegR/Mesh",
            NameplateAnchor = "NameplateAnchor";

        public const string Hand = "Body/ArmR/Hand", Rod = "Body/ArmR/Hand/Rod", RodTipAnchor = "Body/ArmR/Hand/Rod/TipAnchor",
            RodTipVisual = "Body/ArmR/Hand/Rod/TipVisual", RodSegmentPrefix = "Body/ArmR/Hand/Rod/Seg", CatchHold = "Body/ArmR/Hand/CatchHold",
            Line = "Line", Bobber = "Bobber", CastMarker = "CastMarker";

        public const int RodSegmentCount = 6;
    }
}
