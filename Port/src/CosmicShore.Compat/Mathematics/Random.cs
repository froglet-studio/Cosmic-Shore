using System;

namespace Unity.Mathematics
{
    /// <summary>
    /// Small, fast, deterministic PRNG over a 32-bit xorshift (shifts 13 / 17 / 5), matching the
    /// documented behaviour of Unity.Mathematics.Random: a value type whose whole state is one
    /// non-zero <see cref="state"/> word, seeded by a NON-ZERO seed (zero is rejected — xorshift
    /// is stuck at zero forever), float results in [min, max), int results in [min, max).
    /// Copying the struct copies the stream; pass it by <c>ref</c> to share one.
    /// </summary>
    [Serializable]
    public partial struct Random
    {
        public uint state;

        public Random(uint seed = 0x6E624EB7u)
        {
            state = seed;
            CheckInitState();
            NextState();
        }

        public static Random CreateFromIndex(uint index)
        {
            // Hash the index so consecutive indices give well-separated, non-zero seeds.
            uint h = math.hash(index);
            return new Random(h == 0 ? 0x6E624EB7u : h);
        }

        public void InitState(uint seed = 0x6E624EB7u)
        {
            state = seed;
            NextState();
        }

        void CheckInitState()
        {
            if (state == 0)
                throw new ArgumentException("Seed must be non-zero");
        }

        /// <summary>Advances the xorshift32 state and returns the PREVIOUS state.</summary>
        uint NextState()
        {
            CheckInitState();
            uint t = state;
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return t;
        }

        public bool NextBool() => (NextState() & 1u) == 1u;
        public bool2 NextBool2() { uint v = NextState(); return new bool2((v & 1u) == 1u, (v & 2u) == 2u); }
        public bool3 NextBool3() { uint v = NextState(); return new bool3((v & 1u) == 1u, (v & 2u) == 2u, (v & 4u) == 4u); }
        public bool4 NextBool4() { uint v = NextState(); return new bool4((v & 1u) == 1u, (v & 2u) == 2u, (v & 4u) == 4u, (v & 8u) == 8u); }

        /// <summary>Uniform over the full int range.</summary>
        public int NextInt() => (int)NextState() ^ int.MinValue;
        /// <summary>Uniform in [0, max). <paramref name="max"/> must be non-negative.</summary>
        public int NextInt(int max) => (int)((NextState() * (ulong)max) >> 32);
        /// <summary>Uniform in [min, max).</summary>
        public int NextInt(int min, int max)
        {
            uint range = (uint)(max - min);
            return (int)((NextState() * (ulong)range) >> 32) + min;
        }
        public int2 NextInt2() => new int2(NextInt(), NextInt());
        public int2 NextInt2(int2 max) => new int2(NextInt(max.x), NextInt(max.y));
        public int2 NextInt2(int2 min, int2 max) => new int2(NextInt(min.x, max.x), NextInt(min.y, max.y));
        public int3 NextInt3() => new int3(NextInt(), NextInt(), NextInt());
        public int3 NextInt3(int3 max) => new int3(NextInt(max.x), NextInt(max.y), NextInt(max.z));
        public int3 NextInt3(int3 min, int3 max) => new int3(NextInt(min.x, max.x), NextInt(min.y, max.y), NextInt(min.z, max.z));
        public int4 NextInt4() => new int4(NextInt(), NextInt(), NextInt(), NextInt());
        public int4 NextInt4(int4 max) => new int4(NextInt(max.x), NextInt(max.y), NextInt(max.z), NextInt(max.w));
        public int4 NextInt4(int4 min, int4 max) => new int4(NextInt(min.x, max.x), NextInt(min.y, max.y), NextInt(min.z, max.z), NextInt(min.w, max.w));

        /// <summary>Uniform in [0, 2^32 - 2] (the xorshift state is never 0).</summary>
        public uint NextUInt() => NextState() - 1u;
        public uint NextUInt(uint max) => (uint)((NextState() * (ulong)max) >> 32);
        public uint NextUInt(uint min, uint max) => (uint)((NextState() * (ulong)(max - min)) >> 32) + min;
        public uint2 NextUInt2() => new uint2(NextUInt(), NextUInt());
        public uint3 NextUInt3() => new uint3(NextUInt(), NextUInt(), NextUInt());
        public uint4 NextUInt4() => new uint4(NextUInt(), NextUInt(), NextUInt(), NextUInt());

        /// <summary>Uniform in [0, 1): 23 random mantissa bits under exponent 0, minus one.</summary>
        public float NextFloat() => math.asfloat(0x3f800000u | (NextState() >> 9)) - 1f;
        public float NextFloat(float max) => NextFloat() * max;
        public float NextFloat(float min, float max) => NextFloat() * (max - min) + min;
        public float2 NextFloat2() => new float2(NextFloat(), NextFloat());
        public float2 NextFloat2(float2 max) => NextFloat2() * max;
        public float2 NextFloat2(float2 min, float2 max) => NextFloat2() * (max - min) + min;
        public float3 NextFloat3() => new float3(NextFloat(), NextFloat(), NextFloat());
        public float3 NextFloat3(float3 max) => NextFloat3() * max;
        public float3 NextFloat3(float3 min, float3 max) => NextFloat3() * (max - min) + min;
        public float4 NextFloat4() => new float4(NextFloat(), NextFloat(), NextFloat(), NextFloat());
        public float4 NextFloat4(float4 max) => NextFloat4() * max;
        public float4 NextFloat4(float4 min, float4 max) => NextFloat4() * (max - min) + min;

        /// <summary>Uniform in [0, 1) from 52 random mantissa bits (two states).</summary>
        public double NextDouble()
        {
            ulong bits = ((ulong)NextState() << 20) ^ NextState();
            return math.asdouble(0x3ff0000000000000UL | (bits & 0x000FFFFFFFFFFFFFUL)) - 1.0;
        }
        public double NextDouble(double max) => NextDouble() * max;
        public double NextDouble(double min, double max) => NextDouble() * (max - min) + min;
        public double3 NextDouble3() => new double3(NextDouble(), NextDouble(), NextDouble());

        /// <summary>Uniformly distributed unit vector on the circle.</summary>
        public float2 NextFloat2Direction()
        {
            float angle = NextFloat() * math.PI * 2f;
            math.sincos(angle, out float s, out float c);
            return new float2(c, s);
        }

        /// <summary>Uniformly distributed unit vector on the sphere (Archimedes' z-slicing).</summary>
        public float3 NextFloat3Direction()
        {
            float2 rnd = NextFloat2();
            float z = rnd.x * 2f - 1f;
            float r = MathF.Sqrt(MathF.Max(1f - z * z, 0f));
            math.sincos(rnd.y * math.PI * 2f, out float s, out float c);
            return new float3(c * r, s * r, z);
        }

        /// <summary>Uniformly distributed rotation (Shoemake's subgroup method), w kept non-negative.</summary>
        public quaternion NextQuaternionRotation()
        {
            float3 rnd = NextFloat3(new float3(2f * math.PI, 2f * math.PI, 1f));
            float u1 = rnd.z;
            float r1 = MathF.Sqrt(1f - u1), r2 = MathF.Sqrt(u1);
            math.sincos(rnd.x, out float s1, out float c1);
            math.sincos(rnd.y, out float s2, out float c2);
            var q = new float4(r1 * s1, r1 * c1, r2 * s2, r2 * c2);
            return new quaternion(q.w < 0f ? -q : q);
        }
    }
}
