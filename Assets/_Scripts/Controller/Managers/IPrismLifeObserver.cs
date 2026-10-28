using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Watches every prism's life through <see cref="PrismSpatialIndex.LifeObserver"/>: born when
    /// it registers, gone when it is destroyed or leaves the index, restored when a destroyed prism
    /// comes back, moved when its indexed position changes. A slot number is reused after a prism is
    /// gone, so an observer keys a life by the slot between a birth and its gone. Called on the main
    /// thread, from inside the index - an observer only records, it never touches the index back.
    ///
    /// <para>A VIRTUAL entry (a creature's body member, <see cref="PrismSpatialIndex.RegisterVirtual"/>)
    /// has no <see cref="Prism"/>: it is born through <see cref="OnVirtualBorn"/>, and it is restored
    /// (<see cref="OnPrismRestored"/>) with a null prism, meaning "where it last was".</para>
    /// </summary>
    public interface IPrismLifeObserver
    {
        void OnPrismBorn(int slot, Prism prism);
        void OnPrismGone(int slot);
        /// <summary>A destroyed entry came back. <paramref name="prism"/> is null for a virtual entry.</summary>
        void OnPrismRestored(int slot, Prism prism);

        /// <summary>A virtual entry registered: a body member with a position, a domain and a reach, but no GameObject.</summary>
        void OnVirtualBorn(int slot, Vector3 position, CosmicShore.Data.Domains domain, float radius, bool shielded);
        void OnPrismMoved(int slot, Vector3 position);
    }
}
