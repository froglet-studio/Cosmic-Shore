using System;
using CosmicShore.Engine;
using Xunit;

namespace CosmicShore.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public class RenderVisibilityCollection { public const string Name = "Render visibility (Time.frameCount, render epoch)"; }

/// <summary>
/// <see cref="Renderer.isVisible"/> is Unity's "drawn by some camera in the last rendered frame" (RenderVisibility.cs),
/// not "enabled and active". The crystals' visual-only effects skip their work on it; while it read true for every
/// renderer, a grown Bloomrush arena rebuilt every Charge crystal's arc mesh every frame and ran at 3-5 fps headless.
/// </summary>
[Collection(RenderVisibilityCollection.Name)]
public class RenderVisibilityTests : IDisposable
{
    readonly GameLoop _loop;
    readonly GameObject _go;
    readonly MeshRenderer _renderer;

    public RenderVisibilityTests()
    {
        _loop = new GameLoop();
        Renderer.ResetRenderVisibility();
        _go = new GameObject("visibility probe");
        _go.AddComponent<MeshFilter>();
        _renderer = _go.AddComponent<MeshRenderer>();
    }

    public void Dispose()
    {
        Renderer.ResetRenderVisibility();
        CosmicShore.Engine.Object.DestroyImmediate(_go);
        _loop.Dispose();
    }

    static void NextFrame() => Time.Advance(1f / 60f);

    [Fact]
    public void NothingRendered_NothingVisible()
    {
        // Headless: no camera ever draws, so an enabled, active renderer is still not visible (Unity with no graphics).
        Assert.True(_renderer.enabled && _go.activeInHierarchy);
        Assert.False(_renderer.isVisible);
    }

    [Fact]
    public void SeenByACamera_IsVisibleUntilAFrameRendersWithoutIt()
    {
        NextFrame();
        Renderer.NoteCameraRender();
        _renderer.NoteSeenByCamera();
        Assert.True(_renderer.isVisible);

        NextFrame();
        Renderer.NoteCameraRender();   // the next rendered frame: this renderer was culled
        Assert.False(_renderer.isVisible);
    }

    [Fact]
    public void AnyCameraOfTheFrameCounts()
    {
        NextFrame();
        Renderer.NoteCameraRender();   // the main camera: culled
        Renderer.NoteCameraRender();   // a second camera in the same frame (a render texture): sees it
        _renderer.NoteSeenByCamera();
        Assert.True(_renderer.isVisible);
    }

    [Fact]
    public void AFrameTickedWithoutRendering_KeepsTheLastAnswer()
    {
        NextFrame();
        Renderer.NoteCameraRender();
        _renderer.NoteSeenByCamera();
        NextFrame();
        NextFrame();                   // the game ticked twice and nothing drew
        Assert.True(_renderer.isVisible);
    }

    [Fact]
    public void DisabledOrForcedOff_IsNeverVisible()
    {
        NextFrame();
        Renderer.NoteCameraRender();
        _renderer.NoteSeenByCamera();
        _renderer.forceRenderingOff = true;
        Assert.False(_renderer.isVisible);
        _renderer.forceRenderingOff = false;
        _renderer.enabled = false;
        Assert.False(_renderer.isVisible);
    }
}
