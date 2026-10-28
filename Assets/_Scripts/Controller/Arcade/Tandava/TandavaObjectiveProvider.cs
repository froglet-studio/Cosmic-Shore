using CosmicShore.UI;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Objective provider for Tandava (GameModes.Tandava). Shiva's Cell (the 2026-10-28 redesign, <see cref="ShivaController"/>):
    /// the lit ring of the circuit - the one thing the squad must reach next. The first design (<see cref="TandavaController"/>,
    /// kept as TandavaSevering's donor): the swarm, whose anchor follows its body's centre, so the arrow points at the animal.
    /// </summary>
    public class TandavaObjectiveProvider : MonoBehaviour, IObjectiveProvider
    {
        public bool TryGetObjective(out Transform target)
        {
            var shiva = ShivaController.Current;
            if (shiva) { target = shiva.LitRingTransform; return target; }
            var controller = TandavaController.Current;
            target = controller ? controller.SwarmTransform : null;
            return target;
        }
    }
}
