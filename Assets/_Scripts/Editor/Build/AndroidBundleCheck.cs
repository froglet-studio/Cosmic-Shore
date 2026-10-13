using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace CosmicShore.Editor
{
    /// <summary>
    /// Reads a finished Android App Bundle (<c>.aab</c>) and reports what Google Play will think of
    /// it, before anyone uploads it. Run by <see cref="CosmicShoreBuildPipeline"/> after every
    /// Android release build, and on any bundle from <c>FrogletTools/Build/Check Android App
    /// Bundle...</c>.
    ///
    /// <para><b>Errors</b> are what Play rejects outright: a debug or missing signature, a
    /// debuggable manifest, a target SDK below <see cref="RequiredTargetSdk"/>, no 64-bit ARM
    /// libraries, a base module over 200 MB, or the wrong package name. <b>Warnings</b> are what
    /// Play accepts today but the team decided against: native libraries without 16 KB page
    /// alignment (Play blocks UPDATES lacking it from 1 Feb 2027), an app category other than
    /// <c>game</c>, a Play Billing permission (Unity IAP was removed), and a base module over
    /// 150 MB.</para>
    ///
    /// <para>No Unity API here, so the parsing is unit-tested on its own
    /// (<c>AndroidBundleCheckTests</c>), and no bundletool or Java: the manifest inside a bundle is
    /// aapt2's protobuf <c>XmlNode</c> (<c>Resources.proto</c>), read by a small wire-format reader,
    /// and the 16 KB check reads each library's ELF program headers. Both were checked against
    /// manifests linked by aapt2 8.13.2 with the API 36 <c>android.jar</c>.</para>
    /// </summary>
    public static class AndroidBundleCheck
    {
        /// <summary>Play has required API 36 for new apps and updates since 31 Aug 2026.</summary>
        public const int RequiredTargetSdk = 36;

        /// <summary>The LOAD segment alignment Android 15+ 16 KB-page devices need.</summary>
        public const long PageSize16K = 16 * 1024;

        /// <summary>Play's hard limit on the base module's compressed download size.</summary>
        public const long BaseModuleLimitBytes = 200L * 1024 * 1024;

        /// <summary>Above this, Unity's own validation warns; a sign the binary should be split.</summary>
        public const long BaseModuleWarnBytes = 150L * 1024 * 1024;

        /// <summary>The largest version code Play accepts.</summary>
        public const int MaxVersionCode = 2_100_000_000;

        static readonly DateTime VersionCodeEpoch = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        /// <summary>
        /// The version code a build made at <paramref name="utcNow"/> gets: whole minutes since
        /// 2026-01-01 UTC, at least 1. Every build is higher than the one before it without anyone
        /// editing Player Settings, and at about 525,600 a year it stays under
        /// <see cref="MaxVersionCode"/> for millennia.
        /// </summary>
        public static int VersionCodeAt(DateTime utcNow) =>
            (int)Math.Max(1, Math.Floor((utcNow.ToUniversalTime() - VersionCodeEpoch).TotalMinutes));

        const string ManifestEntry = "base/manifest/AndroidManifest.xml";
        const string BillingPermission = "com.android.vending.BILLING";

        // An ELF's program headers sit right after its 64-byte header in every library seen so
        // far. Reading stops here so a library is never inflated whole just to read its header.
        const int MaxElfHeaderBytes = 1 << 20;

        /// <summary>One native library and the smallest alignment of its LOAD segments.</summary>
        public readonly struct LibraryAlignment
        {
            public readonly string Path;
            public readonly long MinLoadAlign;
            public LibraryAlignment(string path, long minLoadAlign) { Path = path; MinLoadAlign = minLoadAlign; }
            public bool Is16KAligned => MinLoadAlign >= PageSize16K;
        }

        /// <summary>What the bundle's manifest says, as far as the checks need it.</summary>
        public sealed class ManifestInfo
        {
            public string Package;
            public long VersionCode;
            public string VersionName;
            public int MinSdk;
            public int TargetSdk;
            public string AppCategory;
            public bool Debuggable;
            public readonly List<string> Permissions = new();
        }

        /// <summary>The result of <see cref="Check"/>.</summary>
        public sealed class Report
        {
            public string BundlePath;
            public ManifestInfo Manifest;
            public readonly List<string> Abis = new();
            public readonly List<LibraryAlignment> Libraries = new();
            public long BaseCompressedBytes;
            public bool Signed;
            public bool DebugSigned;
            public readonly List<string> Errors = new();
            public readonly List<string> Warnings = new();

            public bool Passed => Errors.Count == 0;

            /// <summary>Plain-text report: verdict, errors, warnings, then the facts read.</summary>
            public string Format()
            {
                var sb = new StringBuilder();
                sb.AppendLine($"Android App Bundle check: {(Passed ? "PASS" : "FAIL")}");
                sb.AppendLine($"Bundle: {BundlePath}");
                foreach (string e in Errors) sb.AppendLine($"ERROR   {e}");
                foreach (string w in Warnings) sb.AppendLine($"WARNING {w}");
                if (Manifest != null)
                {
                    sb.AppendLine($"Package: {Manifest.Package}");
                    sb.AppendLine($"Version: {Manifest.VersionName} (code {Manifest.VersionCode})");
                    sb.AppendLine($"SDK: min {Manifest.MinSdk}, target {Manifest.TargetSdk}");
                    sb.AppendLine($"App category: {(string.IsNullOrEmpty(Manifest.AppCategory) ? "(none)" : Manifest.AppCategory)}");
                    sb.AppendLine($"Debuggable: {Manifest.Debuggable}");
                    sb.AppendLine("Permissions: " + (Manifest.Permissions.Count == 0
                        ? "(none)" : string.Join(", ", Manifest.Permissions)));
                }
                sb.AppendLine("ABIs: " + (Abis.Count == 0 ? "(none)" : string.Join(", ", Abis)));
                foreach (var lib in Libraries)
                    sb.AppendLine($"  {lib.Path}: LOAD align {FormatAlign(lib.MinLoadAlign)}{(lib.Is16KAligned ? "" : "  <- not 16 KB")}");
                sb.AppendLine($"Base module, compressed: {BaseCompressedBytes / (1024.0 * 1024.0):F1} MB (Play's limit is 200 MB)");
                sb.AppendLine($"Signed: {(Signed ? (DebugSigned ? "yes, with the Android DEBUG key" : "yes") : "no")}");
                return sb.ToString();
            }
        }

        /// <summary>
        /// Opens <paramref name="aabPath"/> and runs every check. <paramref name="expectedPackage"/>
        /// is the application id Play knows the app by; null skips that check.
        /// </summary>
        public static Report Check(string aabPath, string expectedPackage)
        {
            var report = new Report { BundlePath = aabPath };
            if (!File.Exists(aabPath))
            {
                report.Errors.Add($"No file at {aabPath}.");
                return report;
            }

            using (var zip = ZipFile.OpenRead(aabPath))
            {
                var manifestEntry = zip.GetEntry(ManifestEntry);
                if (manifestEntry == null)
                {
                    report.Errors.Add($"No {ManifestEntry}: this is not an App Bundle (an APK, or a bundle from an export?).");
                }
                else
                {
                    try { report.Manifest = ParseManifest(ReadAll(manifestEntry)); }
                    catch (FormatException ex) { report.Errors.Add($"The manifest could not be read: {ex.Message}"); }
                }

                foreach (var entry in zip.Entries)
                {
                    string name = entry.FullName;
                    if (name.StartsWith("base/", StringComparison.Ordinal))
                        report.BaseCompressedBytes += entry.CompressedLength;

                    if (IsSignatureBlock(name))
                    {
                        report.Signed = true;
                        if (ContainsAscii(ReadAll(entry), "Android Debug")) report.DebugSigned = true;
                    }

                    if (TryGetLibraryAbi(name, out string abi))
                    {
                        if (!report.Abis.Contains(abi)) report.Abis.Add(abi);
                        if (abi == "arm64-v8a" || abi == "x86_64")
                        {
                            using var stream = entry.Open();
                            report.Libraries.Add(new LibraryAlignment(name, MinLoadAlignment(stream)));
                        }
                    }
                }
            }

            Evaluate(report, expectedPackage);
            return report;
        }

        static void Evaluate(Report r, string expectedPackage)
        {
            var m = r.Manifest;
            if (m != null)
            {
                if (!string.IsNullOrEmpty(expectedPackage) && m.Package != expectedPackage)
                    r.Errors.Add($"Package is '{m.Package}', but Play knows the app as '{expectedPackage}'. A package name can never change after the first upload.");
                if (m.VersionCode <= 0)
                    r.Errors.Add("The manifest has no versionCode.");
                else if (m.VersionCode > MaxVersionCode)
                    r.Errors.Add($"Version code {m.VersionCode} is above Play's limit of {MaxVersionCode}.");
                if (m.TargetSdk < RequiredTargetSdk)
                    r.Errors.Add($"Target SDK is {m.TargetSdk}; Play requires {RequiredTargetSdk}. Player Settings > Android > Other Settings > Target API Level, and install SDK Platform {RequiredTargetSdk}.");
                if (m.Debuggable)
                    r.Errors.Add("The manifest is debuggable: Play rejects it. Build with Development Build off.");
                if (m.AppCategory != "game")
                    r.Warnings.Add($"App category is '{m.AppCategory ?? "(none)"}', not 'game'. Android 16 exempts only games from forced large-screen resizing. Player Settings > Android > Other Settings > Application Category.");
                if (m.Permissions.Contains(BillingPermission))
                    r.Warnings.Add("The bundle asks for Play Billing. Unity IAP was removed (Docs/PLATFORM_UNIFICATION.md section 3.9): find what added it back.");
            }

            if (r.Abis.Count > 0 && !r.Abis.Contains("arm64-v8a"))
                r.Errors.Add("No arm64-v8a libraries: Play requires 64-bit. Player Settings > Android > Target Architectures > ARM64.");
            if (r.Abis.Count == 0)
                r.Errors.Add("No native libraries at all. A Unity player always has some; the bundle is incomplete.");

            var unaligned = r.Libraries.Where(l => !l.Is16KAligned).ToList();
            if (unaligned.Count > 0)
                r.Warnings.Add($"{unaligned.Count} native librar{(unaligned.Count == 1 ? "y is" : "ies are")} not 16 KB-aligned ({string.Join(", ", unaligned.Select(l => Path.GetFileName(l.Path)))}). Play blocks updates without 16 KB support from 1 Feb 2027.");

            if (!r.Signed)
                r.Errors.Add("The bundle is not signed. Sign it with the upload key: Player Settings > Android > Publishing Settings > Custom Keystore.");
            else if (r.DebugSigned)
                r.Errors.Add("The bundle is signed with the Android DEBUG key, which Play rejects. Select the upload key in Publishing Settings.");

            if (r.BaseCompressedBytes > BaseModuleLimitBytes)
                r.Errors.Add("The base module is over Play's 200 MB limit. Turn on Player Settings > Android > Publishing Settings > Split Application Binary.");
            else if (r.BaseCompressedBytes > BaseModuleWarnBytes)
                r.Warnings.Add("The base module is over 150 MB, close to Play's 200 MB limit. Consider Split Application Binary.");
        }

        // ──────────────────────────────────────────────
        //  Zip entry helpers
        // ──────────────────────────────────────────────

        static bool IsSignatureBlock(string name) =>
            name.StartsWith("META-INF/", StringComparison.Ordinal) && name.IndexOf('/', 9) < 0
            && (name.EndsWith(".RSA", StringComparison.OrdinalIgnoreCase)
                || name.EndsWith(".EC", StringComparison.OrdinalIgnoreCase)
                || name.EndsWith(".DSA", StringComparison.OrdinalIgnoreCase));

        /// <summary>True for <c>&lt;module&gt;/lib/&lt;abi&gt;/&lt;name&gt;.so</c>.</summary>
        public static bool TryGetLibraryAbi(string entryName, out string abi)
        {
            abi = null;
            if (!entryName.EndsWith(".so", StringComparison.Ordinal)) return false;
            string[] parts = entryName.Split('/');
            if (parts.Length != 4 || parts[1] != "lib") return false;
            abi = parts[2];
            return true;
        }

        static byte[] ReadAll(ZipArchiveEntry entry)
        {
            using var stream = entry.Open();
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            return ms.ToArray();
        }

        static bool ContainsAscii(byte[] data, string text)
        {
            byte[] needle = Encoding.ASCII.GetBytes(text);
            for (int i = 0; i + needle.Length <= data.Length; i++)
            {
                int j = 0;
                while (j < needle.Length && data[i + j] == needle[j]) j++;
                if (j == needle.Length) return true;
            }
            return false;
        }

        // ──────────────────────────────────────────────
        //  ELF: the smallest LOAD alignment
        // ──────────────────────────────────────────────

        /// <summary>
        /// The smallest <c>p_align</c> over a 64-bit little-endian ELF's LOAD segments, read from
        /// the start of <paramref name="elf"/>. Returns 0 for anything that is not such an ELF, or
        /// whose program headers lie beyond the first megabyte; both count as not aligned.
        /// </summary>
        public static long MinLoadAlignment(Stream elf)
        {
            byte[] head = ReadUpTo(elf, 64);
            if (head.Length < 64 || head[0] != 0x7F || head[1] != (byte)'E' || head[2] != (byte)'L' || head[3] != (byte)'F')
                return 0;
            if (head[4] != 2 || head[5] != 1) return 0; // ELFCLASS64, ELFDATA2LSB

            long phoff = (long)BitConverter.ToUInt64(head, 0x20);
            int phentsize = BitConverter.ToUInt16(head, 0x36);
            int phnum = BitConverter.ToUInt16(head, 0x38);
            if (phentsize < 56 || phnum == 0) return 0;

            long end = phoff + (long)phentsize * phnum;
            if (phoff < 64 || end > MaxElfHeaderBytes) return 0;

            byte[] rest = ReadUpTo(elf, (int)end - 64);
            if (rest.Length < end - 64) return 0;
            byte[] all = new byte[end];
            Buffer.BlockCopy(head, 0, all, 0, 64);
            Buffer.BlockCopy(rest, 0, all, 64, rest.Length);

            long min = long.MaxValue;
            for (int i = 0; i < phnum; i++)
            {
                int ph = (int)(phoff + (long)i * phentsize);
                const uint PtLoad = 1;
                if (BitConverter.ToUInt32(all, ph) != PtLoad) continue;
                long align = (long)BitConverter.ToUInt64(all, ph + 48);
                if (align < min) min = align;
            }
            return min == long.MaxValue ? 0 : min;
        }

        static byte[] ReadUpTo(Stream s, int count)
        {
            var buffer = new byte[Math.Max(0, count)];
            int read = 0;
            while (read < count)
            {
                int n = s.Read(buffer, read, count - read);
                if (n <= 0) break;
                read += n;
            }
            if (read == count) return buffer;
            Array.Resize(ref buffer, read);
            return buffer;
        }

        // ──────────────────────────────────────────────
        //  Manifest: aapt2's protobuf XmlNode
        // ──────────────────────────────────────────────

        /// <summary>
        /// Reads a bundle's proto manifest (aapt2 <c>Resources.proto</c>). Only the fields the
        /// checks need are kept: the package and version, the SDK levels, the permissions, and the
        /// application's category and debuggable flag. Throws <see cref="FormatException"/> on bytes
        /// that are not well-formed protobuf.
        /// </summary>
        public static ManifestInfo ParseManifest(byte[] proto)
        {
            var info = new ManifestInfo();
            var root = new ProtoReader(proto, 0, proto.Length);
            while (root.Next(out int field, out int wire))
            {
                if (field == 1 && wire == 2) VisitElement(root.ReadSub(), info);
                else root.Skip(wire);
            }
            return info;
        }

        // XmlElement { namespace_declaration = 1; namespace_uri = 2; name = 3; attribute = 4; child = 5; }
        static void VisitElement(ProtoReader element, ManifestInfo info)
        {
            string name = null;
            var attributes = new List<(string name, string value, long? number)>();
            var children = new List<ProtoReader>();

            while (element.Next(out int field, out int wire))
            {
                if (field == 3 && wire == 2) name = element.ReadString();
                else if (field == 4 && wire == 2) attributes.Add(ReadAttribute(element.ReadSub()));
                else if (field == 5 && wire == 2) children.Add(element.ReadSub());
                else element.Skip(wire);
            }

            foreach (var (attr, value, number) in attributes)
            {
                switch (name)
                {
                    case "manifest":
                        if (attr == "package") info.Package = value;
                        else if (attr == "versionCode") info.VersionCode = AsLong(value, number);
                        else if (attr == "versionName") info.VersionName = value;
                        break;
                    case "uses-sdk":
                        if (attr == "minSdkVersion") info.MinSdk = (int)AsLong(value, number);
                        else if (attr == "targetSdkVersion") info.TargetSdk = (int)AsLong(value, number);
                        break;
                    case "uses-permission":
                    case "uses-permission-sdk-23":
                        if (attr == "name" && !string.IsNullOrEmpty(value)) info.Permissions.Add(value);
                        break;
                    case "application":
                        if (attr == "appCategory") info.AppCategory = AppCategoryName(value, number);
                        else if (attr == "debuggable") info.Debuggable = value == "true" || (value == null && number == 1);
                        break;
                }
            }

            // XmlNode { element = 1; text = 2; }
            foreach (var child in children)
            {
                while (child.Next(out int field, out int wire))
                {
                    if (field == 1 && wire == 2) VisitElement(child.ReadSub(), info);
                    else child.Skip(wire);
                }
            }
        }

        // XmlAttribute { namespace_uri = 1; name = 2; value = 3; resource_id = 5; compiled_item = 6; }
        static (string name, string value, long? number) ReadAttribute(ProtoReader attr)
        {
            string name = null, value = null;
            long? number = null;
            while (attr.Next(out int field, out int wire))
            {
                if (field == 2 && wire == 2) name = attr.ReadString();
                else if (field == 3 && wire == 2) value = attr.ReadString();
                else if (field == 6 && wire == 2) number = ReadPrimitive(attr.ReadSub());
                else attr.Skip(wire);
            }
            return (name, string.IsNullOrEmpty(value) ? null : value, number);
        }

        // Item { prim = 7; }  Primitive { int_decimal = 6; int_hexadecimal = 7; boolean = 8; }
        static long? ReadPrimitive(ProtoReader item)
        {
            long? result = null;
            while (item.Next(out int field, out int wire))
            {
                if (field == 7 && wire == 2)
                {
                    var prim = item.ReadSub();
                    while (prim.Next(out int pf, out int pw))
                    {
                        if ((pf == 6 || pf == 7 || pf == 8) && pw == 0) result = (long)prim.ReadVarint();
                        else prim.Skip(pw);
                    }
                }
                else item.Skip(wire);
            }
            return result;
        }

        static long AsLong(string value, long? number) =>
            long.TryParse(value, out long parsed) ? parsed : number ?? 0;

        // android:appCategory's enum values (R.attr.appCategory), for a manifest that kept only the
        // compiled value.
        static readonly string[] AppCategories =
            { "game", "audio", "video", "image", "social", "news", "maps", "productivity", "accessibility" };

        static string AppCategoryName(string value, long? number)
        {
            if (!string.IsNullOrEmpty(value)) return value;
            if (number is >= 0 and < 9) return AppCategories[number.Value];
            return null;
        }

        /// <summary>A forward-only protobuf wire-format reader over a slice of a byte array.</summary>
        sealed class ProtoReader
        {
            readonly byte[] _data;
            int _pos;
            readonly int _end;

            public ProtoReader(byte[] data, int start, int end) { _data = data; _pos = start; _end = end; }

            public bool Next(out int field, out int wire)
            {
                field = wire = 0;
                if (_pos >= _end) return false;
                ulong key = ReadVarint();
                field = (int)(key >> 3);
                wire = (int)(key & 7);
                if (field == 0) throw new FormatException("field number 0");
                return true;
            }

            public ulong ReadVarint()
            {
                ulong result = 0;
                for (int shift = 0; shift < 64; shift += 7)
                {
                    if (_pos >= _end) throw new FormatException("truncated varint");
                    byte b = _data[_pos++];
                    result |= (ulong)(b & 0x7F) << shift;
                    if ((b & 0x80) == 0) return result;
                }
                throw new FormatException("varint too long");
            }

            int ReadLength()
            {
                ulong len = ReadVarint();
                if (len > (ulong)(_end - _pos)) throw new FormatException("length past the end");
                return (int)len;
            }

            public ProtoReader ReadSub()
            {
                int len = ReadLength();
                var sub = new ProtoReader(_data, _pos, _pos + len);
                _pos += len;
                return sub;
            }

            public string ReadString()
            {
                int len = ReadLength();
                string s = Encoding.UTF8.GetString(_data, _pos, len);
                _pos += len;
                return s;
            }

            public void Skip(int wire)
            {
                switch (wire)
                {
                    case 0: ReadVarint(); break;
                    case 1: Advance(8); break;
                    case 2: Advance(ReadLength()); break;
                    case 5: Advance(4); break;
                    default: throw new FormatException($"wire type {wire}");
                }
            }

            void Advance(int n)
            {
                if (n > _end - _pos) throw new FormatException("field past the end");
                _pos += n;
            }
        }

        static string FormatAlign(long align) =>
            align <= 0 ? "unreadable" : align >= 1024 ? $"{align / 1024} KB" : $"{align} B";
    }
}
