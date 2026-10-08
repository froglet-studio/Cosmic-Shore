using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Ties a cell-environment black hole to the world it belongs to (see
    /// <see cref="SpawnableBlackHole"/>). The container's FIRST parent is the cell that adopted it;
    /// any later re-parent is that cell retiring its world into the suction root, which is when the
    /// hole begins its eased despawn. A hole destroyed outright with its cell (scene unload, reset)
    /// unregisters itself in <c>OnDisable</c> and needs nothing from here.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BlackHoleCellAnchor : MonoBehaviour
    {
        BlackHole _hole;
        Transform _adoptedBy;

        internal void Bind(BlackHole hole) => _hole = hole;

        void OnTransformParentChanged()
        {
            var parent = transform.parent;
            if (!parent) return;

            if (!_adoptedBy)
            {
                _adoptedBy = parent;
                return;
            }

            if (parent == _adoptedBy || !_hole || _hole.IsDespawning) return;
            _hole.BeginDespawn();
            CSDebug.LogVerbose(CSLogChannel.BlackHole,
                $"[BlackHole] #{_hole.Id} released: its cell retired the world it was the centre of.");
        }
    }
}
