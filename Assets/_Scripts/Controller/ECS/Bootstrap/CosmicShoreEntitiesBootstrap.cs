using Unity.Entities;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The project's DOTS world bootstrap. Its ONE job is to keep Entities Graphics from being
    /// created on a graphics device that cannot run it.
    ///
    /// On a healthy device it returns false and Unity performs its ordinary default-world
    /// initialization, byte for byte — every system, including <c>EntitiesGraphicsSystem</c>, on
    /// the player loop exactly as before this class existed.
    ///
    /// When <see cref="EntitiesGraphicsSupportProbe"/> says the compute kernels will not load, it
    /// creates the default world EMPTY — no systems at all — and returns true. Entities requires
    /// <see cref="World.DefaultGameObjectInjectionWorld"/> to be set by a bootstrap that claims to
    /// have initialized, so an empty world (rather than none) is what "stand down" has to look like.
    /// <see cref="PrismRenderService"/> then finds no <c>EntitiesGraphicsSystem</c> in that world and
    /// keeps every prism on its legacy MeshRenderer path, which it already supports: the game runs
    /// with instanced prism rendering off instead of dying before its first frame.
    ///
    /// Instanced prism rendering is currently the project's only runtime Entities usage
    /// (<see cref="PrismRenderService"/> documents this), which is why an empty world is safe.
    /// The day a second Entities consumer lands, this bootstrap should build the world with every
    /// system EXCEPT the Entities Graphics assembly's rather than with none.
    /// </summary>
    public sealed class CosmicShoreEntitiesBootstrap : ICustomBootstrap
    {
        public bool Initialize(string defaultWorldName)
        {
            if (EntitiesGraphicsSupportProbe.IsSupported)
                return false;

            Debug.LogWarning(
                $"[EntitiesBootstrap] Entities Graphics cannot run on this device ({EntitiesGraphicsSupportProbe.Reason}); " +
                $"graphics API {SystemInfo.graphicsDeviceType}, {SystemInfo.graphicsDeviceName}, driver {SystemInfo.graphicsDeviceVersion}. " +
                "Creating an EMPTY default ECS world so the half-built renderer cannot crash the process. " +
                "Prisms will render on the legacy MeshRenderer path (PrismRenderService reports OFF).");

            var world = new World(defaultWorldName, WorldFlags.Game);
            World.DefaultGameObjectInjectionWorld = world;
            return true;
        }
    }
}
