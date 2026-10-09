using System;
using System.Collections.Generic;
using System.Globalization;

namespace FishGame.Core
{
    // Dev-only command-line flags (spec 17.1 criteria 3-6; contract 7.3). AppBootstrap (T13) parses
    // DevStartup.CollectArgs() (command line plus MPPM tags split on whitespace) and applies them only in dev builds.
    // Flag names are case-insensitive. Unknown tokens are ignored. A flag whose value is missing or malformed is
    // ignored (left at its default) and its value token is not consumed, so a following flag is still read.
    public sealed class DevFlags
    {
        public static readonly DevFlags None = new DevFlags();

        private DevFlags()
        {
            JoinDirectAddress = NetworkConstants.LoopbackAddress;
            JoinDirectPort = NetworkConstants.DefaultPort;
        }

        public bool Solo { get; private set; }                 // -dev-solo
        public bool HostDirect { get; private set; }           // -dev-host-direct
        public bool HasJoinDirect { get; private set; }        // -dev-join-direct <ip[:port]>
        public string JoinDirectAddress { get; private set; }  // "127.0.0.1" when HasJoinDirect is false
        public ushort JoinDirectPort { get; private set; }     // NetworkConstants.DefaultPort when omitted
        public int ProtocolOffset { get; private set; }        // -dev-protocol-offset <int>, default 0
        public bool LongTimeout { get; private set; }          // -dev-long-timeout

        public static DevFlags Parse(IReadOnlyList<string> args)
        {
            var flags = new DevFlags();
            if (args == null) return flags;

            for (int i = 0; i < args.Count; i++)
            {
                string arg = args[i];
                string next = i + 1 < args.Count ? args[i + 1] : null;

                if (Is(arg, "-dev-solo"))
                {
                    flags.Solo = true;
                }
                else if (Is(arg, "-dev-host-direct"))
                {
                    flags.HostDirect = true;
                }
                else if (Is(arg, "-dev-long-timeout"))
                {
                    flags.LongTimeout = true;
                }
                else if (Is(arg, "-dev-join-direct"))
                {
                    if (TryParseEndpoint(next, out string address, out ushort port))
                    {
                        flags.HasJoinDirect = true;
                        flags.JoinDirectAddress = address;
                        flags.JoinDirectPort = port;
                        i++;
                    }
                }
                else if (Is(arg, "-dev-protocol-offset"))
                {
                    if (next != null && int.TryParse(next, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int offset))
                    {
                        flags.ProtocolOffset = offset;
                        i++;
                    }
                }
            }

            return flags;
        }

        private static bool Is(string arg, string flag)
        {
            return string.Equals(arg, flag, StringComparison.OrdinalIgnoreCase);
        }

        // "a.b.c.d" or "a.b.c.d:port" with a dotted-decimal IPv4 address and a port in 1..65535.
        private static bool TryParseEndpoint(string text, out string address, out ushort port)
        {
            address = NetworkConstants.LoopbackAddress;
            port = NetworkConstants.DefaultPort;
            if (string.IsNullOrEmpty(text)) return false;

            string host = text;
            int colon = text.IndexOf(':');
            if (colon >= 0)
            {
                host = text.Substring(0, colon);
                string portText = text.Substring(colon + 1);
                if (!ushort.TryParse(portText, NumberStyles.None, CultureInfo.InvariantCulture, out ushort parsed) || parsed == 0) return false;
                port = parsed;
            }

            if (!IsIPv4(host)) return false;
            address = host;
            return true;
        }

        private static bool IsIPv4(string text)
        {
            string[] parts = text.Split('.');
            if (parts.Length != 4) return false;
            foreach (string part in parts)
            {
                if (part.Length == 0 || part.Length > 3) return false;
                foreach (char c in part)
                {
                    if (c < '0' || c > '9') return false;
                }
                if (int.Parse(part, CultureInfo.InvariantCulture) > 255) return false;
            }
            return true;
        }
    }
}
