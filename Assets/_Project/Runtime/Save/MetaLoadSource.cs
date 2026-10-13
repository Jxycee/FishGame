namespace FishGame.Runtime
{
    // Where SaveService.Current came from on the last Load (spec 12.10): nothing readable on disk, the main file, or the .bak.
    public enum MetaLoadSource : byte { Defaults = 0, Main = 1, Backup = 2 }
}
