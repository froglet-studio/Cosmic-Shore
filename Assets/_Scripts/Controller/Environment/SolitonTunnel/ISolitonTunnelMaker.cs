using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Whoever makes a Soliton Tunnel or Network (the Meerkat's <see cref="MeerkatSolitonExecutor"/>; a scene's
    /// <see cref="SolitonTunnelPlacer"/> makes them with no maker at all). The tube asks it only for its domain colour (the
    /// default tint, <see cref="SolitonTunnelConfigSO.Tint"/>); everything else a tube needs is on its own
    /// <see cref="SolitonTunnelConfigSO"/>, so nothing about a Soliton Tunnel is Meerkat-only.
    /// </summary>
    public interface ISolitonTunnelMaker
    {
        /// <summary>The maker's domain colour (its team's), the tube's tint by default.</summary>
        Color DomainColour { get; }
    }
}
