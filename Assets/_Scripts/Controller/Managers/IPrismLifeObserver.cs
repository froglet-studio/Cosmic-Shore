using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Watches every real prism's life through <see cref="PrismSpatialIndex.LifeObserver"/>: born when
    /// it registers, gone when it is destroyed or leaves the index, restored when a destroyed prism
    /// comes back, moved when its indexed position changes. A slot number is reused after a prism is
    /// gone, so an observer keys a life by the slot between a birth and its gone. Called on the main
    /// thread, from inside the index - an observer only records, it never touches the index back.
    /// </summary>
    public interface IPrismLifeObserver
    {
        void OnPrismBorn(int slot, Prism prism);
        void OnPrismGone(int slot);
        void OnPrismRestored(int slot, Prism prism);
        void OnPrismMoved(int slot, Vector3 position);
    }
}
