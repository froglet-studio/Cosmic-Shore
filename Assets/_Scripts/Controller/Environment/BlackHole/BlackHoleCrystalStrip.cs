using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The black-hole half of a pass-through toll: a vessel carried through a SINK whose owner set a
    /// <see cref="BlackHole.CrystalStripShare"/> loses that share of every element it holds, left as
    /// crystals around the sink on the side it went in — the ATTRACTOR side, where the pair's owner can
    /// sweep them up (the Stoat's dipole, <c>R_VesselActions/STOAT_DIPOLE.md</c>).
    ///
    /// <para>The same shape as the Butterfly's <see cref="WormholeMouth.LevyToll"/> and called from the
    /// same place (<see cref="TeleportContinuity"/>, where every pose write lands): the take is
    /// <see cref="ResourceSystem.AccrueElementalLoss"/>, so it conserves (whole petals, clamped to what is
    /// held, a ward against <see cref="ElementalDebuffSources.WormholeToll"/> honoured) and every petal
    /// becomes exactly one crystal. The owner never pays, nor does a teammate.</para>
    /// </summary>
    public static class BlackHoleCrystalStrip
    {
        /// <summary>How far past the horizon the jump's departure may sit and still count as this sink's
        /// (the carry fires on the first frame the centre is inside; a frame's travel is the slack).</summary>
        const float EntrySlackHorizons = 1.5f;
        const float SpreadDegrees = 70f;
        const float ShedSpeed = 6f;

        static readonly Element[] StripElements = { Element.Charge, Element.Mass, Element.Space, Element.Time };

        /// <summary>The strip rule with every read already made — pure, for the edit-mode tests.</summary>
        public static bool OwesStrip(float share, bool isOwner, bool pilotDomainKnown, Domains pilotDomain, Domains holeDomain) =>
            share > 0f && !isOwner && pilotDomainKnown && pilotDomain != holeDomain;

        /// <summary>
        /// <paramref name="status"/> just jumped from <paramref name="from"/>: if that was inside a stripping
        /// sink, take its toll. Returns the whole petals taken.
        /// </summary>
        public static int Levy(IVesselStatus status, Vector3 from)
        {
            if (status == null) return 0;
            var holes = BlackHoleRegistry.Holes;
            for (int i = 0; i < holes.Count; i++)
            {
                var hole = holes[i];
                if (hole == null || hole.IsSource || hole.IsDespawning || hole.CrystalStripShare <= 0f) continue;
                var centre = hole.transform.position;
                float reach = hole.HorizonRadius * EntrySlackHorizons;
                if ((from - centre).sqrMagnitude > reach * reach) continue;
                return Strip(status, hole, from);
            }
            return 0;
        }

        static int Strip(IVesselStatus status, BlackHole hole, Vector3 entryPoint)
        {
            bool isOwner = hole.OwnerVessel != null && status is Component c && c && c.transform == hole.OwnerVessel;
            // IVesselStatus.Domain reads Player and logs when there is none; ask first.
            bool known = status.Player != null;
            if (!OwesStrip(hole.CrystalStripShare, isOwner, known, known ? status.Domain : Domains.Blue, hole.CaptureDomain))
                return 0;
            var resources = status.ResourceSystem;
            if (resources == null) return 0;

            var centre = hole.transform.position;
            float share = Mathf.Clamp01(hole.CrystalStripShare);
            int total = 0;
            for (int i = 0; i < StripElements.Length; i++)
            {
                var element = StripElements[i];
                // Round up to whole petals so a full strip takes the last one (AccrueElementalLoss clamps).
                float amount = Mathf.Ceil(resources.TakeableLevel(element) * share / ResourceSystem.PetalNormalized - 1e-3f)
                               * ResourceSystem.PetalNormalized;
                int petals = resources.AccrueElementalLoss(element, amount, ElementalDebuffSources.WormholeToll);
                if (petals <= 0) continue;
                ElementalCrystalEjector.ShedOntoSphere(centre, hole.HorizonRadius * EntrySlackHorizons, entryPoint, SpreadDegrees,
                                                       element, petals, ShedSpeed, status.PlayerName);
                total += petals;
            }
            return total;
        }
    }
}
