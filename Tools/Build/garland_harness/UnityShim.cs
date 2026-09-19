// UnityEngine + project surface for the Garland harness.
//
// Every PROJECT signature here is a TRANSCRIPTION, and a transcription is only evidence about
// the shipped code if something pins it to its source - so author_garland_cell.py --check
// asserts each one appears VERBATIM in the file named beside it. (The Scarab cavitation
// verifier's rule: comparing copies against each other says nothing about the original.)
//
// Hash01's BODY is copied whole rather than stubbed, because the harness RUNS: every wobble in
// SpawnableGarland is a hash of the emitting index, so a stubbed hash would measure a different
// world and report it as a match.
using System;
using System.Collections.Generic;

namespace UnityEngine
{
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 zero => new Vector3(0, 0, 0);
        public static Vector3 forward => new Vector3(0, 0, 1);
        public static Vector3 up => new Vector3(0, 1, 0);
        public static Vector3 right => new Vector3(1, 0, 0);
        public float magnitude => Mathf.Sqrt(x * x + y * y + z * z);
        public float sqrMagnitude => x * x + y * y + z * z;
        public Vector3 normalized { get { float m = magnitude; return m < 1e-5f ? zero : this / m; } }
        public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3(a.x - b.x, a.y - b.y, a.z - b.z);
        public static Vector3 operator -(Vector3 a) => new Vector3(-a.x, -a.y, -a.z);
        public static Vector3 operator *(Vector3 a, float k) => new Vector3(a.x * k, a.y * k, a.z * k);
        public static Vector3 operator *(float k, Vector3 a) => a * k;
        public static Vector3 operator /(Vector3 a, float k) => new Vector3(a.x / k, a.y / k, a.z / k);
        public static Vector3 Cross(Vector3 a, Vector3 b) =>
            new Vector3(a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x);
        public static float Dot(Vector3 a, Vector3 b) => a.x * b.x + a.y * b.y + a.z * b.z;
        public static Vector3 Lerp(Vector3 a, Vector3 b, float t) => a + (b - a) * t;
    }

    // Inert on purpose: the harness measures POSITION, SCALE, DOMAIN and KIND. A rotation moves
    // neither a prism's centre nor its volume, so stubbing it cannot flatter the measurement.
    public struct Quaternion { public static Quaternion identity => default; }

    public static class Mathf
    {
        public const float PI = 3.14159265f;
        public static float Sqrt(float v) => (float)Math.Sqrt(v);
        public static float Cos(float v) => (float)Math.Cos(v);
        public static float Sin(float v) => (float)Math.Sin(v);
        public static float Pow(float a, float b) => (float)Math.Pow(a, b);
        public static float Max(float a, float b) => a > b ? a : b;
        public static int Max(int a, int b) => a > b ? a : b;
        public static float Lerp(float a, float b, float t) => a + (b - a) * t;
        public static int FloorToInt(float v) => (int)Math.Floor(v);
    }
}

namespace CosmicShore.Data
{
    public enum Domains { Jade = 1, Ruby = 2, Blue = 3, Gold = 4 }
    public enum PrismKind { Plain, Danger, Shielded, SuperShielded }
}

namespace CosmicShore.Gameplay
{
    using UnityEngine;
    using CosmicShore.Data;

    public static class SpawnPoint
    {
        public static Quaternion LookRotation(Vector3 forward, Vector3 up) => Quaternion.identity;
    }

    public abstract class SpawnableBase { }

    public abstract class CellEnvironmentSpawnableBase : SpawnableBase
    {
        public static readonly List<(Vector3 p, Vector3 s, Domains d, PrismKind k)> Recorded = new();

        protected const float GoldenAngle = 2.39996323f;
        protected abstract int DefaultSeed { get; }
        protected virtual int LayCapacity => 40000;
        protected abstract void BuildEnvironment();
        protected virtual bool AdmitsAuthoredPrismScale => false;
        protected abstract int BuildParameterHash();

        protected void Emit(Vector3 pos, Quaternion rot, Vector3 scale, Domains dom, PrismKind kind = PrismKind.Plain)
            => Recorded.Add((pos, scale, dom, kind));

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

        public void RunBuild() => BuildEnvironment();
    }
}
