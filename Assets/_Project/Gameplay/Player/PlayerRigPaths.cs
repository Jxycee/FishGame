namespace FishGame.Gameplay
{
    // Authority: none (names only). Shared with PrefabBuilder (contract 6.3). Keep in sync with the PlayerFP hierarchy.
    public static class PlayerRigPaths
    {
        public const string HeadPivot = "HeadPivot", Shake = "HeadPivot/Shake", Camera = "HeadPivot/Shake/Camera",
            Head = "HeadPivot/Head", Nose = "HeadPivot/Head/Nose", Body = "Body", Torso = "Body/Torso",
            ArmL = "Body/ArmL", ArmR = "Body/ArmR", ArmLMesh = "Body/ArmL/Mesh", ArmRMesh = "Body/ArmR/Mesh",
            LegL = "Body/LegL", LegR = "Body/LegR", LegLMesh = "Body/LegL/Mesh", LegRMesh = "Body/LegR/Mesh",
            NameplateAnchor = "NameplateAnchor";
    }
}
