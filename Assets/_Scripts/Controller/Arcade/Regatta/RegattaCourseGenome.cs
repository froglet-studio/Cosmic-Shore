using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// One thing the genome places on the course: a segment spawner (any usable <see cref="RegattaGeneCatalogue"/>
    /// kind) generated with its own parameters, scaled to a bounding radius, posed in the spine's frame at a point
    /// along the lap, and laid in a colour and a prism kind.
    /// </summary>
    [Serializable]
    public sealed class RegattaPlacement
    {
        /// <summary>The catalogue id (the spawner's class name without "Spawnable": Gyroid, Kelpwood, ...).</summary>
        public string Kind;
        /// <summary>Where along the lap: leg + Hermite parameter on that leg (2.5 = halfway from ring 2 to ring 3).</summary>
        public float Leg;
        /// <summary>Distance of the placement's centre from the spine (u).</summary>
        public float Radius;
        /// <summary>Degrees round the spine's tangent, 0 = straight out from the cell centre, 90 = tangent x out.</summary>
        public float Bearing;
        /// <summary>A shift along the spine's tangent (u), for fine placement either side of <see cref="Leg"/>.</summary>
        public float Along;
        /// <summary>The placement's bounding radius (u): the spawner's whole output is scaled uniformly to it.</summary>
        public float Scale = 60f;
        /// <summary>Euler angles (degrees) applied in the spine frame (+z along the line, +y toward the bearing).</summary>
        public float Yaw, Pitch, Roll;
        /// <summary>"Native" keeps the spawner's own colours; Jade / Ruby / Gold / Blue paints it all one colour (a team
        /// colour is a rail that team's Urchin can ride).</summary>
        public string Domain = "Native";
        /// <summary>"Native" keeps the spawner's own prism kinds; Plain / Danger / Shielded / SuperShielded lays all one.</summary>
        public string Prism = "Native";
        /// <summary>The spawner's own seed (0 = its default).</summary>
        public int Seed;
        /// <summary>A world spawner's density dial (how many of everything); ignored by the others.</summary>
        public float Density = 1f;
        /// <summary>May this placement's prisms stand in the race line's tube? Off (the default), the carve keeps the
        /// <see cref="RegattaCourseGenome.LineClearance"/> tube open and a pilot flying the line never meets it. On, the
        /// placement is an obstacle ON the line: only the ring mouths, the rails and the shell stay carved, so it is
        /// what makes a pilot (and an AI) steer, break through or bounce.</summary>
        public bool CrossLine;
        /// <summary>May this placement's prisms stand on or across the rails? Off (the default), every rail prism keeps
        /// <see cref="RegattaPlacementCarve.RailRadius"/> clear and a pilot riding a rail (the Urchin) never meets the
        /// placement. On, it can block a rail, so a rider must leave the rail or go round. Ring mouths stay carved.</summary>
        public bool CrossRail;
        /// <summary>The spawner's own fields, by serialized name (catalogue <c>params</c>).</summary>
        public SortedDictionary<string, double> Params = new(StringComparer.Ordinal);

        public RegattaPlacement Clone()
        {
            var c = (RegattaPlacement)MemberwiseClone();
            c.Params = new SortedDictionary<string, double>(Params ?? new SortedDictionary<string, double>(), StringComparer.Ordinal);
            return c;
        }
    }

    /// <summary>
    /// A Regatta course as ONE document a search can mutate (Garrett, 2026-11-16: "our genes could bring in every
    /// fundamental with all their types and parameters fully expressable ... really mutate the heck out the course"):
    /// the circuit (any <see cref="RegattaCourse.ForIntensity"/> setting, overridden), the rails' braid, the
    /// <see cref="RegattaCourseFeatures"/> (rail kinds, colours, gaps, detours, alternate rings, line crystals),
    /// placements of any segment spawner in the <see cref="RegattaGeneCatalogue"/>, and the course's lifeform ground.
    ///
    /// <para><b>The empty genome is today's course, prism for prism</b>: no placement, no override, the default seed,
    /// so every reader below returns what the shipped prefab returns (the harness holds it).</para>
    ///
    /// <para><b>One builder.</b> <see cref="SpawnableRegattaRails"/> reads <see cref="Override"/> wherever it read its
    /// own seed, intensity, braid, features and lifeforms, and lays the placements in the same arena build
    /// (<see cref="RegattaPlacementBuilder"/>), so Unity, Amoebius (the race judge sets <see cref="Override"/> before
    /// launch) and a pool entry all lay a genome through one code path. The pure parts here (parsing, the circuit,
    /// the placement frame, <see cref="Repair"/>) are mirrored line for line by
    /// <c>Tools/Compute/evaluators/regatta_genome.py</c> and held to it by the course harness.</para>
    ///
    /// <para>JSON (the flywheel's shared contract): <c>{"version", "intensity", "seed", "circuit": {HeadlongCircuitSettings
    /// field: value, "CornerProfile": [8]}, "rails": {"LaneOffset", "TwistTurnsPerLap", "PrismSpacing"}, "features":
    /// {RegattaCourseFeatures, PascalCase}, "line_clearance", "placements": [{"kind", "leg", "offset": [radius,
    /// bearing_deg, along_u], "scale", "yaw", "pitch", "roll", "domain", "prism", "seed", "density", "cross_line", "cross_rail", "params": {}}],
    /// "flora": {"preset", RegattaLifeformSiteSettings fields}}</c>. "crystals": {"PerLap", "Phase"} at the top level
    /// is read into <c>features.Crystals</c>.</para>
    /// </summary>
    [Serializable]
    public sealed class RegattaCourseGenome
    {
        public const int CurrentVersion = 1;

        /// <summary>Most placements one course carries.</summary>
        public const int MaxPlacements = 24;
        /// <summary>Most prisms one placement lays; a spawner that lays more is thinned by an even stride.</summary>
        public const int PerPlacementCap = 3000;
        /// <summary>Most prisms all placements lay together (the rails are 1,812 more). Garrett, 2026-11-16: a course
        /// may need more than the cell was measured for, so the cell's phase ladder is raised by what the placements lay
        /// (<see cref="ICellPhaseBaseline"/>), but a crowded course is also a flag: players lay trails on top and fauna
        /// must still do their job. 6,000 keeps a course within about three rail sets, and the prisms the measured
        /// ladder leaves for trails (Frenzy at 5,412 against 1,812 rails) stay theirs.</summary>
        public const int TotalPrismBudget = 6000;
        /// <summary>No placement prism nearer the cell centre than this (the nucleus, 391.9 u, with 10%).</summary>
        public const float NucleusCarve = RegattaCourse.NucleusWorldRadius * 1.1f;
        /// <summary>No placement prism farther from the cell centre than this (inside the 1,200 u membrane).</summary>
        public const float MembraneCarve = 1150f;
        public const float LineClearanceMin = 12f, LineClearanceMax = 80f, DefaultLineClearance = 32f;

        /// <summary>Placement colours; index 0 keeps the spawner's own.</summary>
        public static readonly string[] PlacementDomains = { "Native", "Jade", "Ruby", "Gold", "Blue" };
        /// <summary>Placement prism kinds; index 0 keeps the spawner's own.</summary>
        public static readonly string[] PlacementPrisms = { "Native", "Plain", "Danger", "Shielded", "SuperShielded" };

        /// <summary>The circuit settings a genome may override, with their legal ranges (min, max).</summary>
        public static readonly (string name, float lo, float hi)[] CircuitRanges =
        {
            ("BaseRadius", 700f, 900f),
            ("RingRadius", 50f, 140f),
            ("CornerFloorRadius", 100f, 260f),
            ("RadialSwing", 0f, 0.6f),
            ("AngularSpread", 0.5f, 5.5f),
            ("LateralPerturbation", 0f, 320f),
            ("AxisJitterDegrees", 0f, 50f),
            ("MaxPresentDegrees", 30f, 85f),
        };

        /// <summary>A race judge's, benchmark's or test's genome for every Regatta arena, or null for each prefab's own
        /// course. Read where the arena lays its course and where the controller hangs its rings; nothing in a match
        /// sets it.</summary>
        public static RegattaCourseGenome Override;

        public int Version = CurrentVersion;
        public int Intensity = 1;
        /// <summary>0 = <see cref="RegattaCourse.DefaultSeed"/>. The per-intensity seed is derived from it.</summary>
        public int Seed;
        /// <summary>Circuit overrides by <see cref="HeadlongCircuitSettings"/> field name; empty = the intensity's row.</summary>
        public SortedDictionary<string, double> Circuit = new(StringComparer.Ordinal);
        /// <summary>Null = the intensity's row. Eight turn angles, rescaled to sum to 360 by <see cref="Repair"/>.</summary>
        public float[] CornerProfile;
        /// <summary>NaN = the shipped braid's value.</summary>
        public float LaneOffset = float.NaN, TwistTurnsPerLap = float.NaN, PrismSpacing = float.NaN;
        public RegattaCourseFeatures Features = new();
        /// <summary>Radius (u) of the tube round the racing line no placement prism may enter.</summary>
        public float LineClearance = DefaultLineClearance;
        public List<RegattaPlacement> Placements = new();
        /// <summary>The lifeform preset (Tools/Build/regatta_lifeforms.py PRESETS) a pool entry built from this genome
        /// authors its cell's spawn profile from; "none" = barren. The sites below are laid by the arena itself.</summary>
        public string FloraPreset = "none";
        public RegattaLifeformSiteSettings Flora = new();

        // ── Reading the genome as the arena reads a prefab ─────────────────────────────────────────────────────

        public int EffectiveSeed => Seed != 0 ? Seed : RegattaCourse.DefaultSeed;

        public bool IsEmpty =>
            Intensity == 1 && Seed == 0 && (Circuit == null || Circuit.Count == 0) && CornerProfile == null
            && float.IsNaN(LaneOffset) && float.IsNaN(TwistTurnsPerLap) && float.IsNaN(PrismSpacing)
            && (Features == null || Features.IsEmpty) && (Placements == null || Placements.Count == 0)
            && (Flora == null || (!Flora.HasSites && Flora.faunaCorridorRadius <= 0f));

        public HeadlongCircuitSettings CircuitSettings()
        {
            var s = RegattaCourse.ForIntensity(Intensity);
            if (CornerProfile is { Length: RegattaCourse.RingsPerLap }) s.CornerProfile = (float[])CornerProfile.Clone();
            if (Circuit != null)
                foreach (var kv in Circuit)
                {
                    float v = (float)kv.Value;
                    switch (kv.Key)
                    {
                        case "BaseRadius": s.BaseRadius = v; break;
                        case "RingRadius": s.RingRadius = v; break;
                        case "CornerFloorRadius": s.CornerFloorRadius = v; break;
                        case "RadialSwing": s.RadialSwing = v; break;
                        case "AngularSpread": s.AngularSpread = v; break;
                        case "LateralPerturbation": s.LateralPerturbation = v; break;
                        case "AxisJitterDegrees": s.AxisJitterDegrees = v; break;
                        case "MaxPresentDegrees": s.MaxPresentDegrees = v; break;
                    }
                }
            return s;
        }

        public List<RaceGate> BuildGates() =>
            HeadlongCircuit.Generate(RegattaCourse.SeedForIntensity(EffectiveSeed, Intensity), CircuitSettings());

        public RegattaRailSettings RailSettings(RegattaRailSettings authored) => new()
        {
            LaneOffset = float.IsNaN(LaneOffset) ? authored.LaneOffset : LaneOffset,
            TwistTurnsPerLap = float.IsNaN(TwistTurnsPerLap) ? authored.TwistTurnsPerLap : TwistTurnsPerLap,
            PrismSpacing = float.IsNaN(PrismSpacing) ? authored.PrismSpacing : PrismSpacing,
            SamplesPerLeg = authored.SamplesPerLeg,
        };

        // ── The placement frame (pure; regatta_genome.py placement_pose) ───────────────────────────────────────

        /// <summary>The spine point, its unit tangent and the frame's "out" axis at <paramref name="leg"/> (leg +
        /// Hermite parameter, wrapped round the lap): the same closed Hermite <see cref="RegattaCourse.BuildRails"/>
        /// runs through the rings. Out is the spine point's direction from the cell centre with the tangent taken out.</summary>
        public static void SpineFrame(IReadOnlyList<RaceGate> gates, float leg, out Vector3 point, out Vector3 tangent,
                                      out Vector3 outward)
        {
            int n = gates.Count;
            float wrapped = leg - Mathf.Floor(leg / n) * n;
            int i = Mathf.Min(n - 1, (int)wrapped);
            float t = wrapped - i;
            var a = gates[i];
            var b = gates[(i + 1) % n];
            float chord = (b.Position - a.Position).magnitude;
            Vector3 m0 = a.Axis.normalized * chord;
            Vector3 m1 = b.Axis.normalized * chord;
            point = RegattaCourse.Hermite(a.Position, m0, b.Position, m1, t);
            tangent = RaceCourseGeometry.SafeNormalize(RegattaCourse.HermiteDerivative(a.Position, m0, b.Position, m1, t), a.Axis);
            Vector3 o = point - tangent * Vector3.Dot(point, tangent);
            outward = RaceCourseGeometry.SafeNormalize(o, RaceCourseGeometry.Perpendicular(tangent));
        }

        /// <summary>The placement's centre and orientation, cell-local.</summary>
        public static void PlacementPose(IReadOnlyList<RaceGate> gates, RegattaPlacement p, out Vector3 centre,
                                         out Quaternion rotation)
        {
            SpineFrame(gates, p.Leg, out var point, out var tangent, out var outward);
            Vector3 side = Vector3.Cross(tangent, outward);
            float b = p.Bearing * Mathf.Deg2Rad;
            Vector3 dir = outward * Mathf.Cos(b) + side * Mathf.Sin(b);
            centre = point + dir * p.Radius + tangent * p.Along;
            rotation = Quaternion.LookRotation(tangent, dir) * Quaternion.Euler(p.Pitch, p.Yaw, p.Roll);
        }

        // ── Repair (pure; regatta_genome.py repair) ────────────────────────────────────────────────────────────

        static float Wrap(float v, float period) => v - Mathf.Floor(v / period) * period;

        /// <summary>
        /// A copy of this genome that is LEGAL, whatever a mutation did to it: every number clamped into its range,
        /// unknown kinds and parameters dropped, the turn angles rescaled to a closed lap, the braid kept inside the
        /// tightest mouth, every placement's centre inside the membrane, and the placements' estimated prisms kept
        /// under <see cref="TotalPrismBudget"/>. Repair FIXES rather than rejects where it can: a search keeps almost
        /// every mutation it makes. What it changed is listed in <paramref name="notes"/>.
        ///
        /// <para>Where a placement's prisms may go is not a repair rule but a per-prism one: the arena carves them out
        /// of the <see cref="LineClearance"/> tube, the ring mouths, the rails, the nucleus, the membrane and every
        /// earlier placement as it lays them (<see cref="RegattaPlacementBuilder"/>, <see cref="RegattaPlacementCarve"/>),
        /// so no genome can seal the line, and a placement as large as the cell is a world the course runs through.</para>
        /// </summary>
        public RegattaCourseGenome Repair(RegattaGeneCatalogue catalogue, List<string> notes)
        {
            var g = new RegattaCourseGenome
            {
                Version = CurrentVersion,
                Intensity = Mathf.Clamp(Intensity, 1, 4),
                Seed = Seed,
                FloraPreset = string.IsNullOrEmpty(FloraPreset) ? "none" : FloraPreset,
            };

            // Circuit.
            if (Circuit != null)
                foreach (var kv in Circuit)
                {
                    int r = Array.FindIndex(CircuitRanges, c => c.name == kv.Key);
                    if (r < 0) { notes?.Add($"circuit: dropped unknown setting {kv.Key}"); continue; }
                    g.Circuit[kv.Key] = Mathf.Clamp((float)kv.Value, CircuitRanges[r].lo, CircuitRanges[r].hi);
                }
            if (CornerProfile != null)
            {
                var cp = new float[RegattaCourse.RingsPerLap];
                float sum = 0f;
                for (int i = 0; i < cp.Length; i++)
                {
                    cp[i] = i < CornerProfile.Length ? Mathf.Clamp(CornerProfile[i], 0f, 160f) : 0f;
                    sum += cp[i];
                }
                if (sum > 1f)
                {
                    for (int i = 0; i < cp.Length; i++) cp[i] = cp[i] * 360f / sum;
                    g.CornerProfile = cp;
                }
                else notes?.Add("circuit: corner profile had no turning; the intensity's row is used");
            }
            float ringRadius = g.CircuitSettings().RingRadius;

            // Braid: a lane inside the tightest mouth with a prism and a margin to spare (RegattaCourse.DefaultRails).
            if (!float.IsNaN(LaneOffset))
                g.LaneOffset = Mathf.Clamp(LaneOffset, 14f, Mathf.Max(14f, Mathf.Min(40f, ringRadius - 28f)));
            else if (RegattaCourse.DefaultRails.LaneOffset > ringRadius - 28f)
            {
                g.LaneOffset = Mathf.Max(14f, ringRadius - 28f);
                notes?.Add("rails: lane offset pulled in to fit the mouth");
            }
            if (!float.IsNaN(TwistTurnsPerLap)) g.TwistTurnsPerLap = Mathf.Clamp(Mathf.Round(TwistTurnsPerLap), 0f, 3f);
            if (!float.IsNaN(PrismSpacing)) g.PrismSpacing = Mathf.Clamp(PrismSpacing, 6f, 16f);

            g.Features = RepairFeatures(Features, notes);
            g.LineClearance = Mathf.Clamp(float.IsNaN(LineClearance) ? DefaultLineClearance : LineClearance,
                                          LineClearanceMin, LineClearanceMax);
            g.Flora = RepairFlora(Flora, float.IsNaN(g.LaneOffset) ? RegattaCourse.DefaultRails.LaneOffset : g.LaneOffset);

            // Placements.
            var gates = g.BuildGates();
            int estimated = 0;
            if (Placements != null)
                for (int idx = 0; idx < Placements.Count; idx++)
                {
                    var src = Placements[idx];
                    if (src == null) continue;
                    if (g.Placements.Count >= MaxPlacements) { notes?.Add($"placement {idx}: over the {MaxPlacements} cap, dropped"); continue; }
                    if (catalogue == null || !catalogue.TryGet(src.Kind, out var kind) || !kind.Usable)
                    {
                        notes?.Add($"placement {idx}: kind '{src.Kind}' is not placeable, dropped");
                        continue;
                    }
                    var p = src.Clone();
                    p.Leg = Wrap(p.Leg, RegattaCourse.RingsPerLap);
                    p.Radius = Mathf.Clamp(p.Radius, 0f, 400f);
                    p.Bearing = Wrap(p.Bearing, 360f);
                    p.Along = Mathf.Clamp(p.Along, -150f, 150f);
                    p.Yaw = Wrap(p.Yaw, 360f);
                    p.Pitch = Wrap(p.Pitch, 360f);
                    p.Roll = Wrap(p.Roll, 360f);
                    if (Array.IndexOf(PlacementDomains, p.Domain) < 0) p.Domain = PlacementDomains[0];
                    if (Array.IndexOf(PlacementPrisms, p.Prism) < 0) p.Prism = PlacementPrisms[0];
                    p.Density = kind.World ? Mathf.Clamp(p.Density, 0.1f, 2f) : 1f;
                    var clean = new SortedDictionary<string, double>(StringComparer.Ordinal);
                    foreach (var kv in p.Params)
                    {
                        var prm = kind.FindParam(kv.Key);
                        if (prm == null) { notes?.Add($"placement {idx}: dropped unknown parameter {kv.Key}"); continue; }
                        clean[kv.Key] = prm.Clamp(kv.Value);
                    }
                    p.Params = clean;

                    float scale = Mathf.Clamp(p.Scale, (float)kind.ScaleLo, (float)kind.ScaleHi);
                    PlacementPose(gates, p, out var c, out _);
                    if (c.magnitude > MembraneCarve) { notes?.Add($"placement {idx}: centre outside the membrane, dropped"); continue; }
                    p.Scale = scale;
                    int est = EstimatedPrisms(kind, p);
                    if (estimated + est > TotalPrismBudget) { notes?.Add($"placement {idx}: over the prism budget, dropped"); continue; }
                    estimated += est;
                    g.Placements.Add(p);
                }
            return g;
        }

        /// <summary>The prisms a placement is expected to lay: its kind's measured count, scaled by density, capped.</summary>
        public static int EstimatedPrisms(RegattaGeneCatalogue.Kind kind, RegattaPlacement p) =>
            Mathf.Min(PerPlacementCap, Mathf.Max(1, Mathf.RoundToInt(kind.NativePrisms * (kind.World ? p.Density : 1f))));

        static RegattaCourseFeatures RepairFeatures(RegattaCourseFeatures f, List<string> notes)
        {
            var o = new RegattaCourseFeatures();
            if (f == null) return o;
            if (f.Sections != null)
                foreach (var s0 in f.Sections)
                {
                    if (o.Sections.Count >= 24) { notes?.Add("features: over 24 sections, the rest dropped"); break; }
                    var s = s0;
                    s.Leg = Mathf.Clamp(s.Leg, -1, RegattaCourse.RingsPerLap - 1);
                    float a = Mathf.Clamp01(s.From), b = Mathf.Clamp01(s.To);
                    s.From = Mathf.Min(a, b);
                    s.To = Mathf.Max(a, b);
                    if (s.To - s.From < 0.01f) continue;
                    s.Kind = (RegattaRailKind)Mathf.Clamp((int)s.Kind, 0, 3);
                    s.Detour = Mathf.Clamp(s.Detour, 0f, 200f);
                    s.DetourBearing = Wrap(s.DetourBearing, 360f);
                    o.Sections.Add(s);
                }
            if (f.AlternateGates != null)
                foreach (var a0 in f.AlternateGates)
                {
                    var a = a0;
                    a.Ring = ((a.Ring % RegattaCourse.RingsPerLap) + RegattaCourse.RingsPerLap) % RegattaCourse.RingsPerLap;
                    if (o.AlternateGates.Exists(x => x.Ring == a.Ring)) continue;
                    a.Offset = Mathf.Clamp(a.Offset, 0f, 420f);
                    if (a.Offset <= 0f) continue;
                    a.Bearing = Wrap(a.Bearing, 360f);
                    a.RadiusScale = Mathf.Clamp(a.RadiusScale, 0.6f, 1.4f);
                    o.AlternateGates.Add(a);
                }
            o.Crystals = new RegattaLineCrystals
            {
                PerLap = Mathf.Clamp(f.Crystals.PerLap, 0, RegattaCourseFeatures.MaxLineCrystalsPerLap),
                Phase = Wrap(f.Crystals.Phase, 1f),
            };
            return o;
        }

        static RegattaLifeformSiteSettings RepairFlora(RegattaLifeformSiteSettings f, float laneOffset)
        {
            var o = new RegattaLifeformSiteSettings();
            if (f == null) return o;
            var sites = new int[f.sitesPerLeg == null ? 0 : Mathf.Min(RegattaCourse.RingsPerLap, f.sitesPerLeg.Length)];
            bool any = false;
            for (int i = 0; i < sites.Length; i++) { sites[i] = Mathf.Clamp(f.sitesPerLeg[i], 0, 12); any |= sites[i] > 0; }
            o.sitesPerLeg = any ? sites : Array.Empty<int>();
            o.offsetMin = Mathf.Clamp(Mathf.Max(f.offsetMin, laneOffset + RegattaLifeformSites.MinRailClearance), 0f, 300f);
            o.offsetMax = Mathf.Clamp(Mathf.Max(f.offsetMax, o.offsetMin), o.offsetMin, 320f);
            o.sideDegrees = Wrap(f.sideDegrees, 360f);
            o.sideSpreadDegrees = Mathf.Clamp(f.sideSpreadDegrees, 0f, 180f);
            o.legWindowStart = Mathf.Clamp01(Mathf.Min(f.legWindowStart, f.legWindowEnd));
            o.legWindowEnd = Mathf.Clamp01(Mathf.Max(f.legWindowStart, f.legWindowEnd));
            o.ringClearance = Mathf.Clamp(f.ringClearance, 0f, 200f);
            o.faunaCorridorRadius = Mathf.Clamp(f.faunaCorridorRadius, 0f, 200f);
            o.faunaCorridorStandoff = Mathf.Clamp(f.faunaCorridorStandoff, 0f, 200f);
            return o;
        }

        // ── JSON ────────────────────────────────────────────────────────────────────────────────────────────────

        public static RegattaCourseGenome FromJson(string json)
        {
            var root = RegattaJson.Obj(RegattaJson.Parse(json)) ?? throw new FormatException("a course genome is a JSON object");
            var g = new RegattaCourseGenome
            {
                Version = (int)RegattaJson.Num(root, "version", CurrentVersion),
                Intensity = (int)RegattaJson.Num(root, "intensity", 1),
                Seed = (int)RegattaJson.Num(root, "seed", 0),
                LineClearance = (float)RegattaJson.Num(root, "line_clearance", DefaultLineClearance),
            };
            var circuit = RegattaJson.Obj(RegattaJson.Get(root, "circuit"));
            if (circuit != null)
                foreach (var kv in circuit)
                {
                    if (kv.Key == "CornerProfile")
                    {
                        var arr = RegattaJson.Arr(kv.Value);
                        if (arr == null) continue;
                        g.CornerProfile = new float[arr.Count];
                        for (int i = 0; i < arr.Count; i++) g.CornerProfile[i] = (float)RegattaJson.Num(arr[i], 0);
                    }
                    else if (kv.Value is double) g.Circuit[kv.Key] = (double)kv.Value;
                }
            var rails = RegattaJson.Obj(RegattaJson.Get(root, "rails"));
            g.LaneOffset = (float)RegattaJson.Num(rails, "LaneOffset", double.NaN);
            g.TwistTurnsPerLap = (float)RegattaJson.Num(rails, "TwistTurnsPerLap", double.NaN);
            g.PrismSpacing = (float)RegattaJson.Num(rails, "PrismSpacing", double.NaN);

            g.Features = FeaturesFromJson(RegattaJson.Obj(RegattaJson.Get(root, "features")));
            var crystals = RegattaJson.Obj(RegattaJson.Get(root, "crystals"));
            if (crystals != null)
                g.Features.Crystals = new RegattaLineCrystals
                {
                    PerLap = (int)RegattaJson.Num(crystals, "PerLap", 0),
                    Phase = (float)RegattaJson.Num(crystals, "Phase", 0),
                };

            foreach (var po in RegattaJson.Arr(RegattaJson.Get(root, "placements")) ?? new List<object>())
            {
                var pd = RegattaJson.Obj(po);
                if (pd == null) continue;
                var off = RegattaJson.Arr(RegattaJson.Get(pd, "offset"));
                var p = new RegattaPlacement
                {
                    Kind = RegattaJson.Str(pd, "kind", null),
                    Leg = (float)RegattaJson.Num(pd, "leg", 0),
                    Radius = off is { Count: > 0 } ? (float)RegattaJson.Num(off[0], 0) : 0f,
                    Bearing = off is { Count: > 1 } ? (float)RegattaJson.Num(off[1], 0) : 0f,
                    Along = off is { Count: > 2 } ? (float)RegattaJson.Num(off[2], 0) : 0f,
                    Scale = (float)RegattaJson.Num(pd, "scale", 60),
                    Yaw = (float)RegattaJson.Num(pd, "yaw", 0),
                    Pitch = (float)RegattaJson.Num(pd, "pitch", 0),
                    Roll = (float)RegattaJson.Num(pd, "roll", 0),
                    Domain = RegattaJson.Str(pd, "domain", "Native"),
                    Prism = RegattaJson.Str(pd, "prism", "Native"),
                    Seed = (int)RegattaJson.Num(pd, "seed", 0),
                    Density = (float)RegattaJson.Num(pd, "density", 1),
                    CrossLine = RegattaJson.Bool(pd, "cross_line", false),
                    CrossRail = RegattaJson.Bool(pd, "cross_rail", false),
                };
                var prm = RegattaJson.Obj(RegattaJson.Get(pd, "params"));
                if (prm != null)
                    foreach (var kv in prm)
                        p.Params[kv.Key] = RegattaJson.Num(kv.Value, 0);
                g.Placements.Add(p);
            }

            var flora = RegattaJson.Obj(RegattaJson.Get(root, "flora"));
            if (flora != null)
            {
                g.FloraPreset = RegattaJson.Str(flora, "preset", "none");
                var f = g.Flora;
                var sites = RegattaJson.Arr(RegattaJson.Get(flora, "sitesPerLeg"));
                if (sites != null)
                {
                    f.sitesPerLeg = new int[sites.Count];
                    for (int i = 0; i < sites.Count; i++) f.sitesPerLeg[i] = (int)RegattaJson.Num(sites[i], 0);
                }
                f.offsetMin = (float)RegattaJson.Num(flora, "offsetMin", f.offsetMin);
                f.offsetMax = (float)RegattaJson.Num(flora, "offsetMax", f.offsetMax);
                f.sideDegrees = (float)RegattaJson.Num(flora, "sideDegrees", f.sideDegrees);
                f.sideSpreadDegrees = (float)RegattaJson.Num(flora, "sideSpreadDegrees", f.sideSpreadDegrees);
                f.legWindowStart = (float)RegattaJson.Num(flora, "legWindowStart", f.legWindowStart);
                f.legWindowEnd = (float)RegattaJson.Num(flora, "legWindowEnd", f.legWindowEnd);
                f.ringClearance = (float)RegattaJson.Num(flora, "ringClearance", f.ringClearance);
                f.faunaCorridorRadius = (float)RegattaJson.Num(flora, "faunaCorridorRadius", f.faunaCorridorRadius);
                f.faunaCorridorStandoff = (float)RegattaJson.Num(flora, "faunaCorridorStandoff", f.faunaCorridorStandoff);
            }
            return g;
        }

        public static RegattaCourseFeatures FeaturesFromJson(Dictionary<string, object> d)
        {
            var f = new RegattaCourseFeatures();
            if (d == null) return f;
            foreach (var so in RegattaJson.Arr(RegattaJson.Get(d, "Sections")) ?? new List<object>())
            {
                var s = RegattaJson.Obj(so);
                if (s == null) continue;
                f.Sections.Add(new RegattaRailSection(
                    (int)RegattaJson.Num(s, "Leg", -1), (float)RegattaJson.Num(s, "From", 0), (float)RegattaJson.Num(s, "To", 0),
                    (RegattaRailKind)(int)RegattaJson.Num(s, "Kind", 0), RegattaJson.Bool(s, "Neutral", false),
                    (float)RegattaJson.Num(s, "Detour", 0), (float)RegattaJson.Num(s, "DetourBearing", 0)));
            }
            foreach (var ao in RegattaJson.Arr(RegattaJson.Get(d, "AlternateGates")) ?? new List<object>())
            {
                var a = RegattaJson.Obj(ao);
                if (a == null) continue;
                f.AlternateGates.Add(new RegattaAlternateGate((int)RegattaJson.Num(a, "Ring", 0),
                    (float)RegattaJson.Num(a, "Offset", 0), (float)RegattaJson.Num(a, "Bearing", 0),
                    (float)RegattaJson.Num(a, "RadiusScale", 1)));
            }
            var c = RegattaJson.Obj(RegattaJson.Get(d, "Crystals"));
            if (c != null)
                f.Crystals = new RegattaLineCrystals
                {
                    PerLap = (int)RegattaJson.Num(c, "PerLap", 0),
                    Phase = (float)RegattaJson.Num(c, "Phase", 0),
                };
            return f;
        }

        /// <summary>The genome as canonical JSON: a fixed key order, defaults left out, float32 round-trip numbers.</summary>
        public string ToJson()
        {
            var root = new OrderedDictionary
            {
                ["version"] = Version,
                ["intensity"] = Intensity,
                ["seed"] = Seed,
            };
            if ((Circuit != null && Circuit.Count > 0) || CornerProfile != null)
            {
                var c = new OrderedDictionary();
                if (CornerProfile != null) c["CornerProfile"] = Array.ConvertAll(CornerProfile, x => (double)x);
                if (Circuit != null) foreach (var kv in Circuit) c[kv.Key] = kv.Value;
                root["circuit"] = c;
            }
            if (!float.IsNaN(LaneOffset) || !float.IsNaN(TwistTurnsPerLap) || !float.IsNaN(PrismSpacing))
            {
                var r = new OrderedDictionary();
                if (!float.IsNaN(LaneOffset)) r["LaneOffset"] = (double)LaneOffset;
                if (!float.IsNaN(TwistTurnsPerLap)) r["TwistTurnsPerLap"] = (double)TwistTurnsPerLap;
                if (!float.IsNaN(PrismSpacing)) r["PrismSpacing"] = (double)PrismSpacing;
                root["rails"] = r;
            }
            var feats = new OrderedDictionary();
            var sections = new List<object>();
            if (Features?.Sections != null)
                foreach (var s in Features.Sections)
                    sections.Add(new OrderedDictionary
                    {
                        ["Leg"] = s.Leg, ["From"] = (double)s.From, ["To"] = (double)s.To, ["Kind"] = (int)s.Kind,
                        ["Neutral"] = s.Neutral, ["Detour"] = (double)s.Detour, ["DetourBearing"] = (double)s.DetourBearing,
                    });
            var alts = new List<object>();
            if (Features?.AlternateGates != null)
                foreach (var a in Features.AlternateGates)
                    alts.Add(new OrderedDictionary
                    {
                        ["Ring"] = a.Ring, ["Offset"] = (double)a.Offset, ["Bearing"] = (double)a.Bearing,
                        ["RadiusScale"] = (double)a.RadiusScale,
                    });
            feats["Sections"] = sections;
            feats["AlternateGates"] = alts;
            feats["Crystals"] = new OrderedDictionary
            {
                ["PerLap"] = Features?.Crystals.PerLap ?? 0, ["Phase"] = (double)(Features?.Crystals.Phase ?? 0f),
            };
            root["features"] = feats;
            root["line_clearance"] = (double)LineClearance;
            var placements = new List<object>();
            if (Placements != null)
                foreach (var p in Placements)
                {
                    var prm = new OrderedDictionary();
                    foreach (var kv in p.Params) prm[kv.Key] = kv.Value;
                    placements.Add(new OrderedDictionary
                    {
                        ["kind"] = p.Kind, ["leg"] = (double)p.Leg,
                        ["offset"] = new List<object> { (double)p.Radius, (double)p.Bearing, (double)p.Along },
                        ["scale"] = (double)p.Scale, ["yaw"] = (double)p.Yaw, ["pitch"] = (double)p.Pitch,
                        ["roll"] = (double)p.Roll, ["domain"] = p.Domain, ["prism"] = p.Prism, ["seed"] = p.Seed,
                        ["density"] = (double)p.Density, ["cross_line"] = p.CrossLine,
                        ["cross_rail"] = p.CrossRail, ["params"] = prm,
                    });
                }
            root["placements"] = placements;
            var f = Flora ?? new RegattaLifeformSiteSettings();
            root["flora"] = new OrderedDictionary
            {
                ["preset"] = FloraPreset ?? "none",
                ["sitesPerLeg"] = new List<object>(Array.ConvertAll(f.sitesPerLeg ?? Array.Empty<int>(), x => (object)x)),
                ["offsetMin"] = (double)f.offsetMin, ["offsetMax"] = (double)f.offsetMax,
                ["sideDegrees"] = (double)f.sideDegrees, ["sideSpreadDegrees"] = (double)f.sideSpreadDegrees,
                ["legWindowStart"] = (double)f.legWindowStart, ["legWindowEnd"] = (double)f.legWindowEnd,
                ["ringClearance"] = (double)f.ringClearance, ["faunaCorridorRadius"] = (double)f.faunaCorridorRadius,
                ["faunaCorridorStandoff"] = (double)f.faunaCorridorStandoff,
            };
            return RegattaJson.Write(root);
        }

        /// <summary>A stable hash of every setting that changes what the arena lays, for its build cache.</summary>
        public int ParameterHash()
        {
            // FNV-1a over the canonical JSON: string.GetHashCode is randomised per process on .NET.
            unchecked
            {
                uint h = 2166136261u;
                foreach (char ch in ToJson()) h = (h ^ ch) * 16777619u;
                return (int)h;
            }
        }
    }
}
