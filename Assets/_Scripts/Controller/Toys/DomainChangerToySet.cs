using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Domain-changer toy set. Shows one toy per team colour you are NOT currently on (Jade/Ruby/Gold
    /// minus current - always two in a 3-domain session). Each toy is tinted the domain it will turn
    /// you into; flying through it requests that domain, and the toy then flips to the domain you just
    /// left. Domain changes route through the server-authoritative <c>Player.RequestSetDomain_ServerRpc</c>.
    /// </summary>
    public class DomainChangerToySet : SwapToySetCoordinator<Domains>
    {
        // No switch ring here, by design: the cone IS this set's read. Its apex points the way you
        // fly through, it is rebuilt in the target domain's prism material on every flip, and a
        // ring around it would say a second time what the cone already says once.
        protected override bool SlotsWearSwitchRing => false;

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
            // Shared trail-changer shape language: a cone in the domain's PRISM material, apex
            // pointing the way you fly through (local +Z faces the ring centre) - the same shape
            // the painting toy's stroke gates wear, so each teaches the other.
            ToyFactory.AddConeBody(slot.BodyHolder, BodyRadius * 0.95f, BodyRadius * 2.6f, c,
                ToyFactory.DomainPrismMaterial(Context, slot.Option));
            if (slot.Label)
            {
                slot.Label.text = LabelFor(slot.Option);
                slot.Label.color = c;
            }
        }

        Color DomainColor(Domains d) => ToyFactory.DomainAccentColor(Context, d);
    }
}
