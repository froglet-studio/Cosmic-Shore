using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A single fly-through station of a toy MATRIX - the shared "a toy expands into a wall of
    /// choices you fly at" pattern (the Lifeform Matrix's species/variant grids, the Cell
    /// Selector's mini-cell grid).
    ///
    /// Same local-vessel + freestyle gating as <see cref="Toy"/>, with a short per-station
    /// cooldown instead of the full exit-gated re-arm: stations sit close together, so the
    /// cooldown stops one pass from double-firing while still allowing rapid A/B selection.
    /// A matrix's stations are transient - built on a pass, torn down on the next - so they
    /// never contribute to the per-cell collider budget.
    /// </summary>
    public sealed class ToyMatrixStation : MonoBehaviour
    {
        const float CooldownSeconds = 1.5f;

        ToyContext _context;
        float _readyTime;

        /// <summary>What happens when the local vessel passes through.</summary>
        public System.Action OnVesselPassed;

        public void Bind(ToyContext context) => _context = context;

        void OnTriggerEnter(Collider other)
        {
            if (Time.time < _readyTime) return;
            if (!Toy.TryGetLocalVessel(other, out _)) return;
            if (_context?.IsFreestyleActive != null && !_context.IsFreestyleActive()) return;

            _readyTime = Time.time + CooldownSeconds;
            OnVesselPassed?.Invoke();
        }
    }
}
