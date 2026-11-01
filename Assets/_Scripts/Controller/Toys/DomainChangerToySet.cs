using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Domain-changer toy: one Toy Box row per team colour (Jade/Ruby/Gold), the one you wear
    /// flagged. Picking a row requests that domain through the server-authoritative
    /// <c>Player.RequestSetDomain_ServerRpc</c>.
    /// </summary>
    public class DomainChangerToySet : ToyOptionSet<Domains>
    {
        protected override IReadOnlyList<Domains> InitialUniverse()
        {
            int dc = Mathf.Clamp(Context.GameData ? Context.GameData.RequestedDomainCount : 3,
                1, GameDataSO.ActiveDomains.Length);
            var list = new List<Domains>(dc);
            for (int i = 0; i < dc; i++) list.Add(GameDataSO.ActiveDomains[i]);
            return list;
        }

        protected override bool TryGetCurrent(out Domains current)
        {
            current = Domains.Blue;
            var lp = Context.GameData ? Context.GameData.LocalPlayer : null;
            if (lp == null) return false;
            current = lp.Domain;
            return true;
        }

        protected override bool IsValid(Domains d) => d != Domains.Blue;

        protected override void Apply(Domains target)
        {
            if (Context.GameData?.LocalPlayer is Player p && p.IsOwner)
                p.RequestSetDomain_ServerRpc(target);
        }

        /// <summary>A domain HAS a colour, and it is the whole point of this toy.</summary>
        protected override Color ColorFor(Domains option) => ToyFactory.DomainAccentColor(Context, option);
    }
}
