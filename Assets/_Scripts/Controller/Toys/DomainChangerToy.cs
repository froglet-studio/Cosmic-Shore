using System;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Cycles the local player's domain (team colour) on each pass: Jade → Ruby → Gold (within
    /// the session's active-domain slice). Routes through the server-authoritative
    /// <c>Player.RequestSetDomain_ServerRpc</c> — never a client-local write — so the recolour
    /// replicates to every peer via <c>Player.NetDomain</c>.
    /// </summary>
    public class DomainChangerToy : Toy
    {
        protected override void OnActivated(IVesselStatus localVessel)
        {
            if (Context?.GameData?.LocalPlayer is not Player player)
                return;
            if (!player.IsOwner)
                return;

            var active = GameDataSO.ActiveDomains; // { Jade, Ruby, Gold }
            int dc = Mathf.Clamp(Context.GameData.RequestedDomainCount, 1, active.Length);

            int idx = Array.IndexOf(active, player.NetDomain.Value);
            // Start at Jade if the current domain is outside the active slice; else advance.
            int next = (idx < 0 || idx >= dc) ? 0 : (idx + 1) % dc;

            player.RequestSetDomain_ServerRpc(active[next]);
        }
    }
}
