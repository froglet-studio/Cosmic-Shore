using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Domain-changer toy set. Shows one <b>switch</b> per team colour you are NOT currently on
    /// (Jade/Ruby/Gold minus current - always two in a 3-domain session). Each one is a ring in the
    /// prism material of the domain it will turn you into; threading it requests that domain, and
    /// the switch then flips to the domain you just left. Domain changes route through the
    /// server-authoritative <c>Player.RequestSetDomain_ServerRpc</c>.
    ///
    /// <para>This is the toy the switch vocabulary is built around: <b>a switch wearing a playable
    /// domain's colour is one that hands you that domain</b>, and nothing else in the toybox may
    /// wear one (<see cref="ToySwitchSignal"/>). The set used to be cones you flew at instead -
    /// that shape is now reserved for a booster.</para>
    /// </summary>
    public class DomainChangerToySet : SwapToySetCoordinator<Domains>
    {
        /// <summary>Hub radius as a fraction of the slot's body radius - a core you can pick out
        /// from across the cell, well inside the ring's inner rim.</summary>
        const float HubBodyFraction = 0.5f;

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

            // THE SWITCH IS THE TOY. This set used to be bodies you flew at - a cone in the
            // domain's prism material, apex pointing the way through - and that cone is now
            // reserved for a booster (see ToyFactory's shape-language note). What replaced it is
            // not a smaller cone but the platform's own word for "thread me and something
            // happens", carrying its meaning in its SHADER: a DOMAIN-signalled switch, which is
            // the one thing a domain-coloured ring is allowed to be.
            //
            // Set on the toy rather than built here, because the ring belongs to Toy (drawn from
            // its own trigger collider, so it can never advertise a volume that does not fire) -
            // and because a slot FLIPS to the domain you just left, which repaints the live ring.
            slot.Toy.SetSwitchSignal(ToySwitchSignal.Domain, slot.Option);

            // A hub in the same prism material, so the ring reads as one object at range rather
            // than as a thin hoop. Deliberately a sphere: it makes no claim about direction.
            ToyFactory.AddSphereBody(slot.BodyHolder, BodyRadius * HubBodyFraction, c,
                ToyFactory.SwitchMaterial(ToyFactory.Theme(Context), ToySwitchSignal.Domain, slot.Option));

            if (slot.Label)
            {
                slot.Label.text = LabelFor(slot.Option);
                slot.Label.color = c;
            }
        }

        Color DomainColor(Domains d) => ToyFactory.DomainAccentColor(Context, d);
    }
}
