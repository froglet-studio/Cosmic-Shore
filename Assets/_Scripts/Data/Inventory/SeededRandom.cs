using System;

namespace CosmicShore.Data
{
    /// <summary>
    /// A small deterministic random stream (SplitMix64) for procedural pilots and artifacts (Docs/Pilots/INVENTORY.md §2).
    ///
    /// <para>Neither <c>System.Random</c> nor <c>UnityEngine.Random</c> is used for anything that is saved or shared:
    /// <c>System.Random</c>'s sequence is an implementation detail that differs between runtimes (Mono, IL2CPP, .NET),
    /// and <c>UnityEngine.Random</c> is one global stream. A pilot or artifact rolled from the same seed must come out
    /// the same on every platform and every peer, forever, so the generator owns its arithmetic.</para>
    /// </summary>
    public struct SeededRandom
    {
        ulong _state;

        public SeededRandom(ulong seed) { _state = seed; }

        /// <summary>A stream for one named purpose of a seed ("name", "story"...), so adding a roll to one purpose never
        /// shifts what another purpose rolls.</summary>
        public static SeededRandom For(ulong seed, string purpose) => new(seed ^ Hash(purpose));

        /// <summary>The next 64 random bits.</summary>
        public ulong NextULong()
        {
            ulong z = _state += 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        /// <summary>A double in [0, 1).</summary>
        public double NextDouble() => (NextULong() >> 11) * (1.0 / (1UL << 53));

        /// <summary>A float in [min, max).</summary>
        public float Range(float min, float max) => (float)(min + (max - min) * NextDouble());

        /// <summary>An int in [0, count). Throws for count &lt;= 0.</summary>
        public int Next(int count)
        {
            if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count), count, "Pick from at least one.");
            return (int)(NextULong() % (ulong)count);
        }

        /// <summary>One entry of <paramref name="from"/>.</summary>
        public T Pick<T>(T[] from) => from[Next(from.Length)];

        /// <summary>A stable 64-bit FNV-1a hash of a string (the same everywhere, unlike <c>string.GetHashCode</c>).</summary>
        public static ulong Hash(string text)
        {
            ulong h = 0xCBF29CE484222325UL;
            if (text == null) return h;
            foreach (char c in text)
            {
                h ^= c;
                h *= 0x100000001B3UL;
            }
            return h;
        }

        /// <summary>A fresh seed nobody has rolled: 64 bits from a new Guid. Never zero.</summary>
        public static ulong NewSeed()
        {
            var bytes = Guid.NewGuid().ToByteArray();
            ulong seed = BitConverter.ToUInt64(bytes, 0) ^ BitConverter.ToUInt64(bytes, 8);
            return seed == 0 ? 1UL : seed;
        }

        /// <summary>A seed as the 16 lowercase hex characters ids carry.</summary>
        public static string ToHex(ulong seed) => seed.ToString("x16");

        /// <summary>The seed back from <see cref="ToHex"/>. False for anything else.</summary>
        public static bool TryParseHex(string hex, out ulong seed)
        {
            seed = 0;
            if (string.IsNullOrEmpty(hex) || hex.Length != 16) return false;
            return ulong.TryParse(hex, System.Globalization.NumberStyles.AllowHexSpecifier,
                System.Globalization.CultureInfo.InvariantCulture, out seed);
        }
    }
}
