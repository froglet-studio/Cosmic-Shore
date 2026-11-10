using System;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A cell's VISUAL genome: a few bytes that make one cell look unlike another - its membrane's wobble, the
    /// membrane's bright/dull tint, the cytoplasm's flow and tint, and a reserved block of ambient-sound numbers.
    /// Visual only: nothing here reaches a cell's boundary, volume, spawns or phase (Docs/GENOME_CELL_GENOME.md).
    /// <para>The packed form is <see cref="Size"/> bytes with an explicit little-endian layout (the doc's table),
    /// so a genome can be stored as a short base64 string, mutated byte by byte and searched. Every field is
    /// quantised by <see cref="Pack"/>; <see cref="Unpack"/> of packed bytes packs back to the same bytes.</para>
    /// </summary>
    [Serializable]
    public struct CellGenome : IEquatable<CellGenome>
    {
        /// <summary>Packed size in bytes.</summary>
        public const int Size = 56;
        /// <summary>The layout version written at byte 0.</summary>
        public const byte Version = 1;
        /// <summary>Flow terms carried (bytes 22-39).</summary>
        public const int FlowTermCount = 3;

        // quantisation ranges (the packed byte/ushort spans exactly these)
        public const float MaxFrequency = 2f;
        public const float MaxAmplitude = 40f;
        public const float MaxPulseSpeed = 2f;
        public const float MaxHsvOffset = 0.5f;
        public const float MaxFlowFrequency = 4f;

        /// <summary>One term of the cytoplasm's analytic flow.</summary>
        public enum FlowKind : byte
        {
            /// <summary>Unused term.</summary>
            Off = 0,
            /// <summary>Rotation round the term's axis, banded along it.</summary>
            Swirl = 1,
            /// <summary>An Arnold-Beltrami-Childress cell (divergence-free) in the term's frame.</summary>
            Abc = 2,
            /// <summary>Radial breathing rings out from the centre.</summary>
            Rings = 3,
        }

        [Serializable]
        public struct FlowTerm : IEquatable<FlowTerm>
        {
            [Tooltip("What shape this term adds to the flow. Off ignores the term.")]
            public FlowKind Kind;
            [Tooltip("The term's axis: yaw in degrees (0-360).")]
            [Range(0f, 360f)] public float AxisYaw;
            [Tooltip("The term's axis: pitch in degrees (-90 to 90).")]
            [Range(-90f, 90f)] public float AxisPitch;
            [Tooltip("Spatial frequency across the cell (cycles per membrane radius).")]
            [Range(0f, MaxFlowFrequency)] public float Frequency;
            [Tooltip("Signed weight of this term in the sum.")]
            [Range(-1f, 1f)] public float Strength;
            [Tooltip("Phase of the term's pattern (0-1 of a cycle).")]
            [Range(0f, 1f)] public float Phase;

            public Vector3 Axis => Quaternion.Euler(AxisPitch, AxisYaw, 0f) * Vector3.forward;

            public bool Equals(FlowTerm o) => Kind == o.Kind && AxisYaw == o.AxisYaw && AxisPitch == o.AxisPitch &&
                                               Frequency == o.Frequency && Strength == o.Strength && Phase == o.Phase;
            public override bool Equals(object obj) => obj is FlowTerm o && Equals(o);
            public override int GetHashCode() => HashCode.Combine(Kind, AxisYaw, AxisPitch, Frequency, Strength, Phase);
        }

        [Header("Membrane wobble (CapsuleMembrane.ApplyWobble)")]
        [Tooltip("Seed of the membrane's placement and pulse noise.")]
        public int WobbleSeed;
        [Tooltip("Spatial frequency of the radial pulse (shipped 0.6).")]
        [Range(0f, MaxFrequency)] public float WobbleFrequency;
        [Tooltip("Radial pulse amplitude in membrane units (shipped 10).")]
        [Range(0f, MaxAmplitude)] public float WobbleAmplitude;
        [Tooltip("How fast the pulse pattern drifts (shipped 0.4).")]
        [Range(0f, MaxPulseSpeed)] public float WobblePulseSpeed;

        [Header("Tints (HSV offsets from the material's own colours)")]
        [Tooltip("Hue, saturation, value added to the membrane's bright colour (each -0.5 to 0.5).")]
        public Vector3 MembraneBrightHsv;
        [Tooltip("Hue, saturation, value added to the membrane's dull colour (each -0.5 to 0.5).")]
        public Vector3 MembraneDullHsv;
        [Tooltip("Hue, saturation, value added to the cytoplasm shards' colour (each -0.5 to 0.5).")]
        public Vector3 CytoplasmHsv;

        [Header("Cytoplasm flow (SnowChanger.IFlow)")]
        [Tooltip("Off leaves the cytoplasm on the shipped law (every shard pointing at the crystal).")]
        public bool FlowEnabled;
        [Tooltip("Shortest streak the flow draws (0 a mote, 1 the longest streak); the rest scales with flow speed.")]
        [Range(0f, 1f)] public float FlowBaseStretch;
        public FlowTerm Flow0, Flow1, Flow2;

        [Header("Ambient sound (reserved numbers; no event is wired)")]
        [Tooltip("Pitch offset in semitones (-12 to 12). Reserved: nothing reads it yet.")]
        [Range(-12f, 12f)] public float AmbientPitch;
        [Tooltip("Volume (0-1). Reserved.")]
        [Range(0f, 1f)] public float AmbientVolume;
        [Tooltip("Low-pass amount (0-1). Reserved.")]
        [Range(0f, 1f)] public float AmbientLowPass;
        [Tooltip("Pulse rate (0-1 of the sound's range). Reserved.")]
        [Range(0f, 1f)] public float AmbientRate;

        public FlowTerm GetFlowTerm(int i) => i switch { 0 => Flow0, 1 => Flow1, _ => Flow2 };

        void SetFlowTerm(int i, FlowTerm t)
        {
            if (i == 0) Flow0 = t; else if (i == 1) Flow1 = t; else Flow2 = t;
        }

        // ---------------------------------------------------------------- packing

        /// <summary>The genome as <see cref="Size"/> bytes (layout in Docs/GENOME_CELL_GENOME.md).</summary>
        public byte[] Pack()
        {
            var b = new byte[Size];
            b[0] = Version;
            WriteInt(b, 1, WobbleSeed);
            WriteU16(b, 5, QU16(WobbleFrequency, MaxFrequency));
            WriteU16(b, 7, QU16(WobbleAmplitude, MaxAmplitude));
            WriteU16(b, 9, QU16(WobblePulseSpeed, MaxPulseSpeed));
            WriteHsv(b, 11, MembraneBrightHsv);
            WriteHsv(b, 14, MembraneDullHsv);
            WriteHsv(b, 17, CytoplasmHsv);
            b[20] = (byte)(FlowEnabled ? 1 : 0);
            b[21] = QU8(FlowBaseStretch, 1f);
            for (int i = 0; i < FlowTermCount; i++)
            {
                var t = GetFlowTerm(i);
                int o = 22 + i * 6;
                b[o] = (byte)((int)t.Kind & 0x03);
                b[o + 1] = QU8(Mathf.Repeat(t.AxisYaw, 360f), 360f);
                b[o + 2] = QU8(Mathf.Clamp(t.AxisPitch, -90f, 90f) + 90f, 180f);
                b[o + 3] = QU8(t.Frequency, MaxFlowFrequency);
                b[o + 4] = (byte)QS8(t.Strength, 1f);
                b[o + 5] = QU8(Mathf.Repeat(t.Phase, 1f), 1f);
            }
            b[40] = (byte)QS8(AmbientPitch, 12f);
            b[41] = QU8(AmbientVolume, 1f);
            b[42] = QU8(AmbientLowPass, 1f);
            b[43] = QU8(AmbientRate, 1f);
            // 44-54 reserved (zero); 55 checksum
            b[Size - 1] = Checksum(b);
            return b;
        }

        /// <summary>Read a genome from <see cref="Pack"/>'s bytes. False on a wrong length, version or checksum.</summary>
        public static bool TryUnpack(byte[] b, out CellGenome g)
        {
            g = default;
            if (b == null || b.Length != Size || b[0] != Version || b[Size - 1] != Checksum(b)) return false;
            g.WobbleSeed = ReadInt(b, 1);
            g.WobbleFrequency = DU16(ReadU16(b, 5), MaxFrequency);
            g.WobbleAmplitude = DU16(ReadU16(b, 7), MaxAmplitude);
            g.WobblePulseSpeed = DU16(ReadU16(b, 9), MaxPulseSpeed);
            g.MembraneBrightHsv = ReadHsv(b, 11);
            g.MembraneDullHsv = ReadHsv(b, 14);
            g.CytoplasmHsv = ReadHsv(b, 17);
            g.FlowEnabled = b[20] != 0;
            g.FlowBaseStretch = DU8(b[21], 1f);
            for (int i = 0; i < FlowTermCount; i++)
            {
                int o = 22 + i * 6;
                g.SetFlowTerm(i, new FlowTerm
                {
                    Kind = (FlowKind)(b[o] & 0x03),
                    AxisYaw = DU8(b[o + 1], 360f),
                    AxisPitch = DU8(b[o + 2], 180f) - 90f,
                    Frequency = DU8(b[o + 3], MaxFlowFrequency),
                    Strength = DS8((sbyte)b[o + 4], 1f),
                    Phase = DU8(b[o + 5], 1f),
                });
            }
            g.AmbientPitch = DS8((sbyte)b[40], 12f);
            g.AmbientVolume = DU8(b[41], 1f);
            g.AmbientLowPass = DU8(b[42], 1f);
            g.AmbientRate = DU8(b[43], 1f);
            return true;
        }

        public string ToBase64() => Convert.ToBase64String(Pack());

        public static bool TryFromBase64(string s, out CellGenome g)
        {
            g = default;
            if (string.IsNullOrEmpty(s)) return false;
            byte[] b;
            try { b = Convert.FromBase64String(s); }
            catch (FormatException) { return false; }
            return TryUnpack(b, out g);
        }

        /// <summary>The genome as its packed bytes would read back (every field quantised).</summary>
        public CellGenome Quantised() => TryUnpack(Pack(), out var q) ? q : this;

        // ---------------------------------------------------------------- seeding

        /// <summary>
        /// A tasteful genome derived from <paramref name="seed"/> (a cell name's hash): wobble near the shipped values
        /// (frequency 0.6, amplitude 10, pulse 0.4), small tints, a gentle flow of two or three terms. Deterministic on
        /// every platform (its own integer hash, not <c>UnityEngine.Random</c>) and already quantised.
        /// </summary>
        public static CellGenome FromSeed(int seed)
        {
            uint s = (uint)seed * 0x9E3779B9u ^ 0x85EBCA6Bu;
            var g = new CellGenome
            {
                WobbleSeed = (int)(Next(ref s) & 0x7FFFFFFF),
                WobbleFrequency = Range(ref s, 0.45f, 0.8f),
                WobbleAmplitude = Range(ref s, 7f, 14f),
                WobblePulseSpeed = Range(ref s, 0.28f, 0.55f),
                MembraneBrightHsv = SmallHsv(ref s),
                MembraneDullHsv = SmallHsv(ref s),
                CytoplasmHsv = SmallHsv(ref s),
                FlowEnabled = true,
                FlowBaseStretch = Range(ref s, 0.15f, 0.4f),
                AmbientPitch = Range(ref s, -3f, 3f),
                AmbientVolume = Range(ref s, 0.4f, 0.8f),
                AmbientLowPass = Range(ref s, 0.2f, 0.6f),
                AmbientRate = Range(ref s, 0.2f, 0.6f),
            };
            int terms = 2 + (int)(Next(ref s) % 2u);
            for (int i = 0; i < FlowTermCount; i++)
            {
                var t = new FlowTerm
                {
                    Kind = i < terms ? (FlowKind)(1 + Next(ref s) % 3u) : FlowKind.Off,
                    AxisYaw = Range(ref s, 0f, 360f),
                    AxisPitch = Range(ref s, -80f, 80f),
                    Frequency = Range(ref s, 0.5f, 2f),
                    Strength = (Next(ref s) % 2u == 0 ? 1f : -1f) * Range(ref s, 0.3f, 0.9f),
                    Phase = Range(ref s, 0f, 1f),
                };
                if (t.Kind == FlowKind.Off) t = default;
                g.SetFlowTerm(i, t);
            }
            return g.Quantised();
        }

        /// <summary>A stable hash of a cell's name (FNV-1a), for <see cref="FromSeed"/>. Not string.GetHashCode,
        /// which is randomised per process on some runtimes.</summary>
        public static int StableHash(string name)
        {
            unchecked
            {
                uint h = 2166136261u;
                if (name != null) for (int i = 0; i < name.Length; i++) h = (h ^ name[i]) * 16777619u;
                return (int)h;
            }
        }

        static Vector3 SmallHsv(ref uint s) =>
            new(Range(ref s, -0.05f, 0.05f), Range(ref s, -0.08f, 0.08f), Range(ref s, -0.08f, 0.08f));

        static uint Next(ref uint s)
        {
            // xorshift32
            s ^= s << 13; s ^= s >> 17; s ^= s << 5;
            return s;
        }

        static float Range(ref uint s, float lo, float hi) => lo + (hi - lo) * ((Next(ref s) >> 8) / 16777216f);

        // ---------------------------------------------------------------- colour

        /// <summary><paramref name="baseColour"/> shifted by an HSV offset; HDR intensity (a component over 1) and alpha
        /// are kept.</summary>
        public static Color ShiftHsv(Color baseColour, Vector3 hsv)
        {
            float peak = Mathf.Max(1f, Mathf.Max(baseColour.r, Mathf.Max(baseColour.g, baseColour.b)));
            var ldr = new Color(baseColour.r / peak, baseColour.g / peak, baseColour.b / peak);
            Color.RGBToHSV(ldr, out float h, out float sat, out float v);
            h = Mathf.Repeat(h + hsv.x, 1f);
            sat = Mathf.Clamp01(sat + hsv.y);
            v = Mathf.Clamp01(v + hsv.z);
            var c = Color.HSVToRGB(h, sat, v) * peak;
            c.a = baseColour.a;
            return c;
        }

        // ---------------------------------------------------------------- bytes

        static byte Checksum(byte[] b)
        {
            unchecked
            {
                byte c = 0x5A;
                for (int i = 0; i < Size - 1; i++) c = (byte)((c << 1 | c >> 7) ^ b[i]);
                return c;
            }
        }

        static ushort QU16(float v, float max) => (ushort)Mathf.RoundToInt(Mathf.Clamp01(v / max) * 65535f);
        static float DU16(ushort q, float max) => q / 65535f * max;
        static byte QU8(float v, float max) => (byte)Mathf.RoundToInt(Mathf.Clamp01(v / max) * 255f);
        static float DU8(byte q, float max) => q / 255f * max;
        static sbyte QS8(float v, float max) => (sbyte)Mathf.RoundToInt(Mathf.Clamp(v / max, -1f, 1f) * 127f);
        static float DS8(sbyte q, float max) => q / 127f * max;

        static void WriteHsv(byte[] b, int o, Vector3 v)
        {
            b[o] = (byte)QS8(v.x, MaxHsvOffset);
            b[o + 1] = (byte)QS8(v.y, MaxHsvOffset);
            b[o + 2] = (byte)QS8(v.z, MaxHsvOffset);
        }

        static Vector3 ReadHsv(byte[] b, int o) =>
            new(DS8((sbyte)b[o], MaxHsvOffset), DS8((sbyte)b[o + 1], MaxHsvOffset), DS8((sbyte)b[o + 2], MaxHsvOffset));

        static void WriteInt(byte[] b, int o, int v)
        {
            b[o] = (byte)v; b[o + 1] = (byte)(v >> 8); b[o + 2] = (byte)(v >> 16); b[o + 3] = (byte)(v >> 24);
        }

        static int ReadInt(byte[] b, int o) => b[o] | b[o + 1] << 8 | b[o + 2] << 16 | b[o + 3] << 24;
        static void WriteU16(byte[] b, int o, ushort v) { b[o] = (byte)v; b[o + 1] = (byte)(v >> 8); }
        static ushort ReadU16(byte[] b, int o) => (ushort)(b[o] | b[o + 1] << 8);

        // ---------------------------------------------------------------- equality

        public bool Equals(CellGenome o) =>
            WobbleSeed == o.WobbleSeed && WobbleFrequency == o.WobbleFrequency && WobbleAmplitude == o.WobbleAmplitude &&
            WobblePulseSpeed == o.WobblePulseSpeed && MembraneBrightHsv == o.MembraneBrightHsv &&
            MembraneDullHsv == o.MembraneDullHsv && CytoplasmHsv == o.CytoplasmHsv && FlowEnabled == o.FlowEnabled &&
            FlowBaseStretch == o.FlowBaseStretch && Flow0.Equals(o.Flow0) && Flow1.Equals(o.Flow1) &&
            Flow2.Equals(o.Flow2) && AmbientPitch == o.AmbientPitch && AmbientVolume == o.AmbientVolume &&
            AmbientLowPass == o.AmbientLowPass && AmbientRate == o.AmbientRate;

        public override bool Equals(object obj) => obj is CellGenome o && Equals(o);
        public override int GetHashCode() => HashCode.Combine(WobbleSeed, WobbleFrequency, WobbleAmplitude, WobblePulseSpeed, Flow0, Flow1, Flow2);
    }
}
