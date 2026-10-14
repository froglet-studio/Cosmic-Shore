using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>What a Charge crystal's sites are made of: the omni's 12 pentagonal prisms (one plate a site), or the
    /// Charge crystal's 12 blooms of five petals (60 plates).</summary>
    public enum ChargeSiteKind
    {
        Pentagons = 1,
        Blooms = 2,
    }

    /// <summary>
    /// The timing and scale of the Charge light (Omni Motion Lab rounds 4-5, <c>Docs/Studios/OMNI_MOTION_LAB.md</c>), as
    /// plain numbers so the pure core and its harness can run without an asset. <see cref="Lab"/> is the lab's own pick;
    /// <c>CrystalChargeArcsProfileSO</c> carries the shipped values.
    /// </summary>
    public struct ChargeArcSettings
    {
        /// <summary>x the speed of every beat.</summary>
        public float Rate;
        /// <summary>x the bolts' brightness.</summary>
        public float Bolts;
        /// <summary>x the lit plates' brightness.</summary>
        public float Glow;

        /// <summary>Seam walk: seconds a seam takes to strike, the hold before the next (min..max) and the fade behind it.</summary>
        public float SeamStrike, SeamDwellMin, SeamDwellMax, SeamFade;

        /// <summary>Flux arc: seconds between discharges, the source's charge, the strike, the hold and the fade; the
        /// fraction of the arc at each end over which the five strands fork onto the corners; and the band of angles a
        /// sink is drawn from.</summary>
        public float FluxGap, FluxCharge, FluxStrike, FluxHold, FluxFade, FluxFork, FluxMinDegrees, FluxMaxDegrees;

        /// <summary>Flux arc: how far the five strands run off the arc's centre line between the forks (x the crystal's
        /// radius), and how many whole turns the bundle makes about it.</summary>
        public float FluxBundle;
        public int FluxTurns;

        /// <summary>A bolt's core width (x the crystal's radius); how fast its plasma writhes (the smooth noise that bends
        /// it scrolls this many noise cells a second along the bolt); how fast its brightness breathes.</summary>
        public float CoreWidth, WritheSpeed, FlickerSpeed;

        /// <summary>Seam walk: x the light on the petals beside the bolt (on top of <see cref="Glow"/>). Low, so the plasma
        /// reads over the petals it lights (designer, 2026-10-13: "tone down how much it lights up the charge petals so we
        /// can see the plasma, more prominently").</summary>
        public float SeamPetalGlow;

        /// <summary>Flux arc: a Mass column lights where an arc passes through it only while it stands out of the omni
        /// (designer, 2026-10-13: "they should only light when protracted (not retracted)"). Its strike fades in as its
        /// protraction (1 - its column place) rises from the first to the second.</summary>
        public float StrikeProtractionFrom, StrikeProtractionFull;

        public static ChargeArcSettings Lab => new()
        {
            Rate = 1f, Bolts = 1f, Glow = 1f,
            SeamStrike = 0.07f, SeamDwellMin = 0.1f, SeamDwellMax = 0.28f, SeamFade = 0.85f,
            FluxGap = 0.9f, FluxCharge = 0.45f, FluxStrike = 0.06f, FluxHold = 0.45f, FluxFade = 0.45f, FluxFork = 0.3f,
            FluxMinDegrees = 55f, FluxMaxDegrees = 125f, FluxBundle = 0.016f, FluxTurns = 1,
            CoreWidth = 0.012f, WritheSpeed = 3f, FlickerSpeed = 4f,
            SeamPetalGlow = 0.35f, StrikeProtractionFrom = 0.15f, StrikeProtractionFull = 0.45f,
        };

        /// <summary>Seconds per lab-second: every duration is divided by the rate.</summary>
        public float Span => 1f / Mathf.Max(0.05f, Rate);
    }

    /// <summary>One plate's outer face, as the Charge light draws it: a fan pushed just off the plate.</summary>
    public sealed class ChargeGlowFace
    {
        public int Site;
        /// <summary>The face's corners in ring order, pushed off the plate, then its centre.</summary>
        public Vector3[] Ring;
        public Vector3 Centre;
        /// <summary>Each ring corner's distance from the face's own centre (0..1), then from its site's centre.</summary>
        public float[] OwnFalloff, SiteFalloff;
        public float SiteFalloffAtCentre;
        /// <summary>The midpoints of the edges a seam runs beside: a petal's three short sides, a pentagon's five.</summary>
        public Vector3[] Back;
        /// <summary>How near (x this, as a Gaussian) a bolt must run to a back edge to light the face.</summary>
        public float Sigma;
    }

    /// <summary>One seam between two blooms: from junction <see cref="A"/> to junction <see cref="B"/>, nine points in
    /// the gap between the two outlines.</summary>
    public sealed class ChargeSeam
    {
        public int A, B, SiteA, SiteB;
        public Vector3[] Points;
        /// <summary>The seam's length along its points.</summary>
        public float Length;
        public bool Touches(int site) => SiteA == site || SiteB == site;
    }

    /// <summary>
    /// The geometry of a crystal's Charge sites, read from its own model: the 12 sites and their axes, the lit faces, the
    /// SEAM NET between the blooms (the dodecahedral net: 20 junctions where three blooms meet, 30 seams where two do; a
    /// bloom's outline is its petals' SHORT sides only, so a seam never runs to a bloom's centre and never circles a petal)
    /// and each site's five arc ends. Built once per (mesh, thickness) and shared.
    ///
    /// JS twin: <c>seamNet()</c>, <c>fluxCorners()</c> and <c>fluxGeo()</c> in <c>Docs/Studios/OmniMotionLab.html</c>.
    /// </summary>
    public sealed class ChargeSites
    {
        public const int SiteCount = 12;
        const int PlateCorners = 10;   // a pentagonal prism, and a petal (a five-sided prism): 2 x 5 corners
        const float GlowLift = 0.006f; // the lit face stands this far (x the radius) off its plate, over the body's own push
        const float SeamSamples = 8f;
        const float SeamLift = 0.04f;  // the seams run this far (x the radius) above the blooms' outlines
        const float CornerLift = 0.012f; // a pentagon's arc ends stand this far (x the radius) off its face

        public ChargeSiteKind Kind { get; private set; }
        /// <summary>The crystal's radius: its farthest vertex from the centre.</summary>
        public float Radius { get; private set; }
        public Vector3[] Axes { get; private set; }
        public int[][] Neighbours { get; private set; }
        public ChargeGlowFace[] Faces { get; private set; }
        public int[][] FacesOfSite { get; private set; }
        /// <summary>Each site's five arc ends: a pentagon's corners (just off its face), or a bloom's five junctions.</summary>
        public Vector3[][] Corners { get; private set; }
        public Vector3[] Junctions { get; private set; }
        public int[][] JunctionSites { get; private set; }
        public ChargeSeam[] Seams { get; private set; }
        public int[][] SeamsOfJunction { get; private set; }

        readonly Dictionary<int, ChargeFluxPath> paths = new();

        /// <summary>
        /// Reads the sites off a crystal model: every plate with ten corners (welded by position; one component per
        /// plate), 12 of them for the omni's pentagons or 60 petals in 12 blooms for the Charge crystal. Pentagons are
        /// drawn <paramref name="pentagonThickness"/> times thicker about their own centre along their own normal, as
        /// <c>CrystalSpaceSquares</c> draws them. Refused, named, when the plates are not one of those two shapes.
        /// </summary>
        public static bool TryBuild(Vector3[] vertices, int[] triangles, float pentagonThickness, out ChargeSites sites, out string problem)
        {
            sites = null;
            if (vertices == null || vertices.Length == 0 || triangles == null || triangles.Length < 3)
            {
                problem = "the model has no triangles";
                return false;
            }
            float radius = 0f;
            foreach (var v in vertices) radius = Mathf.Max(radius, v.magnitude);

            var plates = TenCornerPlates(vertices, triangles, radius * 1e-5f);
            var kind = plates.Count == SiteCount ? ChargeSiteKind.Pentagons
                : plates.Count == SiteCount * 5 ? ChargeSiteKind.Blooms : (ChargeSiteKind)0;
            if (kind == 0)
            {
                problem = $"{plates.Count} ten-corner plates (expected {SiteCount} pentagonal prisms or {SiteCount * 5} petals)";
                return false;
            }

            var built = new ChargeSites { Kind = kind, Radius = radius };
            var frames = new List<Plate>(plates.Count);
            foreach (var corners in plates)
            {
                if (!Plate.TryRead(corners, kind == ChargeSiteKind.Pentagons ? pentagonThickness : 1f, out var plate, out problem))
                    return false;
                frames.Add(plate);
            }
            if (!built.TryGroup(frames, out var siteOf, out problem)) return false;
            built.BuildFaces(frames, siteOf);
            if (!built.TryBuildNet(frames, siteOf, out problem)) return false;
            built.BuildCorners(frames, siteOf);
            sites = built;
            problem = null;
            return true;
        }

        /// <summary>The arc from <paramref name="source"/> to <paramref name="sink"/> (built once, then shared).</summary>
        public ChargeFluxPath PathOf(int source, int sink)
        {
            int key = source * SiteCount + sink;
            if (!paths.TryGetValue(key, out var path)) paths[key] = path = new ChargeFluxPath(this, source, sink);
            return path;
        }

        // ── plates ──

        sealed class Plate
        {
            public Vector3 Centre, Normal, OuterCentre;
            public Vector3[] Outer;        // the outer face, in ring order
            public int Tip = -1;           // a petal's tip: the corner between its two long sides
            public float Bound;            // the farthest corner from the centre

            public static bool TryRead(List<Vector3> corners, float thickness, out Plate plate, out string problem)
            {
                plate = null;
                Vector3 centre = Vector3.zero;
                float lo = float.MaxValue, hi = float.MinValue;
                foreach (var p in corners) { centre += p; lo = Mathf.Min(lo, p.magnitude); hi = Mathf.Max(hi, p.magnitude); }
                centre /= corners.Count;
                float mid = (lo + hi) / 2f;
                var outer = new List<Vector3>(5);
                Vector3 outerSum = Vector3.zero, innerSum = Vector3.zero;
                int inner = 0;
                foreach (var p in corners)
                    if (p.magnitude > mid) { outer.Add(p); outerSum += p; }
                    else { innerSum += p; inner++; }
                if (outer.Count != 5 || inner != 5)
                {
                    problem = $"a ten-corner plate splits {outer.Count} outer / {inner} inner, not 5 / 5";
                    return false;
                }
                var normal = (outerSum / 5f - innerSum / 5f).normalized;
                // Thicker about the centre along the normal - exactly CrystalSpaceSquares' shaped twin.
                for (int i = 0; i < outer.Count; i++) outer[i] += normal * (Vector3.Dot(outer[i] - centre, normal) * (thickness - 1f));
                var oc = Vector3.zero;
                foreach (var p in outer) oc += p;
                oc /= 5f;
                var reference = (outer[0] - oc).normalized;
                var reference2 = Vector3.Cross(normal, reference);
                outer.Sort((a, b) => Angle(a).CompareTo(Angle(b)));
                float Angle(Vector3 p) => Mathf.Atan2(Vector3.Dot(p - oc, reference2), Vector3.Dot(p - oc, reference));

                float bound = 0f;
                foreach (var p in corners) bound = Mathf.Max(bound, (p - centre).magnitude);
                plate = new Plate { Centre = centre, Normal = normal, OuterCentre = oc, Outer = outer.ToArray(), Bound = bound };
                problem = null;
                return true;
            }

            public float Side(int k) => (Outer[(k + 1) % 5] - Outer[k]).magnitude;

            /// <summary>A short side: one that does not touch the tip (a pentagon, which has no tip, is all short sides).</summary>
            public bool IsBack(int k) => Tip < 0 || (k != Tip && (k + 1) % 5 != Tip);
        }

        static List<List<Vector3>> TenCornerPlates(Vector3[] vertices, int[] triangles, float tolerance)
        {
            // Weld split vertices by position, then one plate per connected component of the welded triangles.
            var weldOf = new int[vertices.Length];
            var welds = new List<Vector3>();
            var cells = new Dictionary<(long, long, long), int>();
            float cell = Mathf.Max(tolerance, 1e-7f);
            for (int i = 0; i < vertices.Length; i++)
            {
                var v = vertices[i];
                var key = ((long)Mathf.Round(v.x / cell), (long)Mathf.Round(v.y / cell), (long)Mathf.Round(v.z / cell));
                if (!cells.TryGetValue(key, out int w))
                {
                    cells[key] = w = welds.Count;
                    welds.Add(v);
                }
                weldOf[i] = w;
            }
            var parent = new int[welds.Count];
            for (int i = 0; i < parent.Length; i++) parent[i] = i;
            int Find(int a)
            {
                while (parent[a] != a) a = parent[a] = parent[parent[a]];
                return a;
            }
            for (int t = 0; t + 2 < triangles.Length; t += 3)
            {
                int a = Find(weldOf[triangles[t]]), b = Find(weldOf[triangles[t + 1]]), c = Find(weldOf[triangles[t + 2]]);
                parent[a] = b;
                parent[Find(c)] = Find(b);
            }
            var byRoot = new Dictionary<int, List<Vector3>>();
            var order = new List<int>();
            for (int w = 0; w < welds.Count; w++)
            {
                int root = Find(w);
                if (!byRoot.TryGetValue(root, out var list))
                {
                    byRoot[root] = list = new List<Vector3>(PlateCorners);
                    order.Add(root);
                }
                list.Add(welds[w]);
            }
            var plates = new List<List<Vector3>>();
            foreach (int root in order)
                if (byRoot[root].Count == PlateCorners) plates.Add(byRoot[root]);
            return plates;
        }

        // ── sites ──

        bool TryGroup(List<Plate> plates, out int[] siteOf, out string problem)
        {
            siteOf = new int[plates.Count];
            Axes = new Vector3[SiteCount];
            if (Kind == ChargeSiteKind.Pentagons)
            {
                for (int q = 0; q < plates.Count; q++)
                {
                    siteOf[q] = q;
                    Axes[q] = plates[q].OuterCentre.normalized;
                }
            }
            else
            {
                // A bloom is five petals whose tips meet (all but touching) at its centre; a tip is the corner between
                // a petal's two long sides.
                float longSide = 0f;
                foreach (var plate in plates)
                {
                    int best = 0;
                    float bestSum = -1f;
                    for (int k = 0; k < 5; k++)
                    {
                        float sum = plate.Side((k + 4) % 5) + plate.Side(k);
                        if (sum > bestSum) { bestSum = sum; best = k; }
                    }
                    plate.Tip = best;
                    longSide += bestSum / 2f;
                }
                longSide /= plates.Count;
                var seeds = new List<Vector3>();
                var members = new List<int>();
                for (int q = 0; q < plates.Count; q++)
                {
                    var tip = plates[q].Outer[plates[q].Tip];
                    int site = -1;
                    for (int s = 0; s < seeds.Count && site < 0; s++)
                        if ((seeds[s] - tip).magnitude < 0.5f * longSide) site = s;
                    if (site < 0)
                    {
                        site = seeds.Count;
                        seeds.Add(tip);
                        members.Add(0);
                    }
                    siteOf[q] = site;
                    members[site]++;
                }
                if (seeds.Count != SiteCount || members.Exists(n => n != 5))
                {
                    problem = $"the petals' tips meet in {seeds.Count} blooms ({string.Join("/", members)} petals), not {SiteCount} of 5";
                    return false;
                }
                var sums = new Vector3[SiteCount];
                for (int q = 0; q < plates.Count; q++) sums[siteOf[q]] += plates[q].Outer[plates[q].Tip];
                for (int s = 0; s < SiteCount; s++) Axes[s] = sums[s].normalized;
            }

            Neighbours = new int[SiteCount][];
            for (int s = 0; s < SiteCount; s++)
            {
                var others = new List<int>();
                for (int t = 0; t < SiteCount; t++) if (t != s) others.Add(t);
                int self = s;
                others.Sort((a, b) => Vector3.Dot(Axes[self], Axes[b]).CompareTo(Vector3.Dot(Axes[self], Axes[a])));
                Neighbours[s] = others.GetRange(0, 5).ToArray();
            }
            problem = null;
            return true;
        }

        void BuildFaces(List<Plate> plates, int[] siteOf)
        {
            var centres = new Vector3[SiteCount];
            var counts = new int[SiteCount];
            for (int q = 0; q < plates.Count; q++) { centres[siteOf[q]] += plates[q].Centre; counts[siteOf[q]]++; }
            for (int s = 0; s < SiteCount; s++) centres[s] /= counts[s];
            var reach = new float[SiteCount];
            for (int q = 0; q < plates.Count; q++)
                foreach (var p in plates[q].Outer) reach[siteOf[q]] = Mathf.Max(reach[siteOf[q]], (p - centres[siteOf[q]]).magnitude);

            Faces = new ChargeGlowFace[plates.Count];
            var facesOfSite = new List<int>[SiteCount];
            for (int s = 0; s < SiteCount; s++) facesOfSite[s] = new List<int>();
            for (int q = 0; q < plates.Count; q++)
            {
                var plate = plates[q];
                int s = siteOf[q];
                var lift = plate.Normal * (GlowLift * Radius);
                var ring = new Vector3[5];
                var own = new float[5];
                var site = new float[5];
                var back = new List<Vector3>(5);
                for (int k = 0; k < 5; k++)
                {
                    ring[k] = plate.Outer[k] + lift;
                    own[k] = Mathf.Clamp01((plate.Outer[k] - plate.OuterCentre).magnitude / Mathf.Max(plate.Bound, 1e-6f));
                    site[k] = Mathf.Clamp01((plate.Outer[k] - centres[s]).magnitude / Mathf.Max(reach[s], 1e-6f));
                    if (plate.IsBack(k)) back.Add((plate.Outer[k] + plate.Outer[(k + 1) % 5]) / 2f);
                }
                Faces[q] = new ChargeGlowFace
                {
                    Site = s, Ring = ring, Centre = plate.OuterCentre + lift, OwnFalloff = own, SiteFalloff = site,
                    SiteFalloffAtCentre = Mathf.Clamp01((plate.OuterCentre - centres[s]).magnitude / Mathf.Max(reach[s], 1e-6f)),
                    Back = back.ToArray(),
                    // The omni's pentagons sit apart (squares between), so the seam runs further from their edges.
                    Sigma = Kind == ChargeSiteKind.Blooms ? plate.Bound * 0.75f : reach[0] * 1.7f,
                };
                facesOfSite[s].Add(q);
            }
            FacesOfSite = new int[SiteCount][];
            for (int s = 0; s < SiteCount; s++) FacesOfSite[s] = facesOfSite[s].ToArray();
        }

        // ── the seam net ──

        bool TryBuildNet(List<Plate> plates, int[] siteOf, out string problem)
        {
            // Each bloom's outline: its petals' short sides (a pentagon's five edges).
            var outline = new List<(Vector3 a, Vector3 b)>[SiteCount];
            for (int s = 0; s < SiteCount; s++) outline[s] = new List<(Vector3, Vector3)>();
            for (int q = 0; q < plates.Count; q++)
                for (int k = 0; k < 5; k++)
                    if (plates[q].IsBack(k)) outline[siteOf[q]].Add((plates[q].Outer[k], plates[q].Outer[(k + 1) % 5]));
            float rOut = 0f;
            for (int s = 0; s < SiteCount; s++)
            {
                float sum = 0f;
                foreach (var seg in outline[s]) sum += seg.a.magnitude;
                rOut += sum / outline[s].Count;
            }
            rOut /= SiteCount;
            float lift = SeamLift * Radius;

            // The point in the gap among a set of blooms, nearest direction q: the mean of each outline's nearest point,
            // lifted just above the highest of them.
            Vector3 GapPoint(int[] among, Vector3 q)
            {
                var target = q * rOut;
                Vector3 sum = Vector3.zero;
                float high = 0f;
                foreach (int s in among)
                {
                    var p = NearestOn(outline[s], target);
                    sum += p;
                    high = Mathf.Max(high, p.magnitude);
                }
                return (sum / among.Length).normalized * (high + lift);
            }

            var triples = new List<int[]>();
            for (int s = 0; s < SiteCount; s++)
                foreach (int t in Neighbours[s])
                    foreach (int u in Neighbours[s])
                        if (s < t && t < u && System.Array.IndexOf(Neighbours[t], u) >= 0) triples.Add(new[] { s, t, u });
            Junctions = new Vector3[triples.Count];
            JunctionSites = triples.ToArray();
            for (int j = 0; j < triples.Count; j++)
                Junctions[j] = GapPoint(triples[j], (Axes[triples[j][0]] + Axes[triples[j][1]] + Axes[triples[j][2]]).normalized);

            var seams = new List<ChargeSeam>();
            var ofJunction = new List<int>[triples.Count];
            for (int j = 0; j < triples.Count; j++) ofJunction[j] = new List<int>(3);
            for (int i = 0; i < triples.Count; i++)
                for (int j = i + 1; j < triples.Count; j++)
                {
                    var shared = new List<int>(2);
                    foreach (int s in triples[i]) if (System.Array.IndexOf(triples[j], s) >= 0) shared.Add(s);
                    if (shared.Count != 2) continue;
                    var pair = shared.ToArray();
                    var points = new Vector3[(int)SeamSamples + 1];
                    Vector3 a = Junctions[i].normalized, b = Junctions[j].normalized;
                    points[0] = Junctions[i];
                    for (int k = 1; k < SeamSamples; k++) points[k] = GapPoint(pair, Vector3.Lerp(a, b, k / SeamSamples).normalized);
                    points[(int)SeamSamples] = Junctions[j];
                    ofJunction[i].Add(seams.Count);
                    ofJunction[j].Add(seams.Count);
                    float length = 0f;
                    for (int k = 1; k < points.Length; k++) length += (points[k] - points[k - 1]).magnitude;
                    seams.Add(new ChargeSeam { A = i, B = j, SiteA = pair[0], SiteB = pair[1], Points = points, Length = length });
                }
            Seams = seams.ToArray();
            SeamsOfJunction = new int[triples.Count][];
            for (int j = 0; j < triples.Count; j++) SeamsOfJunction[j] = ofJunction[j].ToArray();
            if (Junctions.Length != 20 || Seams.Length != 30 || System.Array.Exists(SeamsOfJunction, e => e.Length != 3))
            {
                problem = $"the seam net has {Junctions.Length} junctions and {Seams.Length} seams, not the dodecahedron's 20 and 30";
                return false;
            }
            problem = null;
            return true;
        }

        static Vector3 NearestOn(List<(Vector3 a, Vector3 b)> segments, Vector3 q)
        {
            Vector3 best = Vector3.zero;
            float bestDistance = float.MaxValue;
            foreach (var (a, b) in segments)
            {
                var ab = b - a;
                float u = Mathf.Clamp01(Vector3.Dot(q - a, ab) / Mathf.Max(1e-12f, Vector3.Dot(ab, ab)));
                var p = a + ab * u;
                float d = (p - q).sqrMagnitude;
                if (d < bestDistance) { bestDistance = d; best = p; }
            }
            return best;
        }

        void BuildCorners(List<Plate> plates, int[] siteOf)
        {
            Corners = new Vector3[SiteCount][];
            if (Kind == ChargeSiteKind.Pentagons)
            {
                float lift = CornerLift * Radius;
                for (int q = 0; q < plates.Count; q++)
                {
                    var corners = new Vector3[5];
                    for (int k = 0; k < 5; k++) corners[k] = plates[q].Outer[k] + plates[q].Normal * lift;
                    Corners[siteOf[q]] = corners;
                }
                return;
            }
            // A bloom's arc ends are the five junctions round it.
            for (int s = 0; s < SiteCount; s++)
            {
                var corners = new List<Vector3>(5);
                for (int j = 0; j < Junctions.Length; j++)
                    if (System.Array.IndexOf(JunctionSites[j], s) >= 0) corners.Add(Junctions[j]);
                Corners[s] = corners.ToArray();
            }
        }
    }

    /// <summary>
    /// One flux arc's path: a centre line that bows out between the two sites' axes, and five strands round it - fanned onto
    /// the source's corners over the first <c>fork</c> of the arc and onto the sink's over the last, a coaxial bundle
    /// between. The corners are paired so the five keep their order round the arc, so no two strands cross.
    /// </summary>
    public sealed class ChargeFluxPath
    {
        public readonly int Source, Sink;
        public readonly Vector3[] SourceOffsets, SinkOffsets;
        readonly Vector3 sourceAxis, sinkAxis;
        readonly float angle, bow, sourceRadius, sinkRadius;

        public ChargeFluxPath(ChargeSites sites, int source, int sink)
        {
            Source = source;
            Sink = sink;
            var cs = sites.Corners[source];
            var cd = sites.Corners[sink];
            Vector3 a = Vector3.zero, b = Vector3.zero;
            foreach (var c in cs) a += c;
            foreach (var c in cd) b += c;
            a /= cs.Length;
            b /= cd.Length;
            sourceAxis = sites.Axes[source];
            sinkAxis = sites.Axes[sink];
            angle = Mathf.Acos(Mathf.Clamp(Vector3.Dot(sourceAxis, sinkAxis), -1f, 1f));
            bow = 0.16f + 0.3f * angle / Mathf.PI;
            sourceRadius = a.magnitude;
            sinkRadius = b.magnitude;
            SourceOffsets = new Vector3[cs.Length];
            SinkOffsets = new Vector3[cs.Length];
            for (int k = 0; k < cs.Length; k++) SourceOffsets[k] = cs[k] - a;
            BuildFrame();

            // Pair each source corner with a sink corner so the five keep their ORDER round the arc: a coaxial bundle cannot
            // reorder its strands without crossing them. (Turning the source onto the sink, the first pairing, reverses the
            // order as seen along the arc - it leaves one face and enters the other - and every strand crossed mid-arc.)
            // Both sets are sorted by angle round the arc in its own frame; of the five cyclic matchings, the one that turns
            // the strands least and most alike.
            int n = cs.Length;
            var sourceAngles = new float[n];
            var sinkAngles = new float[n];
            for (int k = 0; k < n; k++) sourceAngles[k] = AngleIn(SourceOffsets[k], 0f);
            for (int j = 0; j < n; j++) sinkAngles[j] = AngleIn(cd[j] - b, 1f);
            var sourceOrder = new int[n];
            var sinkOrder = new int[n];
            for (int i = 0; i < n; i++) { sourceOrder[i] = i; sinkOrder[i] = i; }
            System.Array.Sort(sourceOrder, (x, y) => sourceAngles[x].CompareTo(sourceAngles[y]));
            System.Array.Sort(sinkOrder, (x, y) => sinkAngles[x].CompareTo(sinkAngles[y]));
            int bestShift = 0;
            float bestCost = float.MaxValue, bestMean = 0f;
            for (int shift = 0; shift < n; shift++)
            {
                // the mean swing first, so every strand turns by about the same amount
                float sum = 0f;
                for (int i = 0; i < n; i++) sum += Wrap(sinkAngles[sinkOrder[(i + shift) % n]] - sourceAngles[sourceOrder[i]]);
                float mean = sum / n, cost = mean * mean;
                for (int i = 0; i < n; i++)
                {
                    float d = Wrap(sinkAngles[sinkOrder[(i + shift) % n]] - sourceAngles[sourceOrder[i]] - mean);
                    cost += d * d;
                }
                if (cost < bestCost) { bestCost = cost; bestShift = shift; bestMean = mean; }
            }
            // Through the middle the five sit EXACTLY 2pi/5 apart and turn together by the mean swing; each blends from its
            // true corner angle over the source fork and back out to its true sink angle over the sink fork (the corners,
            // seen obliquely in the arc's frame, are not evenly spaced round it).
            float step = 2f * Mathf.PI / n;
            float sx = 0f, sy = 0f;
            for (int i = 0; i < n; i++)
            {
                float rel = sourceAngles[sourceOrder[i]] - i * step;
                sx += Mathf.Cos(rel);
                sy += Mathf.Sin(rel);
            }
            float reference = Mathf.Atan2(sy, sx);
            meanTurn = bestMean;
            slotAngle = new float[n];
            startOff = new float[n];
            endOff = new float[n];
            for (int i = 0; i < n; i++)
            {
                int k = sourceOrder[i], j = sinkOrder[(i + bestShift) % n];
                SinkOffsets[k] = cd[j] - b;
                slotAngle[k] = reference + i * step;
                startOff[k] = Wrap(sourceAngles[k] - slotAngle[k]);
                endOff[k] = Wrap(sinkAngles[j] - (slotAngle[k] + meanTurn));
            }
        }

        static float Wrap(float a) => a - 2f * Mathf.PI * Mathf.Round(a / (2f * Mathf.PI));

        /// <summary>The centre line at <paramref name="u"/> (0 at the source, 1 at the sink).</summary>
        public Vector3 Centre(float u)
        {
            var dir = (sourceAxis * Mathf.Sin((1f - u) * angle) + sinkAxis * Mathf.Sin(u * angle)).normalized;
            return dir * ((sourceRadius + (sinkRadius - sourceRadius) * u) * (1f + bow * Mathf.Sin(Mathf.PI * u)));
        }

        /// <summary>
        /// Strand <paramref name="k"/> at <paramref name="u"/>: on the source's corner at 0, on its paired sink corner at 1,
        /// and between the forks one of five COAXIAL filaments, <paramref name="bundle"/> off the centre line. Each keeps its
        /// own angle round the line in a frame carried along it without twisting (rotation-minimizing): its source corner's
        /// angle, turning smoothly into its sink corner's, plus the bundle's <paramref name="turns"/> whole turns (a braided
        /// cable; whole turns, so each still lands on its own corner). The five stay 72 degrees apart, as the corners are,
        /// so they never touch. One strand runs the whole arc: there is no separate beam and no seam.
        /// </summary>
        public Vector3 Strand(int k, float u, float fork, float bundle, int turns)
        {
            Frame(u, out var e1, out var e2);
            float into = ChargeArcs.Smooth(u / fork), outOf = ChargeArcs.Smooth((u - (1f - fork)) / fork);
            float theta = slotAngle[k] + meanTurn * ChargeArcs.Smooth(u)
                        + 2f * Mathf.PI * turns * ChargeArcs.Smooth((u - fork) / Mathf.Max(1e-4f, 1f - 2f * fork))
                        + startOff[k] * (1f - into) + endOff[k] * outOf;
            var ring = (e1 * Mathf.Cos(theta) + e2 * Mathf.Sin(theta)) * bundle;
            // the corner's own offset at each end, the ring between (fork <= 0.5, so the two ends never overlap)
            var offset = SourceOffsets[k] * (1f - into) + ring * (into * (1f - outOf)) + SinkOffsets[k] * outOf;
            return Centre(u) + offset;
        }

        // The rotation-minimizing frame along the centre line, sampled; and each strand's angle in it at the source and how
        // far that angle turns to reach its sink corner.
        const int FrameSamples = 128;
        readonly Vector3[] frameTangent = new Vector3[FrameSamples + 1], frameE1 = new Vector3[FrameSamples + 1];
        readonly float[] slotAngle, startOff, endOff;
        readonly float meanTurn;

        void BuildFrame()
        {
            for (int i = 0; i <= FrameSamples; i++)
            {
                float u = (float)i / FrameSamples;
                frameTangent[i] = (Centre(Mathf.Min(1f, u + 0.002f)) - Centre(Mathf.Max(0f, u - 0.002f))).normalized;
            }
            var t0 = frameTangent[0];
            var seed = SourceOffsets[0] - t0 * Vector3.Dot(SourceOffsets[0], t0);
            if (seed.sqrMagnitude < 1e-12f) seed = Vector3.Cross(t0, Mathf.Abs(t0.x) < 0.9f ? new Vector3(1, 0, 0) : new Vector3(0, 1, 0));
            frameE1[0] = seed.normalized;
            for (int i = 1; i <= FrameSamples; i++)
            {
                var carried = Quaternion.FromToRotation(frameTangent[i - 1], frameTangent[i]) * frameE1[i - 1];
                frameE1[i] = (carried - frameTangent[i] * Vector3.Dot(carried, frameTangent[i])).normalized;
            }
        }

        void Frame(float u, out Vector3 e1, out Vector3 e2)
        {
            float x = Mathf.Clamp01(u) * FrameSamples;
            int i = Mathf.Min((int)x, FrameSamples - 1);
            float f = x - i;
            var tangent = Vector3.Lerp(frameTangent[i], frameTangent[i + 1], f).normalized;
            var a = Vector3.Lerp(frameE1[i], frameE1[i + 1], f);
            e1 = (a - tangent * Vector3.Dot(a, tangent)).normalized;
            e2 = Vector3.Cross(tangent, e1);
        }

        float AngleIn(Vector3 offset, float u)
        {
            Frame(u, out var e1, out var e2);
            return Mathf.Atan2(Vector3.Dot(offset, e2), Vector3.Dot(offset, e1));
        }
    }

    /// <summary>
    /// The seam walk: one bolt walking the seams between the blooms forever, struck seam by seam, held flickering and
    /// faded behind it (no core, no escape). Never straight back, never a fifth seam round one bloom (that would circle
    /// it). Stepped forward in time; it keeps only the seams still visible.
    /// </summary>
    public sealed class ChargeSeamWalker
    {
        public struct Leg
        {
            public int Seam, Serial;
            public bool Forward;
            public float Start, Struck;
            /// <summary>When the bolt moves on to the next seam (the end of its hold here).</summary>
            public float Leaves;
            /// <summary>How far the walk had gone before this seam (sum of the seams' lengths): a fixed place along the
            /// walk for the plasma's noise.</summary>
            public float Odometer;
        }

        readonly ChargeSites sites;
        readonly System.Random random;
        readonly List<Leg> legs = new();
        readonly List<int> recent = new(4);
        readonly List<int> options = new(3);
        int at, previous = -1, serial;
        float end, odometer;

        public ChargeSeamWalker(ChargeSites sites, int seed, float startSeconds)
        {
            this.sites = sites;
            random = new System.Random(seed);
            at = random.Next(sites.Junctions.Length);
            end = startSeconds;
        }

        public IReadOnlyList<Leg> Legs => legs;

        /// <summary>Strikes every seam due by <paramref name="seconds"/> (and a second ahead), and forgets the ones that
        /// have faded out.</summary>
        public void Advance(float seconds, in ChargeArcSettings settings)
        {
            float span = settings.Span;
            while (end <= seconds + 1f)
            {
                options.Clear();
                foreach (int e in sites.SeamsOfJunction[at]) if (e != previous) options.Add(e);
                if (recent.Count >= 4)
                {
                    // never a fifth seam round one bloom
                    var first = sites.Seams[recent[0]];
                    foreach (int site in new[] { first.SiteA, first.SiteB })
                    {
                        if (!recent.TrueForAll(e => sites.Seams[e].Touches(site))) continue;
                        if (options.Exists(e => !sites.Seams[e].Touches(site))) options.RemoveAll(e => sites.Seams[e].Touches(site));
                    }
                }
                int seam = options[random.Next(options.Count)];
                var edge = sites.Seams[seam];
                bool forward = edge.A == at;
                float struck = end + settings.SeamStrike * span;
                float leaves = struck + Mathf.Lerp(settings.SeamDwellMin, settings.SeamDwellMax, (float)random.NextDouble()) * span;
                legs.Add(new Leg { Seam = seam, Serial = serial++, Forward = forward, Start = end, Struck = struck, Leaves = leaves,
                                   Odometer = odometer });
                odometer += edge.Length;
                end = leaves;
                at = forward ? edge.B : edge.A;
                previous = seam;
                recent.Add(seam);
                if (recent.Count > 4) recent.RemoveAt(0);
            }
            int drop = 0;
            while (drop < legs.Count && seconds - legs[drop].Leaves > settings.SeamFade * span) drop++;
            if (drop > 0) legs.RemoveRange(0, drop);
        }
    }

    /// <summary>
    /// The flux arcs' chain: discharge k leaps from the site discharge k-1 landed on (never straight back) to a site
    /// between the settings' two angles from it. One seeded sequence, extended as it is needed.
    /// </summary>
    public sealed class ChargeFluxChain
    {
        public struct Discharge
        {
            public long Index;
            public int Source, Sink;
        }

        readonly ChargeSites sites;
        readonly System.Random random;
        readonly List<Discharge> kept = new();
        readonly List<int> candidates = new(SiteCountHint);
        const int SiteCountHint = ChargeSites.SiteCount;
        const int Keep = 8;
        long next;

        public ChargeFluxChain(ChargeSites sites, int seed)
        {
            this.sites = sites;
            random = new System.Random(seed);
        }

        public bool TryGet(long index, in ChargeArcSettings settings, out Discharge discharge)
        {
            discharge = default;
            if (index < 0) return false;
            while (next <= index)
            {
                bool first = kept.Count == 0;
                var last = first ? default : kept[kept.Count - 1];
                int source = first ? random.Next(ChargeSites.SiteCount) : last.Sink;
                float lo = Mathf.Cos(settings.FluxMaxDegrees * Mathf.Deg2Rad), hi = Mathf.Cos(settings.FluxMinDegrees * Mathf.Deg2Rad);
                candidates.Clear();
                for (int i = 0; i < ChargeSites.SiteCount; i++)
                {
                    if (i == source || (!first && i == last.Source)) continue;
                    float d = Vector3.Dot(sites.Axes[source], sites.Axes[i]);
                    if (d < hi && d > lo) candidates.Add(i);
                }
                if (candidates.Count == 0)
                    for (int i = 0; i < ChargeSites.SiteCount; i++) if (i != source) candidates.Add(i);
                kept.Add(new Discharge { Index = next++, Source = source, Sink = candidates[random.Next(candidates.Count)] });
                if (kept.Count > Keep) kept.RemoveAt(0);
            }
            for (int i = kept.Count - 1; i >= 0; i--)
                if (kept[i].Index == index) { discharge = kept[i]; return true; }
            return false;
        }
    }

    /// <summary>
    /// Writes the Charge light into one mesh, in the crystal's own space: camera-facing plasma ribbons (one continuous
    /// strip per bolt, plus its sub-filament) and the lit plates' faces. TEXCOORD0 = (across the strip -1..1, falloff from
    /// the light's centre 0..1, kind: 0 strip / 1 face, intensity) - read by <c>CrystalChargeArcsShader</c>.
    /// Nothing is allocated per frame once the lists have grown.
    /// </summary>
    public sealed class ChargeArcMeshWriter
    {
        public readonly List<Vector3> Positions = new();
        public readonly List<Vector4> Data = new();
        public readonly List<int> Indices = new();

        public void Clear()
        {
            Positions.Clear();
            Data.Clear();
            Indices.Clear();
        }

        /// <summary>A lit face, as a fan from its centre: <paramref name="ownCentre"/> picks the falloff from the face's
        /// own centre (a petal lit by a seam) or from its site's (a pentagon lit by a discharge).</summary>
        public void Face(ChargeGlowFace face, float intensity, bool ownCentre)
        {
            int c = Positions.Count;
            Positions.Add(face.Centre);
            Data.Add(new Vector4(0f, ownCentre ? 0f : face.SiteFalloffAtCentre, 1f, intensity));
            for (int k = 0; k < face.Ring.Length; k++)
            {
                Positions.Add(face.Ring[k]);
                Data.Add(new Vector4(0f, ownCentre ? face.OwnFalloff[k] : face.SiteFalloff[k], 1f, intensity));
            }
            int n = face.Ring.Length;
            for (int k = 0; k < n; k++)
            {
                Indices.Add(c);
                Indices.Add(c + 1 + k);
                Indices.Add(c + 1 + (k + 1) % n);
            }
        }

        /// <summary>
        /// One bolt along <paramref name="points"/> as a single continuous PLASMA ribbon. The reference is the Squirrel's
        /// skim crackle (<c>ForcefieldCrackle.hlsl</c>): one smooth curve bent by FBM value noise, a Gaussian
        /// cross-section that runs from the glow colour to a hot core, and a thinner sub-filament at 0.4 beside it.
        /// <list type="bullet">
        /// <item>The path is resampled every 0.0125 radii, on a grid fixed along the bolt. Each sample is bent off the path by smooth noise that is
        /// continuous along the bolt and in time (it scrolls at <see cref="ChargeArcSettings.WritheSpeed"/>; nothing is
        /// re-rolled), held to zero within ~0.06 radii of each end when <paramref name="pinned"/>, so a bolt still lands
        /// where it is aimed.</item>
        /// <item>One camera-facing strip whose two vertices per sample are SHARED with the next sample's, so it never
        /// parts at a joint. The shader shapes the cross-section from the across coordinate.</item>
        /// <item><paramref name="weights"/> (one per input point, null = 1) scales the brightness along the bolt: the
        /// seam walk's fading tail.</item>
        /// </list>
        /// </summary>
        public void Plasma(IReadOnlyList<Vector3> points, int count, IReadOnlyList<float> weights, IReadOnlyList<float> coords,
            float intensity, float width, float wobble, int seed, float seconds, in ChargeArcSettings settings, Vector3 eye, float radius,
            bool pinned = true)
        {
            if (count < 2 || intensity <= 0.01f) return;
            float breath = 0.8f + 0.2f * ChargeArcs.Fbm(seconds * settings.FlickerSpeed + seed * 0.731f, 2);
            Ribbon(points, count, weights, coords, intensity * breath, width, wobble, seed, seconds, settings, eye, radius, pinned);
            // the sub-filament: thinner, fainter, bent further on its own noise (the crackle's subLine)
            Ribbon(points, count, weights, coords, intensity * breath * 0.4f, width * 0.45f, wobble * 1.7f, seed + 101, seconds, settings,
                   eye, radius, pinned);
        }

        /// <param name="coords">Each input point's place along the bolt for the noise, in radii (null = its arc length from
        /// the first point). A bolt whose tail falls away (the seam walk) gives each point a fixed one, so the plasma's
        /// shape does not slide along it as the tail goes.</param>
        void Ribbon(IReadOnlyList<Vector3> points, int count, IReadOnlyList<float> weights, IReadOnlyList<float> coords,
            float intensity, float width, float wobble, int seed, float seconds, in ChargeArcSettings settings, Vector3 eye, float radius,
            bool pinned)
        {
            // each input point's arc length, and its place along the bolt (its coord, or its arc length)
            arcAt.Clear();
            placeAt.Clear();
            float total = 0f;
            for (int i = 0; i < count; i++)
            {
                if (i > 0) total += (points[i] - points[i - 1]).magnitude;
                arcAt.Add(total);
                placeAt.Add(coords == null ? total : coords[i] * radius);
            }
            if (total < 1e-6f) return;
            float step = SampleSpacing * radius;
            float from = placeAt[0], to = placeAt[count - 1];
            if (to <= from) return;
            // Samples sit on a grid FIXED in the bolt's own place (multiples of the spacing), plus its two ends: a trail
            // whose tail falls away keeps every sample where it was, so the finer octaves of the noise never alias into
            // a shimmer as the grid slides along.
            bent.Clear();
            weight.Clear();
            float flow = seconds * settings.WritheSpeed, phase1 = seed * 1.37f, phase2 = seed * 2.91f + 50f;
            int seg = 0;
            float next = Mathf.Floor(from / step) * step + step;
            if (next - from < 0.25f * step) next += step;   // no sliver piece at either end
            int guard = 0;
            for (float place = from; guard++ < 512; place = to - next < 0.25f * step ? to : next, next += step)
            {
                while (seg < count - 2 && place > placeAt[seg + 1]) seg++;
                float span = placeAt[seg + 1] - placeAt[seg];
                float f = span > 1e-9f ? Mathf.Clamp01((place - placeAt[seg]) / span) : 0f;
                var p = Vector3.LerpUnclamped(points[seg], points[seg + 1], f);
                // the tangent blended between the input points' own (each the mean of its two segments), so the
                // directions the plasma bends in turn smoothly through every joint of the path, a seam junction too
                var tangent = Vector3.Lerp(PointTangent(points, count, seg), PointTangent(points, count, seg + 1), f).normalized;
                var n1 = Vector3.Cross(tangent, eye - p).normalized;
                var n2 = Vector3.Cross(tangent, n1);
                float at = Mathf.Lerp(arcAt[seg], arcAt[seg + 1], f);
                float x = 6f * place / radius;
                // pinned only near the ends, so a bolt lands where it is aimed (a whole-length envelope would re-scale the
                // shape every time the length changed); a trail that is aimed at nothing (the seam walk) is not pinned, so
                // its shape stays put as its tail falls away
                float pin = !pinned ? 1f
                    : ChargeArcs.Smooth(at / (PinLength * radius)) * ChargeArcs.Smooth((total - at) / (PinLength * radius));
                float d1 = ChargeArcs.Fbm(x + phase1 - flow, 3), d2 = ChargeArcs.Fbm(x + phase2 + flow * 0.8f, 3);
                bent.Add(p + (n1 * d1 + n2 * (0.6f * d2)) * (wobble * radius * pin));
                weight.Add(weights == null ? 1f : Mathf.Lerp(weights[seg], weights[seg + 1], f));
                if (place >= to) break;
            }
            float half = ChargeArcs.StripHalfWidth(settings.CoreWidth, width) * radius;
            int first = Positions.Count;
            for (int k = 0; k < bent.Count; k++)
            {
                var p = bent[k];
                var along = bent[Mathf.Min(k + 1, bent.Count - 1)] - bent[Mathf.Max(k - 1, 0)];
                var side = Vector3.Cross(along, eye - p).normalized * half;
                float v = intensity * weight[k];
                Positions.Add(p + side); Data.Add(new Vector4(1f, 0f, 0f, v));
                Positions.Add(p - side); Data.Add(new Vector4(-1f, 0f, 0f, v));
                if (k == 0) continue;
                int i = first + 2 * (k - 1);
                Indices.Add(i); Indices.Add(i + 1); Indices.Add(i + 2);
                Indices.Add(i + 2); Indices.Add(i + 1); Indices.Add(i + 3);
            }
        }

        static Vector3 PointTangent(IReadOnlyList<Vector3> points, int count, int i)
        {
            var before = i > 0 ? (points[i] - points[i - 1]).normalized : Vector3.zero;
            var after = i + 1 < count ? (points[i + 1] - points[i]).normalized : Vector3.zero;
            var t = before + after;
            return t.sqrMagnitude > 1e-12f ? t.normalized : (after.sqrMagnitude > 0f ? after : before);
        }

        const float PinLength = 0.06f;       // x the radius: how far in from each end the plasma is held to its path
        const float SampleSpacing = 0.0125f; // x the radius: finer than the noise's finest octave (~0.035), so it never aliases
        readonly List<float> arcAt = new(129), placeAt = new(129);
        readonly List<Vector3> bent = new(257);
        readonly List<float> weight = new(257);
    }

    /// <summary>
    /// The two Charge effects the Omni Motion Lab picked (round 5), written frame by frame into a
    /// <see cref="ChargeArcMeshWriter"/>:
    ///
    ///   SEAM WALK (the Charge crystal) - <see cref="ChargeSeamWalker"/>'s bolt, and each petal lit by how near the bolt
    ///   runs to its short sides and how fresh that stretch of bolt is.
    ///   FLUX (the omni) - a pentagon charges (only it lights), an arc leaps to a pentagon 55-125 degrees away, which
    ///   lights as it takes the discharge; both fade, and the next discharge leaps on from the sink. At each end the arc
    ///   is five strands, each ONE ribbon from a source corner to a sink corner: fanned onto the corners at each end, a
    ///   coaxial bundle (a braided cable) between - no separate beam, no seam.
    ///
    /// JS twin: <c>seamWalkFx()</c>, <c>seamLight()</c> and <c>fluxFx()</c> in <c>Docs/Studios/OmniMotionLab.html</c>.
    /// </summary>
    public static class ChargeArcs
    {

        public static float Smooth(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }

        /// <summary>The lab's integer hash (0..1).</summary>
        public static float Hash(int a, int b)
        {
            unchecked
            {
                uint h = (uint)a * 374761393u ^ (uint)b * 668265263u;
                h = (h ^ (h >> 13)) * 1274126177u;
                return (h ^ (h >> 16)) / 4294967296f;
            }
        }

        /// <summary>Smooth 1-D value noise (0..1), C1-continuous: the crackle's <c>ValueNoise1D</c> on an integer hash.</summary>
        public static float ValueNoise(float x)
        {
            float i = Mathf.Floor(x), f = x - i;
            f = f * f * (3f - 2f * f);
            int n = (int)i;
            return Mathf.Lerp(Hash(n, 977), Hash(n + 1, 977), f);
        }

        /// <summary>Fractal value noise, about -0.9..0.9: the crackle's <c>FBM1D</c> (x2.17 per octave, half the amplitude).</summary>
        public static float Fbm(float x, int octaves)
        {
            float value = 0f, amplitude = 0.5f, frequency = 1f;
            for (int o = 0; o < octaves; o++)
            {
                value += amplitude * (ValueNoise(x * frequency + o * 19.1f) * 2f - 1f);
                frequency *= 2.17f;
                amplitude *= 0.5f;
            }
            return value;
        }

        /// <summary>A plasma strip's half-width, x the crystal's radius: wide enough for the shader's glow to fall to
        /// nothing before the strip's edge.</summary>
        public static float StripHalfWidth(float coreWidth, float width) => coreWidth * width * 2.2f;

        static readonly List<Vector3> s_points = new(StrandSteps + 1);
        static readonly List<float> s_weights = new(128);
        static readonly List<float> s_coords = new(128);
        static readonly List<Vector4> s_lit = new(128);   // (point, life)
        static readonly List<float> s_reach = new(128);   // per lit point: the largest d*d at which it can still light a petal
        const float PetalFloor = 0.01f;                   // a petal lights only above this

        // The looks, x the crystal's radius: how far each kind of bolt bends off its path.
        const float SeamWobble = 0.02f, BundleWobble = 0.02f;
        const int StrandSteps = 96;   // points along a flux strand: enough for a smooth helix through the bundle

        /// <summary>
        /// The seam walk at <paramref name="seconds"/>: ONE continuous plasma trail through every seam still lit, oldest
        /// first (each seam starts where the last ended), its brightness falling off smoothly toward the tail by how long
        /// ago each point was struck; and its petal light into <paramref name="faceLight"/> (one per face). Returns the
        /// number of points on the trail.
        /// </summary>
        public static int SeamWalk(ChargeSites sites, ChargeSeamWalker walker, float seconds, in ChargeArcSettings settings,
            ChargeArcMeshWriter writer, Vector3 eye, float[] faceLight)
        {
            System.Array.Clear(faceLight, 0, faceLight.Length);
            float span = settings.Span, fade = settings.SeamFade * span;
            s_points.Clear();
            s_weights.Clear();
            s_coords.Clear();
            s_lit.Clear();
            var legs = walker.Legs;
            for (int l = 0; l < legs.Count; l++)
            {
                var leg = legs[l];
                if (leg.Start > seconds) break;
                var pts = sites.Seams[leg.Seam].Points;
                int n = pts.Length - 1;
                float seamLength = sites.Seams[leg.Seam].Length;
                float strike = Mathf.Max(1e-6f, leg.Struck - leg.Start);
                float reach = Mathf.Clamp01((seconds - leg.Start) / strike) * n;
                for (int i = 0; i <= n; i++)
                {
                    if (i == 0 && s_points.Count > 0) continue;   // the junction the last seam ended on
                    bool partial = i > reach;
                    float at = partial ? reach : i;
                    int lo = Mathf.Min((int)at, n - 1);
                    var a = pts[leg.Forward ? lo : n - lo];
                    var b = pts[leg.Forward ? lo + 1 : n - lo - 1];
                    var point = Vector3.Lerp(a, b, at - lo);
                    // Each point fades from when the bolt LEFT it: a seam's hold is spread across its points, so the last
                    // point of one seam and the first of the next are the same moment and the brightness runs on without a
                    // step (a step there cut the trail's tail off at a third of its brightness when the old seam died).
                    // A point the bolt has not left yet (it is still holding on this seam) is at full brightness.
                    float leftAt = Mathf.Min(seconds, leg.Start + (leg.Leaves - leg.Start) * at / n);
                    float life = 1f - Smooth(Mathf.Max(0f, seconds - leftAt) / fade);
                    if (life > 0.001f || s_points.Count > 0)
                    {
                        s_points.Add(point);
                        s_weights.Add(life);
                        // a fixed place along the walk for the noise, so the trail's shape does not slide as its tail goes
                        s_coords.Add((leg.Odometer + seamLength * at / n) / sites.Radius);
                        s_lit.Add(new Vector4(point.x, point.y, point.z, life));
                    }
                    if (partial) break;
                }
            }
            writer.Plasma(s_points, s_points.Count, s_weights, s_coords, settings.Bolts * 2.4f, 2f, SeamWobble, 7, seconds,
                          settings, eye, sites.Radius, pinned: false);
            // a petal glows by how near the bolt runs to its short sides, and how fresh that stretch of bolt is.
            // A petal lights only when its brightest term passes PetalFloor, so a term that cannot reach it cannot change
            // the light: lit.w * e^(-d*d) < PetalFloor * 0.99 whenever d*d > ln(lit.w / (PetalFloor * 0.99)). Those terms
            // are skipped on their squared distance, before the square root and the exponential; every term that is kept
            // is computed exactly as before, so the light is identical. Most terms are far from the petal: this is the
            // seam walk's dominant cost (60 petals x every lit point x 3 back points, each an Exp), and with
            // double-precision Exp (Unity's Mathf.Exp) it measured ~6x the rest of the effect.
            s_reach.Clear();
            foreach (var lit in s_lit) s_reach.Add(lit.w > PetalFloor * 0.99f ? Mathf.Log(lit.w / (PetalFloor * 0.99f)) : -1f);
            for (int f = 0; f < sites.Faces.Length; f++)
            {
                var face = sites.Faces[f];
                float g = 0f;
                float inverseSigmaSquared = 1f / (face.Sigma * face.Sigma);
                for (int i = 0; i < s_lit.Count; i++)
                {
                    float reach = s_reach[i];
                    if (reach < 0f) continue;
                    var lit = s_lit[i];
                    var q = (Vector3)lit;
                    foreach (var back in face.Back)
                    {
                        if ((back - q).sqrMagnitude * inverseSigmaSquared > reach) continue;
                        float d = (back - q).magnitude / face.Sigma;
                        g = Mathf.Max(g, lit.w * Mathf.Exp(-d * d));
                    }
                }
                if (g > PetalFloor) faceLight[f] = settings.Glow * settings.SeamPetalGlow * 2.4f * g;
            }
            return s_points.Count;
        }

        /// <summary>The flux arcs at <paramref name="seconds"/>: their plasma, and the source and sink light into
        /// <paramref name="faceLight"/>. Returns the number of discharges drawn.</summary>
        public static int Flux(ChargeSites sites, ChargeFluxChain chain, float seconds, in ChargeArcSettings settings,
            ChargeArcMeshWriter writer, Vector3 eye, float[] faceLight)
        {
            System.Array.Clear(faceLight, 0, faceLight.Length);
            if (seconds < 0f) return 0;
            float fork = settings.FluxFork;
            int drawn = 0;
            long now = Now(seconds, settings);
            for (long k = now; k >= 0 && k > now - 4; k--)
            {
                if (!Discharge(chain, k, seconds, settings, out var discharge, out float sourceLight, out float sinkLight,
                               out float grow, out float intensity))
                    continue;
                Light(sites, faceLight, discharge.Source, settings.Glow * sourceLight);
                if (sinkLight > 0f) Light(sites, faceLight, discharge.Sink, settings.Glow * sinkLight);
                if (intensity <= 0.01f) continue;
                var path = sites.PathOf(discharge.Source, discharge.Sink);
                int seed = (int)(k * 53);
                // Five strands, each ONE ribbon the whole way from its source corner to its sink corner: fanned onto the
                // corners at each end, a coaxial bundle between. They share one noise seed, so the bundle bends as one and
                // its five filaments stay coaxial while it writhes.
                int steps = Mathf.RoundToInt(grow * StrandSteps);
                for (int q = 0; q < path.SourceOffsets.Length; q++)
                {
                    StrandPoints(path, q, steps, fork, settings, sites.Radius);
                    writer.Plasma(s_points, s_points.Count, null, null, intensity * 1.1f, 0.9f, BundleWobble, seed, seconds, settings, eye, sites.Radius);
                }
                drawn++;
            }
            return drawn;
        }

        /// <summary>
        /// How hard the flux arcs strike each prism standing in their way at <paramref name="seconds"/>, into
        /// <paramref name="strike"/>: a prism whose body (a sphere at <paramref name="centres"/>[j] of radius
        /// <paramref name="radii"/>[j], in the sites' space) the arc's path passes through lights up while the arc holds,
        /// so a bolt through a raised Mass column reads as conducting, not clipping. Full where the path runs through the
        /// prism's middle, nothing once it only grazes the edge.
        /// </summary>
        public static void FluxStrikes(ChargeSites sites, ChargeFluxChain chain, float seconds, in ChargeArcSettings settings,
            Vector3[] centres, float[] radii, int count, float[] strike) =>
            FluxStrikes(sites, chain, seconds, settings, centres, radii, null, null, null, count, strike);

        /// <summary>
        /// As above, for prisms that ride columns: each prism's column place (<paramref name="places"/>, 0 at the top ..
        /// 1 sealed), where its column starts on the shell (<paramref name="seats"/>) and the radius of its body there
        /// (<paramref name="seatRadii"/>). A prism lights only as far as it stands out (<see cref="StrikeGate"/>), and a
        /// standing one lights when an arc crosses its COLUMN - anywhere from its seat up to the prism - since a prism
        /// out of the shell stands above the arcs, which bow just over it.
        /// </summary>
        public static void FluxStrikes(ChargeSites sites, ChargeFluxChain chain, float seconds, in ChargeArcSettings settings,
            Vector3[] centres, float[] radii, float[] places, Vector3[] seats, float[] seatRadii, int count, float[] strike)
        {
            s_seats = seats;
            s_seatRadii = seatRadii;
            try { FluxStrikesUngated(sites, chain, seconds, settings, centres, radii, count, strike); }
            finally { s_seats = null; s_seatRadii = null; }
            if (places == null) return;
            for (int j = 0; j < count; j++)
                if (strike[j] > 0f) strike[j] *= StrikeGate(places[j], settings);
        }

        // The columns the current FluxStrikes call tests along (null = each prism's body only). Main thread only, like
        // the rest of this writer's scratch (s_points).
        static Vector3[] s_seats;
        static float[] s_seatRadii;
        const int ColumnSamples = 8;

        /// <summary>How much of a strike a prism at column place <paramref name="place"/> (0 top .. 1 sealed) shows: none
        /// while it is retracted, all once it stands out (<see cref="ChargeArcSettings.StrikeProtractionFrom"/> ..
        /// <see cref="ChargeArcSettings.StrikeProtractionFull"/>).</summary>
        public static float StrikeGate(float place, in ChargeArcSettings settings)
        {
            float from = settings.StrikeProtractionFrom, full = Mathf.Max(from + 1e-4f, settings.StrikeProtractionFull);
            return Smooth((1f - place - from) / (full - from));
        }

        static void FluxStrikesUngated(ChargeSites sites, ChargeFluxChain chain, float seconds, in ChargeArcSettings settings,
            Vector3[] centres, float[] radii, int count, float[] strike)
        {
            System.Array.Clear(strike, 0, count);
            if (seconds < 0f) return;
            float fork = settings.FluxFork;
            long now = Now(seconds, settings);
            for (long k = now; k >= 0 && k > now - 4; k--)
            {
                if (!Discharge(chain, k, seconds, settings, out var discharge, out _, out _, out float grow, out float intensity)
                    || intensity <= 0.01f)
                    continue;
                var path = sites.PathOf(discharge.Source, discharge.Sink);
                int steps = Mathf.RoundToInt(grow * StrandSteps);
                for (int q = 0; q < path.SourceOffsets.Length; q++)
                {
                    StrandPoints(path, q, steps, fork, settings, sites.Radius);
                    StrikeAlong(s_points, centres, radii, count, intensity * 1.1f, settings, strike);
                }
            }
        }

        static void StrikeAlong(List<Vector3> path, Vector3[] centres, float[] radii, int count, float intensity,
            in ChargeArcSettings settings, float[] strike)
        {
            if (path.Count < 2) return;
            for (int j = 0; j < count; j++)
            {
                float r = radii[j];
                if (r <= 0f) continue;
                // How deep inside the body (or its column) the arc runs, as a fraction of the radius there: 0 on the axis.
                float depth = float.MaxValue;
                int samples = s_seats != null ? ColumnSamples : 0;
                for (int c = 0; c <= samples; c++)
                {
                    float along = samples == 0 ? 1f : c / (float)samples;
                    var p = samples == 0 ? centres[j] : Vector3.Lerp(s_seats[j], centres[j], along);
                    float rr = samples == 0 ? r : Mathf.Lerp(s_seatRadii[j], r, along);
                    if (rr <= 0f) continue;
                    float d = float.MaxValue;
                    for (int i = 0; i + 1 < path.Count; i++) d = Mathf.Min(d, DistanceToSegment(p, path[i], path[i + 1]));
                    depth = Mathf.Min(depth, d / rr);
                }
                if (depth >= 1f) continue;
                float v = settings.Glow * 2.4f * intensity * (1f - Smooth((depth - 0.6f) / 0.4f));
                strike[j] = Mathf.Max(strike[j], v);
            }
        }

        static float DistanceToSegment(Vector3 p, Vector3 a, Vector3 b)
        {
            var ab = b - a;
            float u = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(1e-12f, Vector3.Dot(ab, ab)));
            return (a + ab * u - p).magnitude;
        }

        static long Now(float seconds, in ChargeArcSettings settings) => (long)Mathf.Floor(seconds / (settings.FluxGap * settings.Span));

        /// <summary>Discharge <paramref name="k"/> at <paramref name="seconds"/>: its source and sink light, how far its
        /// arc has grown, and how bright the arc is. False once it has faded.</summary>
        static bool Discharge(ChargeFluxChain chain, long k, float seconds, in ChargeArcSettings settings,
            out ChargeFluxChain.Discharge discharge, out float sourceLight, out float sinkLight, out float grow, out float intensity)
        {
            float span = settings.Span, gap = settings.FluxGap * span;
            float charge = settings.FluxCharge * span, struck = charge + settings.FluxStrike * span;
            float held = struck + settings.FluxHold * span, gone = held + settings.FluxFade * span;
            float t = seconds - k * gap;
            sourceLight = sinkLight = grow = intensity = 0f;
            if (t > gone || !chain.TryGet(k, settings, out discharge)) { discharge = default; return false; }
            float fading = 1f - Smooth((t - held) / (gone - held));
            // the source charges, flares as it discharges, fades; the sink lights as the arc lands
            sourceLight = t < charge ? 1.4f * Smooth(t / charge) : t < held ? 2.2f : 2.2f * fading;
            if (t < charge) return true;
            sinkLight = t < held ? 2.4f * Smooth((t - charge) / (struck - charge)) : 2.4f * fading;
            grow = Mathf.Clamp01((t - charge) / (struck - charge));
            intensity = settings.Bolts * (t < held ? 1f : fading);
            return true;
        }

        /// <summary>Strand <paramref name="q"/> from its source corner out to step <paramref name="steps"/> of
        /// <see cref="StrandSteps"/> (how far the arc has grown), into <c>s_points</c>.</summary>
        static void StrandPoints(ChargeFluxPath path, int q, int steps, float fork, in ChargeArcSettings settings, float radius)
        {
            s_points.Clear();
            float bundle = settings.FluxBundle * radius;
            for (int i = 0; i <= steps; i++)
                s_points.Add(path.Strand(q, (float)i / StrandSteps, fork, bundle, settings.FluxTurns));
        }

        static void Light(ChargeSites sites, float[] faceLight, int site, float value)
        {
            foreach (int f in sites.FacesOfSite[site]) faceLight[f] = Mathf.Max(faceLight[f], value);
        }
    }
}
