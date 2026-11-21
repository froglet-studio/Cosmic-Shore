// The scalar float maths of the code that runs BOTH in a Burst job and in a pure-.NET harness: the substrate's
// SubstrateKernel.StepAgent (Burst-compiled by SubstrateAgentJob) and the swarm's SwarmBodyPose.PoseMatrix (by
// SwarmPoseJob).
//
// Why it exists: those kernels were written against System.MathF, and Burst cannot compile MathF's transcendental
// functions. In Unity's Mono corlib MathF.Sqrt/Pow/Exp/Sin/Cos/Acos (and the rest of its transcendentals) are
// [MethodImpl(InternalCall)] externs with no IL body, so Burst reports "Unable to find internal function
// `System.MathF::Sqrt`", refuses the job, and Unity runs it managed every frame. Unity.Mathematics is the library
// Burst maps onto its own float intrinsics, so in Unity every method here forwards to math.*.
//
// UNITY_5_3_OR_NEWER is defined in every Unity compile (Editor, development player AND release player), so the
// Unity branch is never editor-only (Docs/CONDITIONAL_COMPILATION.md). The harnesses under Tools/Build compile these
// files with no Unity reference and no Unity define, and there the methods forward to MathF, bit for bit what the
// harnesses asserted before this shim existed (group K's bit-match against the pre-11c managed step included).
// Gates: Tools/Build/swarm_core_harness/check_kernel_math.py (this file's two branches agree, and the Unity branch
// never touches MathF), check_burst_substrate.py and check_burst_pose.py (no MathF in a Burst-compiled kernel).
#if UNITY_5_3_OR_NEWER
using Unity.Mathematics;
#else
using System;
#endif
using System.Runtime.CompilerServices;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Burst-safe scalar float maths for kernels that also run in a pure-.NET harness: Unity.Mathematics in Unity,
    /// <c>System.MathF</c> in the harness. Use it, never <c>MathF</c>, in any function a Burst job calls.
    /// </summary>
    public static class KernelMath
    {
        /// <summary>The float pi, the same constant as <c>MathF.PI</c>.</summary>
        public const float PI = 3.14159265f;

#if UNITY_5_3_OR_NEWER
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static float Sqrt(float x) => math.sqrt(x);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static float Pow(float x, float y) => math.pow(x, y);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static float Exp(float x) => math.exp(x);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static float Sin(float x) => math.sin(x);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static float Cos(float x) => math.cos(x);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static float Acos(float x) => math.acos(x);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static float Abs(float x) => math.abs(x);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static float Min(float a, float b) => math.min(a, b);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static float Max(float a, float b) => math.max(a, b);
#else
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static float Sqrt(float x) => MathF.Sqrt(x);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static float Pow(float x, float y) => MathF.Pow(x, y);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static float Exp(float x) => MathF.Exp(x);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static float Sin(float x) => MathF.Sin(x);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static float Cos(float x) => MathF.Cos(x);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static float Acos(float x) => MathF.Acos(x);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static float Abs(float x) => MathF.Abs(x);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static float Min(float a, float b) => MathF.Min(a, b);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static float Max(float a, float b) => MathF.Max(a, b);
#endif
    }
}
