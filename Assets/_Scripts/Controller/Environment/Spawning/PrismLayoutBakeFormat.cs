using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The on-disk form of a BAKED cell-environment layout: every prism a
    /// <see cref="CellEnvironmentSpawnableBase"/> world lays (pose, scale, domain, kind) plus the
    /// planting sites it sows, written once at authoring time so the game reads the layout instead
    /// of running the generator while it loads (Docs/ECOSYSTEM.md §62.3).
    ///
    /// <para><b>Why a bake.</b> Every world is fitted so no prism interpenetrates another
    /// (§62.2), and the fit is part of the generator: up to ~0.45 s of generation on the heavy
    /// worlds before the first prism is laid. The output depends only on the generator's code and
    /// its serialized fields, so it is computed once by <c>Tools/Build/bake_prism_layouts.py</c>
    /// (the shipped generator run through the prism clip audit's harness, the same run the audit
    /// proves flush) and read back here.</para>
    ///
    /// <para><b>The key is a fingerprint of the generator's parameters</b>, not a name:
    /// <see cref="Fingerprint"/> hashes every serialized field the generator declares plus the
    /// base fields that feed generation (seed, density, spawn clearance). A bake is stored as
    /// <c>Resources/PrismLayouts/&lt;Type&gt;_&lt;fingerprint&gt;.bytes</c>, so an edited field asks
    /// for a file that does not exist and the world is generated live. A stale bake cannot be
    /// served for changed parameters. A change to the generator's CODE is caught by
    /// <c>bake_prism_layouts.py --check</c>, which re-runs every bake and compares bytes.</para>
    ///
    /// <para>Pure C# with no engine calls, so the offline harness compiles this exact file: the
    /// fingerprint it writes is the fingerprint the game computes.</para>
    ///
    /// <para>The bake also records <see cref="SourceHash"/> of the generator's own source file.
    /// The player ignores it; the Editor compares it with the script it is running, so a generator
    /// whose code was edited is generated live in Play mode instead of showing its old bake.</para>
    ///
    /// <para>Layout (little-endian): a 32-byte header (magic "CSPL", version, reserved,
    /// fingerprint, source hash, lay count, site count), then the lays as ten float planes (position xyz,
    /// rotation xyzw, scale xyz), a domain byte plane and a kind byte plane, then the sites as six
    /// float planes (position xyz, up xyz) and a kind byte plane. Planes rather than records
    /// because the player build's compression packs a plane of similar floats far better.</para>
    /// </summary>
    public static class PrismLayoutBakeFormat
    {
        /// <summary>The Resources folder every bake lives in.</summary>
        public const string ResourcesFolder = "PrismLayouts";

        const uint Magic = 0x4C505343u;   // "CSPL"
        const ushort Version = 1;
        const int HeaderBytes = 32;
        const int LayFloats = 10;
        const int SiteFloats = 6;

        /// <summary>The base-class fields that feed generation (the same set
        /// <c>CellEnvironmentSpawnableBase.GetParameterHash</c> hashes), named by the base itself with
        /// <c>nameof</c>. Everything else on the bases is laying or runtime state (intensityLevel,
        /// layAcrossFrames, the prism prefab).</summary>
        static string[] BaseFields => CellEnvironmentSpawnableBase.FingerprintBaseFields;

        const string FamilyBase = "CellEnvironmentSpawnableBase";

        public static string FileStem(Type generator, ulong fingerprint) => $"{generator.Name}_{fingerprint:x16}";

        public static string ResourcePath(Type generator, ulong fingerprint) =>
            $"{ResourcesFolder}/{FileStem(generator, fingerprint)}";

        // ── fingerprint ─────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// A stable 64-bit hash (FNV-1a over the raw bits) of every parameter that shapes this
        /// generator's output: the format version, the concrete type's name, the base fields in
        /// <see cref="BaseFields"/>, and every field Unity serializes on the types between the
        /// concrete class and <c>CellEnvironmentSpawnableBase</c>, in name order. Object
        /// references are skipped (the prism prefab shapes the lay's clamp, not the layout).
        /// Not <c>System.HashCode</c>: that one is seeded per process.
        /// </summary>
        public static ulong Fingerprint(object generator)
        {
            var type = generator.GetType();
            ulong h = 14695981039346656037UL;
            Mix(ref h, Version);
            MixString(ref h, type.Name);
            foreach (var name in BaseFields)
            {
                // A base field this cannot find would drop out of the key and let a stale bake be
                // served for any value of it, so a miss is an error, never a skip.
                var f = FindField(type, name) ?? throw new InvalidOperationException(
                    $"{type.Name} has no base field '{name}' for the layout-bake fingerprint");
                MixString(ref h, name);
                MixValue(ref h, f.GetValue(generator), f.FieldType, 0);
            }
            for (var t = type; t != null && t.Name != FamilyBase; t = t.BaseType)
            {
                var declared = t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic |
                                           BindingFlags.DeclaredOnly);
                Array.Sort(declared, (a, b) => string.CompareOrdinal(a.Name, b.Name));
                foreach (var f in declared)
                {
                    if (!IsSerialized(f)) continue;
                    MixString(ref h, f.Name);
                    MixValue(ref h, f.GetValue(generator), f.FieldType, 0);
                }
            }
            return h;
        }

        static FieldInfo FindField(Type t, string name)
        {
            for (var c = t; c != null; c = c.BaseType)
            {
                var f = c.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic |
                                         BindingFlags.DeclaredOnly);
                if (f != null) return f;
            }
            return null;
        }

        /// <summary>Unity's rule: an instance field that is public or [SerializeField], not
        /// readonly, not const, not [NonSerialized].</summary>
        static bool IsSerialized(FieldInfo f) =>
            !f.IsStatic && !f.IsLiteral && !f.IsInitOnly &&
            !f.IsDefined(typeof(NonSerializedAttribute), false) &&
            (f.IsPublic || f.IsDefined(typeof(SerializeField), false));

        static void MixValue(ref ulong h, object v, Type t, int depth)
        {
            // An object reference is not layout: skipped whether or not it is set, so a prefab
            // read with its references unresolved fingerprints the same as the loaded one.
            if (typeof(UnityEngine.Object).IsAssignableFrom(t) || depth > 8) return;
            if (v == null) { MixByte(ref h, 0); return; }
            MixByte(ref h, 1);
            if (t == typeof(float)) { Mix(ref h, (uint)BitConverter.SingleToInt32Bits((float)v)); return; }
            if (t == typeof(double)) { Mix(ref h, (ulong)BitConverter.DoubleToInt64Bits((double)v)); return; }
            if (t == typeof(bool)) { MixByte(ref h, (bool)v ? (byte)1 : (byte)0); return; }
            if (t == typeof(string)) { MixString(ref h, (string)v); return; }
            if (t.IsEnum) { Mix(ref h, unchecked((ulong)Convert.ToInt64(v))); return; }
            if (t == typeof(ulong)) { Mix(ref h, (ulong)v); return; }
            if (t.IsPrimitive) { Mix(ref h, unchecked((ulong)Convert.ToInt64(v))); return; }
            if (t.IsArray)
            {
                var a = (Array)v;
                var et = t.GetElementType();
                Mix(ref h, (uint)a.Length);
                foreach (var e in a) MixValue(ref h, e, et, depth + 1);
                return;
            }
            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>))
            {
                var list = (IList)v;
                var et = t.GetGenericArguments()[0];
                Mix(ref h, (uint)list.Count);
                foreach (var e in list) MixValue(ref h, e, et, depth + 1);
                return;
            }
            // A serializable struct or class (Vector3, Quaternion, a spec record): its own fields.
            var fields = t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Array.Sort(fields, (a, b) => string.CompareOrdinal(a.Name, b.Name));
            foreach (var f in fields)
            {
                if (!IsSerialized(f)) continue;
                MixString(ref h, f.Name);
                MixValue(ref h, f.GetValue(v), f.FieldType, depth + 1);
            }
        }

        static void MixByte(ref ulong h, byte b)
        {
            h ^= b;
            h *= 1099511628211UL;
        }

        static void Mix(ref ulong h, ulong v)
        {
            for (int i = 0; i < 8; i++) MixByte(ref h, (byte)(v >> (8 * i)));
        }

        static void MixString(ref ulong h, string s)
        {
            var bytes = Encoding.UTF8.GetBytes(s);
            Mix(ref h, (uint)bytes.Length);
            foreach (var b in bytes) MixByte(ref h, b);
        }

        /// <summary>FNV-1a over a source file's text, with a byte-order mark and every carriage
        /// return dropped, so a Windows checkout and a Linux one hash the same file alike.</summary>
        public static ulong SourceHash(string text)
        {
            ulong h = 14695981039346656037UL;
            if (string.IsNullOrEmpty(text)) return h;
            int start = text[0] == '\uFEFF' ? 1 : 0;
            var bytes = Encoding.UTF8.GetBytes(text.Substring(start).Replace("\r", ""));
            foreach (var b in bytes) MixByte(ref h, b);
            return h;
        }

        /// <summary>The generator source hash a bake was made from (see <see cref="SourceHash"/>);
        /// false for bytes that are not a bake of this version.</summary>
        public static bool TryReadSourceHash(byte[] data, out ulong sourceHash)
        {
            sourceHash = 0;
            if (!BitConverter.IsLittleEndian || data == null || data.Length < HeaderBytes) return false;
            if (BitConverter.ToUInt32(data, 0) != Magic || BitConverter.ToUInt16(data, 4) != Version) return false;
            sourceHash = BitConverter.ToUInt64(data, 16);
            return true;
        }

        // ── encode / decode ─────────────────────────────────────────────────────────────────────

        /// <summary>The bake for one generation. Used by the offline baker (and any editor tool
        /// that wants to write one); the game only decodes.</summary>
        public static byte[] Encode(ulong fingerprint, ulong sourceHash, IReadOnlyList<PrismLay> lays,
            IReadOnlyList<FloraPlantingSite> sites)
        {
            int n = lays.Count, m = sites?.Count ?? 0;
            var data = new byte[HeaderBytes + n * (LayFloats * 4 + 2) + m * (SiteFloats * 4 + 1)];
            BitConverter.GetBytes(Magic).CopyTo(data, 0);
            BitConverter.GetBytes(Version).CopyTo(data, 4);
            BitConverter.GetBytes(fingerprint).CopyTo(data, 8);
            BitConverter.GetBytes(sourceHash).CopyTo(data, 16);
            BitConverter.GetBytes(n).CopyTo(data, 24);
            BitConverter.GetBytes(m).CopyTo(data, 28);

            var f = new float[n * LayFloats];
            for (int i = 0; i < n; i++)
            {
                var p = lays[i].Point;
                f[i] = p.Position.x; f[n + i] = p.Position.y; f[2 * n + i] = p.Position.z;
                f[3 * n + i] = p.Rotation.x; f[4 * n + i] = p.Rotation.y; f[5 * n + i] = p.Rotation.z; f[6 * n + i] = p.Rotation.w;
                f[7 * n + i] = p.Scale.x; f[8 * n + i] = p.Scale.y; f[9 * n + i] = p.Scale.z;
            }
            Buffer.BlockCopy(f, 0, data, HeaderBytes, f.Length * 4);
            int at = HeaderBytes + f.Length * 4;
            for (int i = 0; i < n; i++) data[at + i] = (byte)lays[i].Domain;
            at += n;
            for (int i = 0; i < n; i++) data[at + i] = (byte)lays[i].Kind;
            at += n;

            var g = new float[m * SiteFloats];
            for (int i = 0; i < m; i++)
            {
                var s = sites[i];
                g[i] = s.Position.x; g[m + i] = s.Position.y; g[2 * m + i] = s.Position.z;
                g[3 * m + i] = s.Up.x; g[4 * m + i] = s.Up.y; g[5 * m + i] = s.Up.z;
            }
            Buffer.BlockCopy(g, 0, data, at, g.Length * 4);
            at += g.Length * 4;
            for (int i = 0; i < m; i++) data[at + i] = (byte)sites[i].Kind;
            return data;
        }

        /// <summary>
        /// Read a bake back. False (and nothing out) when the bytes are not a bake of this
        /// version, were baked for a different <paramref name="fingerprint"/>, or are truncated:
        /// the caller then generates live, which is always correct.
        /// </summary>
        public static bool TryDecode(byte[] data, ulong fingerprint, out List<PrismLay> lays, out List<FloraPlantingSite> sites)
        {
            lays = null;
            sites = null;
            if (!BitConverter.IsLittleEndian || data == null || data.Length < HeaderBytes) return false;
            if (BitConverter.ToUInt32(data, 0) != Magic || BitConverter.ToUInt16(data, 4) != Version) return false;
            if (BitConverter.ToUInt64(data, 8) != fingerprint) return false;
            int n = BitConverter.ToInt32(data, 24), m = BitConverter.ToInt32(data, 28);
            if (n < 0 || m < 0 ||
                data.Length != HeaderBytes + (long)n * (LayFloats * 4 + 2) + (long)m * (SiteFloats * 4 + 1)) return false;

            var f = new float[n * LayFloats];
            Buffer.BlockCopy(data, HeaderBytes, f, 0, f.Length * 4);
            int dom = HeaderBytes + f.Length * 4, kind = dom + n;
            lays = new List<PrismLay>(n);
            for (int i = 0; i < n; i++)
                lays.Add(new PrismLay(new SpawnPoint(
                        new Vector3(f[i], f[n + i], f[2 * n + i]),
                        new Quaternion(f[3 * n + i], f[4 * n + i], f[5 * n + i], f[6 * n + i]),
                        new Vector3(f[7 * n + i], f[8 * n + i], f[9 * n + i])),
                    (Domains)data[dom + i], (PrismKind)data[kind + i]));

            int at = kind + n;
            var g = new float[m * SiteFloats];
            Buffer.BlockCopy(data, at, g, 0, g.Length * 4);
            at += g.Length * 4;
            sites = new List<FloraPlantingSite>(m);
            for (int i = 0; i < m; i++)
                sites.Add(new FloraPlantingSite(new Vector3(g[i], g[m + i], g[2 * m + i]),
                    new Vector3(g[3 * m + i], g[4 * m + i], g[5 * m + i]), (FloraSiteKind)data[at + i]));
            return true;
        }
    }
}
