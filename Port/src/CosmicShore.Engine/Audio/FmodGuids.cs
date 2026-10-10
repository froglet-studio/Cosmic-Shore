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
    ///
    /// <para>GUIDs.txt is only as current as the last File > Export GUIDs in FMOD Studio, while the
    /// banks are rebuilt every Build. Which events the loaded banks CARRY is therefore read from
    /// the strings bank itself (<see cref="LoadStringsBank"/>): measured 2026-10-10, the project's
    /// GUIDs.txt lists 75 entries and Master.strings.bank 89; the Bootstrap music
    /// {03de9ea9-9b51-400a-b0cb-8bcc12a12697} and seven other serialized references are in the
    /// bank and not in the file, and one retired event (Mass brittle star) is in the file and not
    /// in the bank. <see cref="BankCarries"/> is the silent model's answer to "does a loaded bank
    /// have this event", the question FMOD's RuntimeManager asks before every CreateInstance.</para>
    /// </summary>
    public static class FmodGuids
    {
        static readonly Dictionary<GUID, string> s_paths = new();
        static readonly Dictionary<string, GUID> s_guids = new();
        static HashSet<GUID> s_bank;
        static string s_bankFile;

        public static int Count => s_paths.Count;

        /// <summary>Loads (adds) a GUIDs.txt; returns the number of entries read, 0 when the file is absent.</summary>
        public static int Load(string file)
        {
            if (string.IsNullOrEmpty(file) || !File.Exists(file)) return 0;
            int n = 0;
            foreach (var line in File.ReadLines(file))
                if (TryParse(line, out var guid, out var path)) { s_paths[guid] = path; s_guids[path] = guid; n++; }
            return n;
        }

        public static string PathOf(GUID guid) => !guid.IsNull && s_paths.TryGetValue(guid, out var p) ? p : null;

        /// <summary>The GUID listed for <paramref name="path"/>, or default when the file does not name it.</summary>
        public static GUID GuidOf(string path) => path != null && s_guids.TryGetValue(path, out var g) ? g : default;

        /// <summary>Every path the loaded file names (banks, buses, events, snapshots).</summary>
        public static IEnumerable<string> Paths => s_paths.Values;

        public static void Clear()
        {
            s_paths.Clear();
            s_guids.Clear();
            ClearStringsBank();
        }

        // ── The strings bank: what the loaded banks carry ────────────────────────────────

        /// <summary>True once a strings bank's GUID index was read; until then <see cref="BankCarries"/> cannot answer.</summary>
        public static bool StringsBankLoaded => s_bank != null;

        /// <summary>GUIDs the loaded strings bank indexes (events, snapshots, buses, VCAs, parameters, banks).</summary>
        public static int StringsBankCount => s_bank?.Count ?? 0;

        /// <summary>The strings bank file that was read, for messages.</summary>
        public static string StringsBankFile => s_bankFile;

        /// <summary>
        /// Reads the GUID index of an FMOD Studio strings bank (Master.strings.bank). Returns the
        /// number of GUIDs indexed, 0 when the file is absent or holds no index (nothing is then
        /// loaded, and <see cref="BankCarries"/> keeps answering null).
        /// </summary>
        public static int LoadStringsBank(string file)
        {
            if (string.IsNullOrEmpty(file) || !File.Exists(file)) return 0;
            var guids = ReadGuidIndex(File.ReadAllBytes(file));
            if (guids.Count == 0) return 0;
            s_bank = guids;
            s_bankFile = file;
            return guids.Count;
        }

        public static void ClearStringsBank() { s_bank = null; s_bankFile = null; }

        /// <summary>
        /// Whether a loaded bank carries the event, as FMOD's RuntimeManager resolves a reference:
        /// by GUID when the reference has one (the serialized path is the editor's label and can be
        /// stale), else by the GUID GUIDs.txt lists for the path. Null when no strings bank was read,
        /// or when a path-only reference names a path GUIDs.txt does not know (the strings bank's
        /// own path table is not decoded, so a path is never refused on the strength of the file).
        /// </summary>
        public static bool? BankCarries(EventReference reference)
        {
            if (s_bank == null) return null;
            if (!reference.Guid.IsNull) return s_bank.Contains(reference.Guid);
            var g = GuidOf(reference.Path);
            return g.IsNull ? null : s_bank.Contains(g);
        }

        public static bool? BankCarries(GUID guid) => s_bank == null ? null : s_bank.Contains(guid);

        /// <summary>
        /// How many of the GUIDs.txt entries the strings bank indexes: (listed, carried). A bank
        /// whose index misses most of the file was not read correctly, and the caller drops it.
        /// </summary>
        public static (int listed, int carried) StringsBankCoverage()
        {
            if (s_bank == null) return (s_paths.Count, 0);
            int carried = 0;
            foreach (var g in s_paths.Keys) if (s_bank.Contains(g)) carried++;
            return (s_paths.Count, carried);
        }

        /// <summary>
        /// The GUID index inside a strings bank's bytes. The bank keeps one 16-byte GUID per entry
        /// in one array, sorted ascending by the first 32 bits (Data1 as an unsigned little-endian
        /// int, the order FMOD binary-searches); the path strings sit in a packed table after it
        /// that this reader does not decode. The array is found as the longest run of consecutive
        /// 16-byte slots, at any of the 16 alignments, whose Data1 never decreases: a run a packed
        /// string region produces by chance is a few slots long, the index is dozens. A leading
        /// slot with Data1 of zero that is not a version-4 GUID is the tail of the table before the
        /// array and is dropped. Fewer than <see cref="MinimumIndexEntries"/> entries is no index.
        /// </summary>
        public static HashSet<GUID> ReadGuidIndex(ReadOnlySpan<byte> bytes)
        {
            int bestLength = 0, bestStart = 0;
            for (int align = 0; align < 16; align++)
            {
                int run = 0, runStart = align;
                uint previous = 0;
                for (int o = align; o + 16 <= bytes.Length; o += 16)
                {
                    uint data1 = BitConverter.ToUInt32(bytes.Slice(o, 4));
                    if (run > 0 && data1 >= previous) run++;
                    else { run = 1; runStart = o; }
                    previous = data1;
                    if (run > bestLength) { bestLength = run; bestStart = runStart; }
                }
            }
            var result = new HashSet<GUID>();
            if (bestLength < MinimumIndexEntries) return result;
            int start = bestStart;
            if (BitConverter.ToUInt32(bytes.Slice(start, 4)) == 0 && (bytes[start + 7] >> 4) != 4) { start += 16; bestLength--; }
            for (int i = 0; i < bestLength; i++)
            {
                var slot = bytes.Slice(start + 16 * i, 16);
                var guid = new GUID
                {
                    Data1 = BitConverter.ToInt32(slot.Slice(0, 4)), Data2 = BitConverter.ToInt32(slot.Slice(4, 4)),
                    Data3 = BitConverter.ToInt32(slot.Slice(8, 4)), Data4 = BitConverter.ToInt32(slot.Slice(12, 4)),
                };
                if (!guid.IsNull) result.Add(guid);
            }
            return result;
        }

        public const int MinimumIndexEntries = 8;

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

        /// <summary>The System.Guid with the same bytes, for messages ({03de9ea9-9b51-400a-b0cb-8bcc12a12697}).</summary>
        public static Guid ToSystem(GUID g)
        {
            var b = new byte[16];
            BitConverter.TryWriteBytes(b.AsSpan(0, 4), g.Data1);
            BitConverter.TryWriteBytes(b.AsSpan(4, 4), g.Data2);
            BitConverter.TryWriteBytes(b.AsSpan(8, 4), g.Data3);
            BitConverter.TryWriteBytes(b.AsSpan(12, 4), g.Data4);
            return new Guid(b);
        }
    }
}
