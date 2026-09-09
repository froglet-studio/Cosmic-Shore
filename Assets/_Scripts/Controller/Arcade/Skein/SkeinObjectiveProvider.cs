using CosmicShore.UI;
using CosmicShore.Utility;
using Reflex.Attributes;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Objective provider for Skein: the local pilot's NEXT ring.
    ///
    /// <para>This is the mode's whole answer to "which of these twenty-four rings is mine", and it
    /// is deliberately the only one. Repainting the next ring in the pilot's domain colour is the
    /// obvious alternative and is wrong twice: it spends the switch vocabulary's RESERVED domain
    /// colour on a ring that hands nobody a domain, and it makes two pilots flying side by side
    /// see different worlds. An arrow is per-viewer by construction, which is what a per-pilot
    /// fact needs.</para>
    /// </summary>
    public class SkeinObjectiveProvider : MonoBehaviour, IObjectiveProvider
    {
        [Inject] GameDataSO gameData;

        SkeinController _controller;

        public bool TryGetObjective(out Transform target)
        {
            target = null;
            if (gameData == null) return false;

            // Resolved lazily and re-resolved if it goes null: the HUD can build this provider
            // before the controller has network-spawned, and a scene-reload replay replaces it.
            if (_controller == null) _controller = FindAnyObjectByType<SkeinController>();
            if (_controller == null) return false;

            return _controller.TryGetNextGate(gameData.LocalPlayer, out target);
        }
    }
}
