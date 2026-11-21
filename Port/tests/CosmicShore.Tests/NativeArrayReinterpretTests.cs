using System;
using CosmicShore.Engine.Collections;
using Unity.Mathematics;

namespace CosmicShore.Tests;

/// <summary>
/// NativeArray.Reinterpret is a view of the same memory, as in Unity: the swarm, substrate and
/// builder fauna pose their prisms by writing floats through <c>matrices.Reinterpret&lt;float&gt;(64)</c>.
/// The port used to return a copy, so those writes vanished without an error.
/// </summary>
public class NativeArrayReinterpretTests
{
    [Fact]
    public void WritesThroughAReinterpretedView_LandInTheOriginalArray()
    {
        var matrices = new NativeArray<float4x4>(3, Allocator.Persistent);
        var floats = matrices.Reinterpret<float>(64);
        Assert.Equal(48, floats.Length);

        var scratch = new float[32];
        for (int i = 0; i < scratch.Length; i++) scratch[i] = i + 1;
        NativeArray<float>.Copy(scratch, 0, floats, 0, 32);   // the fauna's PoseBodies, verbatim

        Assert.Equal(1f, matrices[0].c0.x);
        Assert.Equal(16f, matrices[0].c3.w);
        Assert.Equal(17f, matrices[1].c0.x);
        Assert.Equal(float4x4.zero, matrices[2]);             // untouched beyond the copy
        floats[47] = 99f;
        Assert.Equal(99f, matrices[2].c3.w);
    }

    [Fact]
    public void SameSizeReinterpret_IsAViewToo_AndMismatchesThrow()
    {
        var a = new NativeArray<int>(4, Allocator.Persistent);
        var u = a.Reinterpret<uint>();
        u[2] = 0xFFFFFFFF;
        Assert.Equal(-1, a[2]);
        Assert.Throws<InvalidOperationException>(() => a.Reinterpret<long>());         // different size, no expected size
        Assert.Throws<InvalidOperationException>(() => a.Reinterpret<float>(8));       // wrong expected size
        var odd = new NativeArray<byte>(3, Allocator.Persistent);
        Assert.Throws<InvalidOperationException>(() => odd.Reinterpret<int>(1));       // 3 bytes do not make whole ints
    }

    [Fact]
    public void ViewsOfViews_SubArrays_AndSpansStayOnTheSameMemory()
    {
        var a = new NativeArray<float4>(4, Allocator.Persistent);
        var sub = a.GetSubArray(1, 2).Reinterpret<float>(16);   // floats of elements 1 and 2
        sub.GetSubArray(4, 4).AsSpan().Fill(7f);                 // element 2
        Assert.Equal(new float4(7f), a[2]);
        Assert.Equal(float4.zero, a[1]);
        Assert.Equal(8, sub.AsReadOnlySpan().Length);
        Assert.Equal(new[] { 7f, 7f, 7f, 7f }, sub.GetSubArray(4, 4).ToArray());
    }

    [Fact]
    public void ArrayCopiesWithALength_CopyThatMany()
    {
        var dst = new NativeArray<int>(5, Allocator.Persistent);
        NativeArray<int>.Copy(new[] { 1, 2, 3, 4, 5 }, dst, 3);
        Assert.Equal(new[] { 1, 2, 3, 0, 0 }, dst.ToArray());
        var back = new int[5];
        NativeArray<int>.Copy(dst, back, 2);
        Assert.Equal(new[] { 1, 2, 0, 0, 0 }, back);
    }
}
