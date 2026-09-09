using CosmicShore.UI;
using CosmicShore.Utility;
using Reflex.Attributes;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Objective provider for Breakwater: the local pilot's NEXT station.
    ///
    /// <para>This is half of the mode's answer to "which of these fourteen identical dishes is
    /// mine", and it is deliberately the PER-VIEWER half. Repainting the next station in the
    /// pilot's DOMAIN colour was the obvious alternative and is wrong twice: it spends the switch
    /// vocabulary's reserved domain colour on something that hands nobody a domain, and it makes
    /// two pilots flying side by side see different worlds - worse here than in Switchback,
    /// because a Breakwater station is a 257-prism landmark rather than a bare ring, so a
    /// domain-coloured one would also read as mass somebody owns in an arena where every prism is
    /// deliberately <c>Domains.Blue</c> and therefore hostile to everyone. The arrow is per-viewer
    /// by construction, which is exactly what a per-pilot fact needs.</para>
    ///
    /// <para>The other half is the controller's own <c>LightLocalNextStation</c>, which paints
    /// that same ring lime on this machine only. The two are not redundant: the arrow says which
    /// DIRECTION to fly when the station is off screen, the lime says "this one" once several
    /// dishes are in frame at once. Both read the pilot's live <c>SwitchesThreaded</c>, so they
    /// cannot point at different stations.</para>
    ///
    /// <para>Cheap by shape rather than by caching: the controller already indexes its rings by
    /// station number and the pilot's progress IS that index, so answering is one array lookup -
    /// no scene scan, no dirty flag, no allocation.</para>
    /// </summary>
    public class BreakwaterObjectiveProvider : MonoBehaviour, IObjectiveProvider
    {
        [Inject] GameDataSO gameData;

        BreakwaterController _controller;

        public bool TryGetObjective(out Transform target)
        {
            target = null;
            if (gameData == null) return false;

            // Resolved lazily and re-resolved if it goes null: the HUD can build this provider
            // before the controller has network-spawned, and a scene reload replay replaces it.
            if (_controller == null)
                _controller = FindAnyObjectByType<BreakwaterController>();
            if (_controller == null) return false;

            return _controller.TryGetNextStation(gameData.LocalPlayer, out target);
        }
    }
}
