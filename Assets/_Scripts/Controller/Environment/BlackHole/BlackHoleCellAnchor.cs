using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Ties a cell-environment black hole — or a dipole's two holes and their wormhole — to the world
    /// it belongs to (see <see cref="SpawnableBlackHole"/>). The container's FIRST parent is the cell
    /// that adopted it; any later re-parent is that cell retiring its world into the suction root,
    /// which is when the holes begin their eased despawn and the mouths wither. A hole destroyed
    /// outright with its cell (scene unload, reset) unregisters itself in <c>OnDisable</c> and needs
    /// nothing from here.
    ///
    /// <para>A dipole's SOURCE is also a pole of the cell's warp field
    /// (<see cref="WarpFieldRuntime.AddPole"/>), so a pilot shrinks toward the white hole as well as
    /// toward the black one — and grows as they climb away from it. The field keeps the pole's last
    /// position once the hole is gone, so it eases out with the field instead of popping.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BlackHoleCellAnchor : MonoBehaviour
    {
        BlackHole _hole;
        BlackHole _source;
        WormholeMouth _sinkMouth;
        WormholeMouth _sourceMouth;
        Transform _adoptedBy;
        bool _released;

        /// <summary>How long a retiring world's mouths take to wither — about the cell's suction.</summary>
        const float MouthWitherSeconds = 0.8f;

        internal void Bind(BlackHole hole) => _hole = hole;

        internal void BindSource(BlackHole source)
        {
            _source = source;
            if (source) WarpFieldRuntime.AddPole(source.transform);
        }

        internal void BindMouths(WormholeMouth sinkMouth, WormholeMouth sourceMouth)
        {
            _sinkMouth = sinkMouth;
            _sourceMouth = sourceMouth;
        }

        void OnTransformParentChanged()
        {
            var parent = transform.parent;
            if (!parent) return;

            if (!_adoptedBy)
            {
                _adoptedBy = parent;
                return;
            }

            if (parent == _adoptedBy) return;
            Release();
        }

        void Release()
        {
            if (_released) return;
            _released = true;

            if (_hole && !_hole.IsDespawning)
            {
                _hole.BeginDespawn();
                CSDebug.LogVerbose(CSLogChannel.BlackHole,
                    $"[BlackHole] #{_hole.Id} released: its cell retired the world it was the centre of.");
            }
            if (_source && !_source.IsDespawning) _source.BeginDespawn();
            if (_sinkMouth) _sinkMouth.Retire(MouthWitherSeconds);
            if (_sourceMouth) _sourceMouth.Retire(MouthWitherSeconds);
        }
    }
}
