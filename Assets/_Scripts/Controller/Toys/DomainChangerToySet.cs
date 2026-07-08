using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Domain-changer toy set. Shows one toy per team colour you are NOT currently on (Jade/Ruby/Gold
    /// minus current — always two in a 3-domain session). Each toy is tinted the domain it will turn
    /// you into; flying through it requests that domain, and the toy then flips to the domain you just
    /// left. Domain changes route through the server-authoritative <c>Player.RequestSetDomain_ServerRpc</c>.
    /// </summary>
    public class DomainChangerToySet : SwapToySetCoordinator<Domains>
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

        protected override void ConfigureVisual(Slot slot)
        {
            ClearChildren(slot.BodyHolder);
            Color c = DomainColor(slot.Option);
            ToyFactory.AddSphereBody(slot.BodyHolder, BodyRadius, c);
            if (slot.Label)
            {
                slot.Label.text = LabelFor(slot.Option);
                slot.Label.color = c;
            }
        }

        Color DomainColor(Domains d)
        {
            var tm = Context.GameData ? Context.GameData.ThemeManagerData : null;
            if (tm) return tm.GetDomainUIColor(d);
            return d switch
            {
                Domains.Jade => new Color(0.15f, 0.95f, 0.55f),
                Domains.Ruby => new Color(1.00f, 0.20f, 0.45f),
                Domains.Gold => new Color(1.00f, 0.80f, 0.15f),
                _ => Color.gray,
            };
        }
    }
}
