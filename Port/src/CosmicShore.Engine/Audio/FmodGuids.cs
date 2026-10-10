using System;
using System.Collections.Generic;
using System.IO;

namespace CosmicShore.Engine.Audio.Fmod
{
    /// <summary>
    /// GUID → path for every event, snapshot, bus and VCA, read from the FMOD Studio build's
    /// GUIDs.txt ("{guid} event:/Path" per line). The Unity integration names a GUID-only
    /// <see cref="EventReference"/> from its strings bank; with no runtime installed (parity, tests)
    /// this table does the same, so a reference never records as "(unresolved)".
    /// </summary>
    public static class FmodGuids
    {
        static readonly Dictionary<GUID, string> s_paths = new();

        public static int Count => s_paths.Count;

        /// <summary>Loads (adds) a GUIDs.txt; returns the number of entries read, 0 when the file is absent.</summary>
        public static int Load(string file)
        {
            if (string.IsNullOrEmpty(file) || !File.Exists(file)) return 0;
            int n = 0;
            foreach (var line in File.ReadLines(file))
                if (TryParse(line, out var guid, out var path)) { s_paths[guid] = path; n++; }
            return n;
        }

        public static string PathOf(GUID guid) => !guid.IsNull && s_paths.TryGetValue(guid, out var p) ? p : null;

        public static void Clear() => s_paths.Clear();

        internal static bool TryParse(string line, out GUID guid, out string path)
        {
            guid = default; path = null;
            if (string.IsNullOrWhiteSpace(line)) return false;
            int space = line.IndexOf(' ');
            if (space <= 0 || !Guid.TryParse(line.AsSpan(0, space), out var g)) return false;
            path = line.Substring(space + 1).Trim();
            if (path.Length == 0) return false;
            guid = FromSystem(g);
            return true;
        }

        /// <summary>FMOD.GUID shares System.Guid's memory layout (FMOD.GUID.Parse reads ToByteArray as four ints).</summary>
        public static GUID FromSystem(Guid g)
        {
            var b = g.ToByteArray();
            return new GUID
            {
                Data1 = BitConverter.ToInt32(b, 0), Data2 = BitConverter.ToInt32(b, 4),
                Data3 = BitConverter.ToInt32(b, 8), Data4 = BitConverter.ToInt32(b, 12),
            };
        }
    }
}
