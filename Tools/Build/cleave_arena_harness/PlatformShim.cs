// The first-party surface the arena generators sit on: the two enums, the lay record, and a
// MEASURING CellEnvironmentSpawnableBase.
//
// The real base drags in UniTask, Prism, PrismTrailBuilder and the whole spawn pipeline, none of
// which affects what an arena EMITS. What does affect it is the protected surface below, so every
// member here reproduces the real one exactly - Jit draws from the same shared System.Random in
// the same order, N01 runs the same value noise, and Emit applies the same spawn-clearance test.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Data
{
    public enum Domains { Jade = 1, Ruby = 2, Blue = 3, Gold = 4 }
    public enum PrismKind { Plain = 0, Danger = 1, Shielded = 2, SuperShielded = 3 }
}

namespace CosmicShore.Gameplay
{
    using CosmicShore.Data;

    public struct SpawnPoint
    {
        public Vector3 Position; public Quaternion Rotation; public Vector3 Scale;
        public SpawnPoint(Vector3 p, Quaternion r, Vector3 s) { Position = p; Rotation = r; Scale = s; }

        public static Quaternion LookRotation(Vector3 forward, Vector3 up) =>
            forward.sqrMagnitude < 0.0001f ? Quaternion.identity : Quaternion.LookRotation(forward, up);
        public static Quaternion LookRotation(Vector3 from, Vector3 to, Vector3 up) =>
            LookRotation(to - from, up);
    }

    public readonly struct PrismLay
    {
        public readonly SpawnPoint Point; public readonly Domains Domain; public readonly PrismKind Kind;
        public PrismLay(SpawnPoint p, Domains d, PrismKind k = PrismKind.Plain) { Point = p; Domain = d; Kind = k; }
    }

    public enum FloraSiteKind { Bed, Climb, Basket, Water, Ledge }
    public readonly struct FloraPlantingSite
    {
        public readonly Vector3 Position, Up; public readonly FloraSiteKind Kind;
        public FloraPlantingSite(Vector3 p, Vector3 up, FloraSiteKind k) { Position = p; Up = up; Kind = k; }
    }

    /// <summary>
    /// VERBATIM copy of the three functions <c>CellEnvironmentSpawnableBase.N01</c> reaches in
    /// <c>Assets/_Scripts/Controller/Toys/PaintingStrokeToolkit.cs</c>. Copied rather than compiled
    /// because that file is 1,000 lines and pulls in the ScriptableObjects assembly; the copy is
    /// guarded by <c>cleave_budget.py</c>, which hashes the real file and refuses to answer from a
    /// stale measurement if it moves.
    /// </summary>
    public static class PaintingStrokeToolkit
    {
        static float Hash(int x, int y, int z, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + z * 2147483647 + seed * 971);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFFu) / (float)0x800000 - 1f; // [-1,1)
            }
        }

        static float Fade(float t) => t * t * t * (t * (t * 6f - 15f) + 10f); // smootherstep

        public static float ValueNoise(Vector3 p, int seed)
        {
            int x0 = Mathf.FloorToInt(p.x), y0 = Mathf.FloorToInt(p.y), z0 = Mathf.FloorToInt(p.z);
            float fx = Fade(p.x - x0), fy = Fade(p.y - y0), fz = Fade(p.z - z0);

            float c000 = Hash(x0, y0, z0, seed), c100 = Hash(x0 + 1, y0, z0, seed);
            float c010 = Hash(x0, y0 + 1, z0, seed), c110 = Hash(x0 + 1, y0 + 1, z0, seed);
            float c001 = Hash(x0, y0, z0 + 1, seed), c101 = Hash(x0 + 1, y0, z0 + 1, seed);
            float c011 = Hash(x0, y0 + 1, z0 + 1, seed), c111 = Hash(x0 + 1, y0 + 1, z0 + 1, seed);

            float x00 = Mathf.Lerp(c000, c100, fx), x10 = Mathf.Lerp(c010, c110, fx);
            float x01 = Mathf.Lerp(c001, c101, fx), x11 = Mathf.Lerp(c011, c111, fx);
            float y0v = Mathf.Lerp(x00, x10, fy), y1v = Mathf.Lerp(x01, x11, fy);
            return Mathf.Lerp(y0v, y1v, fz);
        }

        public static Vector3 CurlNoise(Vector3 p, float freq, int seed) => Vector3.zero;
    }

    public class Prism { }

    /// <summary>Measuring stand-in for the real base. Same protected surface, and Emit tallies
    /// instead of laying.</summary>
    public abstract class CellEnvironmentSpawnableBase
    {
        protected Prism prism;
        protected const float LayBudgetMsPerFrame = 8f;
        protected float density = 1f;
        protected float spawnClearRadius = 0f;
        protected Vector3[] spawnClearPoints = Array.Empty<Vector3>();
        protected const float GoldenAngle = 2.39996323f;
        protected List<PrismLay> _cachedLays;
        protected System.Random _r;
        protected int _noiseSeed;
        protected int seed;
        protected Domains domain;
        protected List<FloraPlantingSite> _plantingSites;

        protected abstract int DefaultSeed { get; }
        protected virtual int LayCapacity => 40000;
        protected abstract void BuildEnvironment();
        protected abstract int BuildParameterHash();

        protected float RangeF(float min, float max) => (float)(_r.NextDouble() * (max - min) + min);

        protected Vector3 Jit(Vector3 s, float amt = 0.2f)
        {
            float k = 1f + RangeF(-amt, amt);
            return new Vector3(Mathf.Max(0.5f, s.x * k), Mathf.Max(0.5f, s.y * k), Mathf.Max(0.5f, s.z * k));
        }

        protected static float Hash01(int n)
        {
            unchecked
            {
                uint h = (uint)n;
                h = (h ^ 61u) ^ (h >> 16);
                h *= 9u;
                h ^= h >> 4;
                h *= 0x27d4eb2du;
                h ^= h >> 15;
                return (h & 0xffffffu) / (float)0x1000000;
            }
        }

        protected float N01(float x, float y, float z, int seedOffset) =>
            0.5f * (PaintingStrokeToolkit.ValueNoise(new Vector3(x, y, z), _noiseSeed + seedOffset) + 1f);

        protected Vector3 Curl(Vector3 p, float freq, int seedOffset) =>
            PaintingStrokeToolkit.CurlNoise(p, freq, _noiseSeed + seedOffset);

        protected int Scaled(int n) => Mathf.Max(1, Mathf.RoundToInt(n * density));

        protected void Emit(Vector3 pos, Quaternion rot, Vector3 scale, Domains dom, PrismKind kind = PrismKind.Plain)
        {
            if (spawnClearPoints != null && spawnClearRadius > 0f)
            {
                float rr = spawnClearRadius * spawnClearRadius;
                for (int i = 0; i < spawnClearPoints.Length; i++)
                    if ((pos - spawnClearPoints[i]).sqrMagnitude < rr) return;
            }
            _cachedLays.Add(new PrismLay(new SpawnPoint(pos, rot, scale), dom, kind));
        }

        protected void Sow(Vector3 pos, Vector3 up, FloraSiteKind kind = FloraSiteKind.Bed)
        {
            _plantingSites?.Add(new FloraPlantingSite(pos, up, kind));
        }

        /// <summary>Run the SHIPPED generator exactly as <c>GenerateTrailData</c> would, and hand
        /// back its lay list.</summary>
        public List<PrismLay> Measure(int authoredSeed, float authoredDensity)
        {
            seed = authoredSeed;
            density = authoredDensity;
            _noiseSeed = seed != 0 ? seed : DefaultSeed;
            _r = new System.Random(_noiseSeed);
            _cachedLays = new List<PrismLay>(LayCapacity);
            _plantingSites = new List<FloraPlantingSite>();
            BuildEnvironment();
            return _cachedLays;
        }

        public int DefaultSeedForHarness => DefaultSeed;
        public int LayCapacityForHarness => LayCapacity;
    }
}
