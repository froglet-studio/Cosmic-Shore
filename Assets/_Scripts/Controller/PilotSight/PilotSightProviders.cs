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
    /// (Docs/Pilots/CALLSIGNS.md §2, row 18). Every Soliton Tunnel, made or placed
    /// (<see cref="SolitonTunnel.Live"/>, read only). Marked white: a soliton belongs to no one.
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
            var live = SolitonTunnel.Live;
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

    /// <summary>
    /// MANGER: the mass cluster the fauna are heading for (Docs/Pilots/CALLSIGNS.md §2, row 2), answered in the prism
    /// shader as a sphere around the densest region of the viewer's cell, the same point a grazer steers to
    /// (<see cref="Cell.TryGetDensestRegionAnyDomain"/>). A cell holding no mass lights nothing.
    /// </summary>
    public sealed class MangerSight : PilotSightProvider
    {
        /// <summary>Radius of the lit cluster, world units: about one grazing swarm's reach.</summary>
        public const float ClusterRadius = 220f;

        public override void Gather(in PilotSightContext ctx, ref PilotSightPrismState prisms, PilotSightMarks marks)
        {
            prisms.Sight = (float)PilotVision.Manger;
            var at = ctx.Viewer?.Transform ? ctx.Viewer.Transform.position : Vector3.zero;
            var cell = Cell.FindCellContaining(at);
            if (!cell || !cell.TryGetDensestRegionAnyDomain(out var region)) return;
            prisms.Volume = new Vector4(region.x, region.y, region.z, ClusterRadius);
        }
    }

    /// <summary>
    /// GALLEON: the Arks, and what rides with them (CALLSIGNS.md §2, row 17): every standing Ark in its own domain
    /// colour, and every artifact loose or carried (<see cref="ArtifactPickup"/>), white, so a wreck's spill and a
    /// raider running home with a find both show through mass.
    /// </summary>
    public sealed class GalleonSight : PilotSightProvider
    {
        /// <summary>An Ark is far bigger than a vessel: its mark is this many vessel marks wide.</summary>
        const float ArkMarkScale = 3f;

        public override void Gather(in PilotSightContext ctx, ref PilotSightPrismState prisms, PilotSightMarks marks)
        {
            var set = ctx.Set;
            float vesselRadius = set ? set.VesselMarkRadius : 12f;
            float crystalRadius = set ? set.CrystalMarkRadius : 6f;
            var white = set ? set.CrystalMarkColor : Color.white;
            float intensity = (set ? set.MarkIntensity : 1f) * ctx.Strength;
            var holds = ArkHold.All;
            for (int i = 0; i < holds.Count; i++)
            {
                var ark = holds[i] ? holds[i].Ark : null;
                if (!ark || ark.IsHullLost) continue;
                var color = ctx.Colors ? ctx.Colors.GetDomainSignalColor(ark.Domain) : white;
                marks.Add(ark.transform.position, vesselRadius * ArkMarkScale, color, intensity);
            }
            var loose = ArtifactPickup.Loose;
            for (int i = 0; i < loose.Count; i++)
                if (loose[i]) marks.Add(loose[i].transform.position, crystalRadius * 2f, white, intensity);
        }
    }

    /// <summary>IVY: every plant (CALLSIGNS.md §2, row 11), marked white at its heart.</summary>
    public sealed class FloraSight : PilotSightProvider
    {
        public override void Gather(in PilotSightContext ctx, ref PilotSightPrismState prisms, PilotSightMarks marks)
        {
            var set = ctx.Set;
            float radius = set ? set.CrystalMarkRadius : 6f;
            var color = set ? set.CrystalMarkColor : Color.white;
            float intensity = (set ? set.MarkIntensity : 1f) * ctx.Strength;
            var live = FloraHeartRegistry.Live;
            for (int i = 0; i < live.Count; i++)
                if (live[i]) marks.Add(live[i].transform.position, radius * 1.5f, color, intensity);
        }
    }

    /// <summary>
    /// TALON and DOE: every predator, or every herbivore (CALLSIGNS.md §2, rows 12 and 13), in the colour of the domain
    /// it fights for. There is no live list of fauna, so the sight takes a census of the scene's fauna every
    /// <see cref="CensusSeconds"/> while it is held, never per frame, and draws the census's live positions each frame.
    /// Photons only: it reads the creatures and changes nothing.
    /// </summary>
    public sealed class FaunaSight : PilotSightProvider
    {
        const float CensusSeconds = 0.5f;

        readonly FaunaDiet _diet;
        readonly System.Collections.Generic.List<Fauna> _census = new();
        float _nextCensusAt;

        public FaunaSight(FaunaDiet diet) => _diet = diet;

        public override void Gather(in PilotSightContext ctx, ref PilotSightPrismState prisms, PilotSightMarks marks)
        {
            if (Time.unscaledTime >= _nextCensusAt)
            {
                _nextCensusAt = Time.unscaledTime + CensusSeconds;
                _census.Clear();
                var all = Object.FindObjectsByType<Fauna>(FindObjectsSortMode.None);
                for (int i = 0; i < all.Length; i++)
                    if (all[i] && all[i].Diet == _diet) _census.Add(all[i]);
            }
            var set = ctx.Set;
            float radius = set ? set.CrystalMarkRadius * 1.5f : 9f;
            float intensity = (set ? set.MarkIntensity : 1f) * ctx.Strength;
            var white = set ? set.CrystalMarkColor : Color.white;
            for (int i = 0; i < _census.Count; i++)
            {
                var f = _census[i];
                if (!f || !f.isActiveAndEnabled || !f.IsAlivePrey) continue;
                var color = ctx.Colors ? ctx.Colors.GetDomainSignalColor(f.Domain) : white;
                marks.Add(f.transform.position, radius, color, intensity);
            }
        }
    }

    /// <summary>The sight behind each callsign. Null for a callsign whose sight is not built yet.</summary>
    public static class PilotSightProviders
    {
        // Built: fifteen of the eighteen. RAMPART and HORNET wait on the prism carrying its shielded and danger states
        // per instance (the shader's predicate needs them); NEEDLE waits on a registry of switches.
        static readonly PilotSightProvider Ensign = new PrismPredicateSight(PilotVision.Ensign);
        static readonly PilotSightProvider Manger = new MangerSight();
        static readonly PilotSightProvider Anvil = new CrystalSight(Element.Mass);
        static readonly PilotSightProvider Volt = new CrystalSight(Element.Charge);
        static readonly PilotSightProvider Zodiac = new CrystalSight(Element.Space);
        static readonly PilotSightProvider Sundial = new CrystalSight(Element.Time);
        static readonly PilotSightProvider Opal = new CrystalSight(Element.Omni);
        static readonly PilotSightProvider Citadel = new PrismPredicateSight(PilotVision.Citadel);
        static readonly PilotSightProvider Ivy = new FloraSight();
        static readonly PilotSightProvider Talon = new FaunaSight(FaunaDiet.Predator);
        static readonly PilotSightProvider Doe = new FaunaSight(FaunaDiet.Herbivore);
        static readonly PilotSightProvider Wingman = new VesselSight(rivals: false);
        static readonly PilotSightProvider Bandit = new VesselSight(rivals: true);
        static readonly PilotSightProvider Galleon = new GalleonSight();
        static readonly PilotSightProvider Flume = new FlumeSight();

        public static PilotSightProvider For(PilotVision sight) => sight switch
        {
            PilotVision.Ensign => Ensign,
            PilotVision.Manger => Manger,
            PilotVision.Anvil => Anvil,
            PilotVision.Volt => Volt,
            PilotVision.Zodiac => Zodiac,
            PilotVision.Sundial => Sundial,
            PilotVision.Opal => Opal,
            PilotVision.Citadel => Citadel,
            PilotVision.Ivy => Ivy,
            PilotVision.Talon => Talon,
            PilotVision.Doe => Doe,
            PilotVision.Wingman => Wingman,
            PilotVision.Bandit => Bandit,
            PilotVision.Galleon => Galleon,
            PilotVision.Flume => Flume,
            _ => null,
        };
    }
}
