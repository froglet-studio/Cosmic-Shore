using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The object a crystal PAIR (Docs/BLACK_HOLE.md §13 — a sling or a tool spawn) is opened on. A
    /// <see cref="CrystalWormhole"/> tidies its wells and mouths away when it is gone but never its host,
    /// because the crystal cell's host is the cell's own environment; a pair spawned on its own has no
    /// such owner, so this destroys the host once both wells are gone. Not sooner: the wells are the
    /// host's children and ease out of the warp before destroying themselves (<c>BlackHole.BeginDespawn</c>),
    /// so destroying the host at <see cref="CrystalWormhole.IsGone"/> would cut that ease short. The same
    /// test covers the tool's Despawn all, which takes the wells while the pair still stands.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CrystalPairHost : MonoBehaviour
    {
        CrystalWormhole _wormhole;

        public void Bind(CrystalWormhole wormhole) => _wormhole = wormhole;

        void Update()
        {
            if (!_wormhole || (!_wormhole.Attractor && !_wormhole.Repulsor)) Destroy(gameObject);
        }
    }
}
