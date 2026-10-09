// Text access to persisted files for the validators (YAML settings, manifest, scenes, prefabs). Owned by T15.
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace FishGame.EditorTools
{
    internal static class ValidationText
    {
        public static string Read(string projectRelativePath)
        {
            string abs = BuilderUtil.Abs(projectRelativePath);
            return File.Exists(abs) ? File.ReadAllText(abs) : null;
        }

        // Each entry is "key: value". It must match a whole YAML line (leading spaces allowed).
        public static void ExpectLines(string projectRelativePath, List<string> errors, params string[] keyValues)
        {
            string text = Read(projectRelativePath);
            if (text == null) { errors.Add("missing " + projectRelativePath); return; }
            foreach (string keyValue in keyValues)
            {
                int colon = keyValue.IndexOf(':');
                string key = keyValue.Substring(0, colon);
                string value = keyValue.Substring(colon + 1).Trim();
                string pattern = "^\\s*" + Regex.Escape(key) + ":\\s*" + Regex.Escape(value) + "\\s*$";
                if (!Regex.IsMatch(text, pattern, RegexOptions.Multiline))
                    errors.Add(projectRelativePath + " must contain '" + keyValue + "'");
            }
        }

        // First capture group of pattern in the file, or null.
        public static string FirstGroup(string projectRelativePath, string pattern)
        {
            string text = Read(projectRelativePath);
            if (text == null) return null;
            Match match = Regex.Match(text, pattern, RegexOptions.Multiline);
            return match.Success ? match.Groups[1].Value : null;
        }
    }
}
