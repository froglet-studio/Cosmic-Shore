using System.Linq;
using CosmicShore.Engine;
using CosmicShore.Engine.InputSystem;
using CosmicShore.Engine.Jobs;
using CosmicShore.Engine.Rendering;
using CosmicShore.Engine.Rendering.RenderGraphModule;
using Unity.Jobs;
using Object = CosmicShore.Engine.Object;

namespace CosmicShore.Tests;

// The engine API the game reached for on bleeding-edge 2026-10-13 (the black hole, the mouse orbit
// camera, the crystal flip wave, the camera aspect fit): each as Unity defines it, so the game's own
// files compile unchanged and behave as they do in Unity.
public class EngineApiDriftTests : System.IDisposable
{
    readonly GameLoop _loop = new();

    public void Dispose() => _loop.Dispose();

    struct MoveUp : IJobParallelForTransform
    {
        public float By;
        public void Execute(int i, TransformAccess t) => t.position += new Vector3(0f, By * (i + 1), 0f);
    }

    struct ReadValid : IJobParallelForTransform
    {
        public bool[] Valid;
        public Vector3[] Positions;
        public void Execute(int i, TransformAccess t)
        {
            Valid[i] = t.isValid;
            Positions[i] = t.position;
        }
    }

    [Fact]
    public void ATransformJob_RunsOverEveryTransform_InOrder_AndSkipsADestroyedOne()
    {
        var a = new GameObject("a").transform;
        var b = new GameObject("b").transform;
        var c = new GameObject("c").transform;
        var array = new TransformAccessArray(4);
        array.Add(a); array.Add(b); array.Add(c);
        Assert.True(array.isCreated);
        Assert.Equal(3, array.length);

        var handle = new MoveUp { By = 2f }.Schedule(array);
        handle.Complete();
        Assert.Equal(2f, a.position.y, 4);
        Assert.Equal(4f, b.position.y, 4);
        Assert.Equal(6f, c.position.y, 4);

        // Swap-back removal: the last transform takes the removed slot.
        array.RemoveAtSwapBack(0);
        Assert.Same(c, array[0]);
        Assert.Same(b, array[1]);

        Object.DestroyImmediate(b.gameObject);
        var read = new ReadValid { Valid = new bool[2], Positions = new Vector3[2] };
        read.ScheduleReadOnly(array, 64).Complete();
        Assert.True(read.Valid[0]);
        Assert.Equal(6f, read.Positions[0].y, 4);
        Assert.False(read.Valid[1]);                 // destroyed: not valid, reads as default
        Assert.Equal(Vector3.zero, read.Positions[1]);

        array.Dispose();
        Assert.False(array.isCreated);
    }

    [Fact]
    public void InstantiatingAMesh_DeepCopiesIt_IncludingTheWideUvChannels()
    {
        var source = new Mesh { name = "Plates" };
        source.vertices = new[] { Vector3.zero, Vector3.right, Vector3.up };
        source.triangles = new[] { 0, 1, 2 };
        source.SetUVs(2, new[] { new Vector4(1, 2, 3, 4), new Vector4(5, 6, 7, 8), new Vector4(9, 10, 11, 12) });

        var clone = Object.Instantiate(source);
        Assert.NotSame(source, clone);
        Assert.Equal("Plates(Clone)", clone.name);
        Assert.Equal(source.vertices, clone.vertices);
        Assert.Equal(4, clone.GetUVDimension(2));
        var uvs = new System.Collections.Generic.List<Vector4>();
        clone.GetUVs(2, uvs);
        Assert.Equal(new Vector4(9, 10, 11, 12), uvs[2]);

        clone.vertices = new[] { Vector3.one, Vector3.one, Vector3.one };
        Assert.Equal(Vector3.right, source.vertices[1]);   // the source is untouched
    }

    [Fact]
    public void InstantiatingAMaterial_CopiesIt()
    {
        var source = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "Glow" };
        source.SetFloat("_Cutoff", 0.25f);
        var clone = Object.Instantiate(source);
        Assert.NotSame(source, clone);
        Assert.Equal("Glow(Clone)", clone.name);
        Assert.Equal(0.25f, clone.GetFloat("_Cutoff"), 4);
    }

    [Fact]
    public void APhysicalCamera_FitsItsSensorGate_TheWayUnityDoes()
    {
        var cam = new GameObject("cam").AddComponent<Camera>();
        cam.fieldOfView = 60f;
        cam.aspect = 4f / 3f;                    // a screen narrower than the 16:9 sensor
        float Vertical() => 2f * Mathf.Atan(1f / cam.projectionMatrix.m11) * Mathf.Rad2Deg;

        Assert.Equal(60f, Vertical(), 3);         // not physical: the gate is ignored

        cam.usePhysicalProperties = true;
        cam.sensorSize = new Vector2(16f, 9f);
        cam.gateFit = Camera.GateFitMode.Vertical;
        Assert.Equal(60f, Vertical(), 3);

        // Overscan shows the whole sensor: on a narrower screen the width fits, so more is seen vertically.
        cam.gateFit = Camera.GateFitMode.Overscan;
        float expected = 2f * Mathf.Atan(Mathf.Tan(30f * Mathf.Deg2Rad) * (16f / 9f) / (4f / 3f)) * Mathf.Rad2Deg;
        Assert.Equal(expected, Vertical(), 3);

        // Fill covers the screen with the sensor: on a narrower screen the height fits.
        cam.gateFit = Camera.GateFitMode.Fill;
        Assert.Equal(60f, Vertical(), 3);

        // On a wider screen it is the other way round.
        cam.aspect = 21f / 9f;
        cam.gateFit = Camera.GateFitMode.Overscan;
        Assert.Equal(60f, Vertical(), 3);
        cam.gateFit = Camera.GateFitMode.Fill;
        Assert.True(Vertical() < 60f);
    }

    [Fact]
    public void TheControls_LiveWhereUnitysDo()
    {
        Assert.Equal("CosmicShore.Engine.InputSystem.Controls", typeof(CosmicShore.Engine.InputSystem.Controls.ButtonControl).Namespace);
        Assert.Equal("CosmicShore.Engine.InputSystem.Controls", typeof(CosmicShore.Engine.InputSystem.Controls.KeyControl).Namespace);
        Assert.Equal("CosmicShore.Engine.InputSystem", typeof(Mouse).Namespace);
        Assert.Equal("SerializeField", typeof(SerializeField).Name);   // typeof(SerializeField), as the game writes it
        Assert.Equal(new Vector2Int(1, 0), Vector2Int.right);
        Assert.Equal(new Vector2Int(0, -1), Vector2Int.down);
    }

    sealed class RecordingPass : ScriptableRenderPass
    {
        class Data { public TextureHandle Source; }
        public bool Recorded;

        public RecordingPass() => renderPassEvent = RenderPassEvent.AfterRenderingTransparents;

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var resources = frameData.Get<UniversalResourceData>();
            var desc = renderGraph.GetTextureDesc(resources.activeColorTexture);
            desc.name = "_Copy";
            var copy = renderGraph.CreateTexture(desc);
            using var builder = renderGraph.AddRasterRenderPass<Data>("copy", out var data, profilingSampler);
            data.Source = resources.activeColorTexture;
            builder.SetRenderAttachment(copy, 0, AccessFlags.WriteAll);
            builder.SetRenderFunc((Data d, RasterGraphContext ctx) => Blitter.BlitTexture(ctx.cmd, d.Source, Vector4.one, 0f, false));
            Recorded = copy.IsValid();
        }
    }

    [Fact]
    public void AScriptInjectedPass_IsQueuedOnTheCamerasRenderer_AndItsRecordRuns()
    {
        var cam = new GameObject("cam").AddComponent<Camera>();
        var renderer = cam.GetUniversalAdditionalCameraData().scriptableRenderer;
        var pass = new RecordingPass();
        renderer.EnqueuePass(pass);
        Assert.Same(pass, renderer.QueuedPasses.Single());
        renderer.ClearQueuedPasses();
        Assert.Empty(renderer.QueuedPasses);

        var frame = new ContextContainer();
        frame.Create<UniversalResourceData>();
        pass.RecordRenderGraph(new RenderGraph(), frame);
        Assert.True(pass.Recorded);
    }

    [Fact]
    public void ACommandBuffer_RecordsInOrder_AndExecuteHandsItToTheBackend()
    {
        var cmd = new CommandBuffer { name = "sky" };
        var rt = new RenderTexture(8, 8, 0);
        cmd.SetRenderTarget(rt, 0, CubemapFace.Unknown, 3);
        cmd.ClearRenderTarget(false, true, Color.black);
        cmd.SetRenderTarget(BuiltinRenderTextureType.CameraTarget);
        Assert.Equal(3, cmd.Commands.Count);
        var first = Assert.IsType<RenderCommand.SetRenderTarget>(cmd.Commands[0]);
        Assert.Same(rt, first.Target.texture);
        Assert.Equal(3, first.DepthSlice);

        CommandBuffer seen = null;
        var previous = Graphics.ExecuteCommandBufferHook;
        Graphics.ExecuteCommandBufferHook = b => seen = b;
        try { Graphics.ExecuteCommandBuffer(cmd); }
        finally { Graphics.ExecuteCommandBufferHook = previous; }
        Assert.Same(cmd, seen);

        cmd.Clear();
        Assert.Empty(cmd.Commands);
    }
}
