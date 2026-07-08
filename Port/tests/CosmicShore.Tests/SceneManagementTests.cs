using System;
using System.Threading.Tasks;
using CosmicShore.Engine;
using CosmicShore.Engine.SceneManagement;

namespace CosmicShore.Tests;

// ─────────────────────────────────────────────────────────────────────────────
// Engine scene-management surface (scene phase): GetActiveScene resolving the
// loop-owned scene, and the minimal LoadSceneAsync — next-tick completion,
// active-scene re-designation, and the single sceneLoaded announce carrying
// the ACTIVE scene instance (what HostConnectionService.OnSceneLoaded keys on).
// ─────────────────────────────────────────────────────────────────────────────

public class SceneManagementTests : IDisposable
{
    readonly GameLoop loop = new(nameof(SceneManagementTests));

    public void Dispose() => loop.Dispose();

    sealed class TickDriver : MonoBehaviour
    {
        public Action Action;
        void Update() { var a = Action; Action = null; a?.Invoke(); }
    }

    [Fact]
    public void GetActiveScene_IsTheLoopOwnedScene()
    {
        Assert.Same(loop.Scene, SceneManager.GetActiveScene());
        Assert.Equal(nameof(SceneManagementTests), SceneManager.GetActiveScene().name);
    }

    [Fact]
    public void LoadSceneAsync_CompletesNextTick_RedesignatesAndAnnounces()
    {
        var announced = new System.Collections.Generic.List<(Scene scene, LoadSceneMode mode)>();
        void Record(Scene s, LoadSceneMode m) => announced.Add((s, m));
        SceneManager.sceneLoaded += Record;
        try
        {
            // Before any load, the loop's own scene is active and nothing has fired.
            Assert.Equal(nameof(SceneManagementTests), SceneManager.GetActiveScene().name);
            Assert.Empty(announced);

            // Start inside a tick (C4/C6 discipline); the apply completes through the
            // loop's continuation pump (GameTask.Yield), i.e. asynchronously but still
            // on the PlayerLoop — pump until it settles.
            Task load = null;
            var driver = new GameObject("driver").AddComponent<TickDriver>();
            driver.Action = () => load = SceneManager.LoadSceneAsync("Menu_Main", LoadSceneMode.Single);
            loop.Tick(1f / 60f);
            for (int i = 0; i < 10 && !load.IsCompleted; i++) loop.Tick(1f / 60f);

            Assert.True(load.IsCompleted);
            Assert.Equal("Menu_Main", SceneManager.GetActiveScene().name);
            var (scene, mode) = Assert.Single(announced);
            Assert.Same(loop.Scene, scene); // the ACTIVE scene instance, not a detached handle
            Assert.Equal(LoadSceneMode.Single, mode);
        }
        finally
        {
            SceneManager.sceneLoaded -= Record;
        }
    }
}
