using System;
using System.Collections.Generic;
using System.Text;

namespace FishGame.Runtime
{
    // Per-instance profile name: picks the settings file suffix (and the UGS auth profile in T9), so MPPM clones and
    // several standalone builds on one machine never share settings.json.
    // Rules (flag names compared case-insensitively):
    //   1. "-dev-profile <x>" wins: Name = Sanitize(x), IsMppmClone = false.
    //   2. else "-editor-mode" plus "-name <x>" (MPPM clone; same rule Authentication 3.8.0 uses) with Sanitize(x) non-empty:
    //      Name = Sanitize(x), IsMppmClone = true.
    //   3. else main instance: Name = "", Suffix = "".
    public sealed class InstanceProfile
    {
        private const int MaxNameLength = 30;

        private static InstanceProfile _current;

        private InstanceProfile(string name, bool isMppmClone)
        {
            Name = name;
            Suffix = name.Length == 0 ? "" : "_" + name;
            IsMppmClone = isMppmClone;
        }

        public string Name { get; }
        public string Suffix { get; }
        public bool IsMppmClone { get; }

        // Command-line args never change within a process, so caching survives disabled domain reload safely.
        public static InstanceProfile Current
        {
            get
            {
                if (_current == null) _current = FromArgs(Environment.GetCommandLineArgs());
                return _current;
            }
        }

        public static InstanceProfile FromArgs(IReadOnlyList<string> args)
        {
            if (args == null) return new InstanceProfile("", false);

            bool editorMode = false;
            string cloneName = null;
            for (int i = 0; i < args.Count; i++)
            {
                string arg = args[i];
                if (IsFlag(arg, "-dev-profile") && i + 1 < args.Count)
                    return new InstanceProfile(Sanitize(args[i + 1]), false);
                if (IsFlag(arg, "-editor-mode")) editorMode = true;
                else if (IsFlag(arg, "-name") && i + 1 < args.Count && cloneName == null) cloneName = args[i + 1];
            }

            if (editorMode && cloneName != null)
            {
                string name = Sanitize(cloneName);
                if (name.Length > 0) return new InstanceProfile(name, true);
            }
            return new InstanceProfile("", false);
        }

        // Lowercase; keep [a-z0-9_-]; max 30 chars. Null/empty -> "".
        public static string Sanitize(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "";
            var sb = new StringBuilder(raw.Length);
            foreach (char c in raw.ToLowerInvariant())
            {
                bool keep = (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_' || c == '-';
                if (!keep) continue;
                sb.Append(c);
                if (sb.Length == MaxNameLength) break;
            }
            return sb.ToString();
        }

        private static bool IsFlag(string arg, string flag)
        {
            return string.Equals(arg, flag, StringComparison.OrdinalIgnoreCase);
        }
    }
}
