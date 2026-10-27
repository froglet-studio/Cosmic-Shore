using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>What one frame of a sight asks of the prism shader (PrismDestructionSight.hlsl).</summary>
    public struct PilotSightPrismState
    {
        /// <summary>The PilotVision value the shader keeps lit, or 0 for none (every prism suppressed).</summary>
        public float Sight;
        /// <summary>ENSIGN's domain.</summary>
        public float Domain;
        /// <summary>MANGER's sphere (centre.xyz, radius).</summary>
        public Vector4 Volume;
    }

    /// <summary>Who is looking, and with what, for one frame.</summary>
    public readonly struct PilotSightContext
    {
        public readonly IVesselStatus Viewer;
        public readonly Domains Domain;
        public readonly float Strength;
        public readonly CallsignSetSO Set;
        public readonly SO_ColorSet Colors;

        public PilotSightContext(IVesselStatus viewer, Domains domain, float strength, CallsignSetSO set, SO_ColorSet colors)
        {
            Viewer = viewer;
            Domain = domain;
            Strength = strength;
            Set = set;
            Colors = colors;
        }
    }

    /// <summary>
    /// One sight (Docs/Pilots/CALLSIGNS.md §6.4): a prism predicate, a volume, or a target set. An
    /// eighteenth sight is one subclass plus its row in <see cref="PilotSightProviders.For"/>.
    /// Photons only: a provider reads the world and writes rendering state, nothing else.
    /// </summary>
    public abstract class PilotSightProvider
    {
        public abstract void Gather(in PilotSightContext ctx, ref PilotSightPrismState prisms, PilotSightMarks marks);
    }

    /// <summary>A sight of PRISMS by their state, answered entirely in the prism shader (ENSIGN,
    /// CITADEL; RAMPART and HORNET once the prism carries those states per instance).</summary>
    public sealed class PrismPredicateSight : PilotSightProvider
    {
        readonly PilotVision _sight;
        public PrismPredicateSight(PilotVision sight) => _sight = sight;

        public override void Gather(in PilotSightContext ctx, ref PilotSightPrismState prisms, PilotSightMarks marks)
        {
            prisms.Sight = (float)_sight;
            // ENSIGN shows YOUR domain's mass (CALLSIGNS.md §2; decided by Garrett 2026-10-27).
            prisms.Domain = (float)ctx.Domain;
        }
    }

    /// <summary>A sight of CRYSTALS of one element: field, ejected and the hearts lifeforms carry,
    /// all of which are live <see cref="Crystal"/>s. Marked white: elements have no colour (LOCKED).</summary>
    public sealed class CrystalSight : PilotSightProvider
    {
        readonly Element _element;
        public CrystalSight(Element element) => _element = element;

        public override void Gather(in PilotSightContext ctx, ref PilotSightPrismState prisms, PilotSightMarks marks)
        {
            var set = ctx.Set;
            float radius = set ? set.CrystalMarkRadius : 6f;
            var color = set ? set.CrystalMarkColor : Color.white;
            float intensity = (set ? set.MarkIntensity : 1f) * ctx.Strength;
            var crystals = Crystal.Active;
            for (int i = 0; i < crystals.Count; i++)
            {
                var crystal = crystals[i];
                if (!crystal || crystal.crystalProperties.Element != _element) continue;
                var t = crystal.transform;
                marks.Add(t.position, radius * Mathf.Max(0.2f, t.lossyScale.x), color, intensity);
            }
        }
    }

    /// <summary>A sight of VESSELS by side (WINGMAN, BANDIT), each marked in its own domain colour,
    /// through mass. Never the viewer's own hull.</summary>
    public sealed class VesselSight : PilotSightProvider
    {
        readonly bool _rivals;
        public VesselSight(bool rivals) => _rivals = rivals;

        public override void Gather(in PilotSightContext ctx, ref PilotSightPrismState prisms, PilotSightMarks marks)
        {
            var set = ctx.Set;
            float radius = set ? set.VesselMarkRadius : 12f;
            float intensity = (set ? set.MarkIntensity : 1f) * ctx.Strength;
            var live = VesselStatus.Live;
            for (int i = 0; i < live.Count; i++)
            {
                var hull = live[i];
                if (!hull) continue;
                IVesselStatus vessel = hull;
                if (ReferenceEquals(vessel, ctx.Viewer) || vessel.Player == null) continue;
                var domain = vessel.Domain;
                if ((domain != ctx.Domain) != _rivals) continue;
                var color = ctx.Colors ? ctx.Colors.GetDomainSignalColor(domain) : Color.white;
                marks.Add(hull.transform.position, radius, color, intensity);
            }
        }
    }

    /// <summary>
    /// FLUME: every soliton, whoever made it, traced along its length so you see where it is and where it leads
    /// (Docs/Pilots/CALLSIGNS.md §2, row 18). The Meerkat's tunnels are the only solitons so far
    /// (<see cref="MeerkatWormholeExecutor.Live"/>, read only). Marked white: a soliton belongs to no one.
    /// </summary>
    public sealed class FlumeSight : PilotSightProvider
    {
        /// <summary>Marks along each tunnel, mouth to mouth.</summary>
        const int Samples = 9;

        public override void Gather(in PilotSightContext ctx, ref PilotSightPrismState prisms, PilotSightMarks marks)
        {
            var set = ctx.Set;
            float minRadius = set ? set.CrystalMarkRadius : 6f;
            var color = set ? set.CrystalMarkColor : Color.white;
            float intensity = (set ? set.MarkIntensity : 1f) * ctx.Strength;
            var live = MeerkatWormholeExecutor.Live;
            for (int i = 0; i < live.Count; i++)
            {
                var tunnel = live[i];
                if (tunnel == null || tunnel.L <= 0f) continue;
                float fade = intensity * tunnel.Fade;
                if (fade <= 0f) continue;
                for (int k = 0; k < Samples; k++)
                {
                    float at = tunnel.L * k / (Samples - 1);
                    marks.Add(tunnel.At(at, out _), Mathf.Max(minRadius, tunnel.Radius(at)), color, fade);
                }
            }
        }
    }

    /// <summary>The sight behind each callsign. Null for a callsign whose sight is not built yet.</summary>
    public static class PilotSightProviders
    {
        // The four cheapest sights first (the build prompt's order): prove the look, then add the rest.
        static readonly PilotSightProvider Ensign = new PrismPredicateSight(PilotVision.Ensign);
        static readonly PilotSightProvider Citadel = new PrismPredicateSight(PilotVision.Citadel);
        static readonly PilotSightProvider Volt = new CrystalSight(Element.Charge);
        static readonly PilotSightProvider Bandit = new VesselSight(rivals: true);
        static readonly PilotSightProvider Flume = new FlumeSight();

        public static PilotSightProvider For(PilotVision sight) => sight switch
        {
            PilotVision.Ensign => Ensign,
            PilotVision.Citadel => Citadel,
            PilotVision.Volt => Volt,
            PilotVision.Bandit => Bandit,
            PilotVision.Flume => Flume,
            _ => null,
        };
    }
}
