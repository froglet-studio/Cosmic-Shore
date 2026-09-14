namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The character generator's random stream: a specified xorshift32 with a splitmix seed
    /// scramble, so a genome rolled from a seed is the SAME genome on every machine and every
    /// runtime — never <c>System.Random</c> (implementation-defined across runtimes when
    /// seeded) and never <c>UnityEngine.Random</c> (a global stream anything can spend from).
    /// Same discipline as the Switchback course walk.
    /// </summary>
    public struct CharacterRandom
    {
        uint _state;

        public CharacterRandom(int seed)
        {
            // splitmix32 scramble so consecutive seeds do not produce correlated first draws.
            uint z = unchecked((uint)seed + 0x9E3779B9u);
            z = (z ^ (z >> 16)) * 0x85EBCA6Bu;
            z = (z ^ (z >> 13)) * 0xC2B2AE35u;
            z ^= z >> 16;
            _state = z == 0u ? 0x1234567u : z;
        }

        public uint NextUInt()
        {
            uint x = _state;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            _state = x;
            return x;
        }

        /// <summary>Uniform in [0, 1).</summary>
        public float NextFloat() => (NextUInt() >> 8) * (1f / 16777216f);

        public float Range(float min, float max) => min + (max - min) * NextFloat();

        public int Range(int minInclusive, int maxExclusive)
        {
            int span = maxExclusive - minInclusive;
            return span <= 0 ? minInclusive : minInclusive + (int)(NextUInt() % (uint)span);
        }

        /// <summary>Approximately normal (sum of three uniforms, re-centred), clamped to ±clamp.</summary>
        public float Gaussian(float sigma, float clamp)
        {
            float g = (NextFloat() + NextFloat() + NextFloat() - 1.5f) * 2f * sigma;
            return g < -clamp ? -clamp : (g > clamp ? clamp : g);
        }
    }
}
