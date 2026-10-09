using System;
using System.IO;
using System.Text;

namespace FishGame.Runtime
{
    // Atomic text write: path + ".tmp" (UTF-8 without BOM, flushed to disk), then
    // File.Replace(tmp, path, path + ".bak") when path exists, else File.Move(tmp, path).
    // The previous good file therefore survives as path + ".bak".
    public static class AtomicFile
    {
        public const string TempSuffix = ".tmp";
        public const string BackupSuffix = ".bak";

        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

        public static void WriteAllText(string path, string contents)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentException("path is required", nameof(path));
            string directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            string temp = path + TempSuffix;
            byte[] bytes = Utf8NoBom.GetBytes(contents ?? "");
            using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true); // flush OS buffers to disk before the swap
            }

            try
            {
                if (File.Exists(path)) File.Replace(temp, path, path + BackupSuffix);
                else File.Move(temp, path);
            }
            catch
            {
                TryDelete(temp);
                throw;
            }
        }

        // Null when the file is missing or cannot be read.
        public static string ReadAllTextOrNull(string path)
        {
            try
            {
                return File.Exists(path) ? File.ReadAllText(path, Utf8NoBom) : null;
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
