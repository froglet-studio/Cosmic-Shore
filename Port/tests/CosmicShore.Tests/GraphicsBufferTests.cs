using System.Runtime.InteropServices;
using CosmicShore.Engine;

namespace CosmicShore.Tests;

/// <summary>
/// ComputeBuffer and GraphicsBuffer are CPU-backed in Prisma; SetData/GetData must round-trip
/// arrays of structs (the swarm renderers upload SwarmInstance[]), which Buffer.BlockCopy refused.
/// Prisma reports no vertex-stage storage buffers, so code that would draw from them falls back.
/// </summary>
public class GraphicsBufferTests
{
    [StructLayout(LayoutKind.Sequential)]
    struct Instance { public Vector3 Position; public float Scale; public uint Flags; }

    [Fact]
    public void StructArrays_RoundTrip_WithRanges()
    {
        var src = new Instance[4];
        for (int i = 0; i < 4; i++) src[i] = new Instance { Position = new Vector3(i, i * 2, i * 3), Scale = i + 0.5f, Flags = (uint)(1 << i) };
        using var buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 4, Marshal.SizeOf<Instance>());
        buffer.SetData(src, 0, 0, 4);
        src[2].Flags = 0;
        buffer.SetData(src, 2, 2, 1);                       // the renderers' HideSlot: one element

        var back = new Instance[4];
        buffer.GetData(back);
        Assert.Equal(new Vector3(3, 6, 9), back[3].Position);
        Assert.Equal(0u, back[2].Flags);
        Assert.Equal(2u, back[1].Flags);

        using var compute = new ComputeBuffer(4, Marshal.SizeOf<Instance>());
        compute.SetData(src);
        var cback = new Instance[4];
        compute.GetData(cback);
        Assert.Equal(src, cback);
    }

    [Fact]
    public void NoVertexStageStorageBuffers_SoBufferDrawingCodeFallsBack()
    {
        Assert.Equal(0, SystemInfo.maxComputeBufferInputsVertex);
        var block = new MaterialPropertyBlock();
        using var buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 1, 4);
        block.SetBuffer("_Instances", buffer);               // accepted and kept
        Graphics.RenderMeshPrimitives(new RenderParams(null) { matProps = block }, new Mesh(), 0, 1);  // not drawn; warns once
    }
}
