using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A switch ring drawn as the SYNTHETIC WIRE (Docs/SPINDLE_LOD.md §9), handed to
    /// <see cref="SpindleLodSystem"/>: a machined cable up close, unravelling into a spindle's lace on
    /// the prisms' depth ramp, a glowing thread far away, never culled. Added by
    /// <see cref="ToyFactory.AddSwitchRing"/>; the system owns the renderer's mesh and material while
    /// registered, and this hands them back (the wire and its near material) whenever the ring is
    /// disabled, repainted or destroyed.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SwitchRingLod : MonoBehaviour
    {
        Material _near;
        SpindleLodSystem.Handle _handle;

        /// <summary>Draw the ring with <paramref name="near"/> (its signal colour on SpindleWireGraph):
        /// a repaint gives the LOD the new colour's tiers.</summary>
        public void Bind(Material near)
        {
            if (_near == near && _handle is { IsRegistered: true }) return;
            Release(restore: true);
            _near = near;
            if (TryGetComponent(out Renderer ring)) ring.sharedMaterial = near;
            if (isActiveAndEnabled) Register();
        }

        void OnEnable()
        {
            if (_near && _handle == null) Register();
        }

        void OnDisable() => Release(restore: gameObject.scene.isLoaded);

        void OnDestroy() => Release(restore: false);

        void Register()
        {
            if (TryGetComponent(out Renderer ring))
                _handle = SpindleLodSystem.RegisterSwitchRing(ring, _near);
        }

        void Release(bool restore)
        {
            if (_handle == null) return;
            SpindleLodSystem.Unregister(_handle, restore);
            _handle = null;
        }
    }
}
