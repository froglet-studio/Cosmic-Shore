using CosmicShore.UI;
using CosmicShore.Utility;
using Reflex.Attributes;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Objective provider for Switchback: the local pilot's NEXT gate.
    ///
    /// <para>This is the mode's whole answer to "which of these twenty identical rings is mine",
    /// and it is deliberately the only answer. Repainting the next gate in the pilot's domain
    /// colour was the obvious alternative and is wrong twice: it spends the switch vocabulary's
    /// reserved domain colour on something that hands nobody a domain, and it makes two pilots
    /// flying side by side see different worlds. The arrow is per-viewer by construction, which
    /// is exactly what a per-pilot fact needs.</para>
    ///
    /// <para>Cheap by shape rather than by caching: the controller already indexes its rings by
    /// gate number and the pilot's progress IS that index, so answering is one array lookup - no
    /// scene scan, no dirty flag, no allocation.</para>
    /// </summary>
    public class SwitchbackObjectiveProvider : MonoBehaviour, IObjectiveProvider
    {
        [Inject] GameDataSO gameData;

        SwitchbackController _controller;

        public bool TryGetObjective(out Transform target)
        {
            target = null;
            if (gameData == null) return false;

            // Resolved lazily and re-resolved if it goes null: the HUD can build this provider
            // before the controller has network-spawned, and a scene reload replay replaces it.
            if (_controller == null)
                _controller = FindAnyObjectByType<SwitchbackController>();
            if (_controller == null) return false;

            return _controller.TryGetNextGate(gameData.LocalPlayer, out target);
        }
    }
}
