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

        /// <summary>A bolt's body width (x the crystal's radius), how often its kinks re-roll and how often its
        /// brightness flickers (per second).</summary>
        public float CoreWidth, JagHz, FlickerHz;

        public static ChargeArcSettings Lab => new()
        {
            Rate = 1f, Bolts = 1f, Glow = 1f,
            SeamStrike = 0.07f, SeamDwellMin = 0.1f, SeamDwellMax = 0.28f, SeamFade = 0.85f,
            FluxGap = 0.9f, FluxCharge = 0.45f, FluxStrike = 0.06f, FluxHold = 0.45f, FluxFade = 0.45f, FluxFork = 0.3f,
            FluxMinDegrees = 55f, FluxMaxDegrees = 125f,
            CoreWidth = 0.012f, JagHz = 20f, FlickerHz = 18f,
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
                    seams.Add(new ChargeSeam { A = i, B = j, SiteA = pair[0], SiteB = pair[1], Points = points });
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
    /// One flux arc's path: a centre line that bows out between the two sites' axes, and five strands that fork off it
    /// onto the source's corners over the first <c>fork</c> of the arc and onto the sink's over the last. Strand k at the
    /// source is paired with the sink corner it would reach if the source were turned onto the sink, so no two strands
    /// cross.
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
            var turn = Quaternion.AngleAxis(angle * Mathf.Rad2Deg, Vector3.Cross(sourceAxis, sinkAxis).normalized);
            SourceOffsets = new Vector3[cs.Length];
            SinkOffsets = new Vector3[cs.Length];
            var used = new bool[cd.Length];
            for (int k = 0; k < cs.Length; k++)
            {
                var target = (turn * (cs[k] - a)).normalized;
                int best = -1;
                float bestDot = -2f;
                for (int j = 0; j < cd.Length; j++)
                {
                    if (used[j]) continue;
                    float d = Vector3.Dot(target, (cd[j] - b).normalized);
                    if (d > bestDot) { bestDot = d; best = j; }
                }
                used[best] = true;
                SourceOffsets[k] = cs[k] - a;
                SinkOffsets[k] = cd[best] - b;
            }
            bow = 0.16f + 0.3f * angle / Mathf.PI;
            sourceRadius = a.magnitude;
            sinkRadius = b.magnitude;
        }

        /// <summary>The centre line at <paramref name="u"/> (0 at the source, 1 at the sink).</summary>
        public Vector3 Centre(float u)
        {
            var dir = (sourceAxis * Mathf.Sin((1f - u) * angle) + sinkAxis * Mathf.Sin(u * angle)).normalized;
            return dir * ((sourceRadius + (sinkRadius - sourceRadius) * u) * (1f + bow * Mathf.Sin(Mathf.PI * u)));
        }

        /// <summary>Strand <paramref name="k"/> at <paramref name="u"/>: on the source's corner at 0, the centre line
        /// through the middle, the sink's corner at 1.</summary>
        public Vector3 Strand(int k, float u, float fork) =>
            Centre(u) + SourceOffsets[k] * (1f - ChargeArcs.Smooth(u / fork))
                      + SinkOffsets[k] * ChargeArcs.Smooth((u - (1f - fork)) / fork);
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
        }

        readonly ChargeSites sites;
        readonly System.Random random;
        readonly List<Leg> legs = new();
        readonly List<int> recent = new(4);
        readonly List<int> options = new(3);
        int at, previous = -1, serial;
        float end;

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
                legs.Add(new Leg { Seam = seam, Serial = serial++, Forward = forward, Start = end, Struck = struck });
                end = struck + Mathf.Lerp(settings.SeamDwellMin, settings.SeamDwellMax, (float)random.NextDouble()) * span;
                at = forward ? edge.B : edge.A;
                previous = seam;
                recent.Add(seam);
                if (recent.Count > 4) recent.RemoveAt(0);
            }
            int drop = 0;
            while (drop < legs.Count && seconds - legs[drop].Struck > settings.SeamFade * span) drop++;
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
    /// Writes the Charge light into one mesh, in the crystal's own space: camera-facing bolt strips (a halo and a body; a
    /// thick beam also a hot narrow core) and the lit plates' faces. TEXCOORD0 = (across the strip -1..1, falloff from
    /// the light's centre 0..1, kind: 0 strip / 1 face, intensity) - read by <c>CrystalChargeArcsShader</c>.
    /// Nothing is allocated per frame once the lists have grown.
    /// </summary>
    public sealed class ChargeArcMeshWriter
    {
        public readonly List<Vector3> Positions = new();
        public readonly List<Vector4> Data = new();
        public readonly List<int> Indices = new();
        readonly List<Vector3> kinked = new(64);

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
        /// One bolt along <paramref name="points"/>: each stretch cut into pieces about 0.07 radii long, each inner joint
        /// kicked sideways by a seeded amount (x <paramref name="jag"/> of the piece) that re-rolls with
        /// <paramref name="jagFrame"/>, then drawn as camera-facing strips <paramref name="width"/> body widths wide.
        /// </summary>
        public void Bolt(IReadOnlyList<Vector3> points, int count, float intensity, float jag, float width, int seed,
            Vector3 eye, float radius, float coreWidth, int jagFrame)
        {
            if (count < 2 || intensity <= 0.01f) return;
            kinked.Clear();
            kinked.Add(points[0]);
            for (int s = 0; s + 1 < count; s++)
            {
                Vector3 a = points[s], c = points[s + 1];
                float length = (c - a).magnitude;
                int pieces = Mathf.Clamp(Mathf.RoundToInt(length / (0.07f * radius)), 1, 10);
                var side = Vector3.Cross(c - a, eye - a).normalized;
                for (int k = 1; k <= pieces; k++)
                {
                    var v = Vector3.Lerp(a, c, (float)k / pieces);
                    if (k < pieces && jag > 0f)
                        v += side * ((ChargeArcs.Hash(seed * 131 + s * 17 + k, jagFrame) - 0.5f) * 2f * jag * length / pieces);
                    kinked.Add(v);
                }
            }
            float body = coreWidth * radius * width;
            for (int k = 0; k + 1 < kinked.Count; k++)
            {
                Vector3 a = kinked[k], c = kinked[k + 1];
                var side = Vector3.Cross(c - a, eye - (a + c) * 0.5f).normalized;
                if (width >= 2.5f)
                {
                    // a thick beam: a soft halo, a body and a hot narrow core (a tube under bloom)
                    Strip(a, c, side * (body * 1.9f), intensity * 0.14f);
                    Strip(a, c, side * body, intensity * 0.55f);
                    Strip(a, c, side * (body * 0.32f), intensity * 1.5f);
                }
                else
                {
                    // the halo is narrower on wider bolts, or its short strips fan out into fins at every kink
                    Strip(a, c, side * (body * Mathf.Max(1.8f, 4f - 0.8f * (width - 1f))), intensity * 0.22f);
                    Strip(a, c, side * body, intensity * 1.2f);
                }
            }
        }

        void Strip(Vector3 a, Vector3 c, Vector3 side, float intensity)
        {
            int i = Positions.Count;
            Positions.Add(a + side); Data.Add(new Vector4(1f, 0f, 0f, intensity));
            Positions.Add(a - side); Data.Add(new Vector4(-1f, 0f, 0f, intensity));
            Positions.Add(c + side); Data.Add(new Vector4(1f, 0f, 0f, intensity));
            Positions.Add(c - side); Data.Add(new Vector4(-1f, 0f, 0f, intensity));
            Indices.Add(i); Indices.Add(i + 1); Indices.Add(i + 2);
            Indices.Add(i + 2); Indices.Add(i + 1); Indices.Add(i + 3);
        }
    }

    /// <summary>
    /// The two Charge effects the Omni Motion Lab picked (round 5), written frame by frame into a
    /// <see cref="ChargeArcMeshWriter"/>:
    ///
    ///   SEAM WALK (the Charge crystal) - <see cref="ChargeSeamWalker"/>'s bolt, and each petal lit by how near the bolt
    ///   runs to its short sides and how fresh that stretch of bolt is.
    ///   FLUX (the omni) - a pentagon charges (only it lights), an arc leaps to a pentagon 55-125 degrees away, which
    ///   lights as it takes the discharge; both fade, and the next discharge leaps on from the sink. At each end the arc
    ///   forks smoothly onto the pentagon's five corners; through the middle it is one thick beam.
    ///
    /// JS twin: <c>seamWalkFx()</c>, <c>seamLight()</c> and <c>fluxFx()</c> in <c>Docs/Studios/OmniMotionLab.html</c>.
    /// </summary>
    public static class ChargeArcs
    {
        const int SeamPointsPerLeg = 9;
        const int FluxSteps = 24;

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

        static readonly List<Vector3> s_points = new(FluxSteps + 1);
        static readonly List<Vector4> s_lit = new(64);   // (point, life)

        /// <summary>The seam walk at <paramref name="seconds"/>: its bolts, and its petal light into
        /// <paramref name="faceLight"/> (one per face). Returns the number of bolts drawn.</summary>
        public static int SeamWalk(ChargeSites sites, ChargeSeamWalker walker, float seconds, in ChargeArcSettings settings,
            ChargeArcMeshWriter writer, Vector3 eye, float[] faceLight)
        {
            System.Array.Clear(faceLight, 0, faceLight.Length);
            float span = settings.Span, fade = settings.SeamFade * span;
            int flicker = Mathf.FloorToInt(seconds * settings.FlickerHz), jagFrame = Mathf.FloorToInt(seconds * settings.JagHz);
            int bolts = 0;
            s_lit.Clear();
            var legs = walker.Legs;
            for (int l = legs.Count - 1; l >= 0; l--)
            {
                var leg = legs[l];
                if (leg.Start > seconds || seconds - leg.Struck > fade) continue;
                var pts = sites.Seams[leg.Seam].Points;
                int n = pts.Length - 1;
                float reach = Mathf.Clamp01((seconds - leg.Start) / Mathf.Max(1e-6f, leg.Struck - leg.Start)) * n;
                float life = 1f - Smooth(Mathf.Max(0f, seconds - leg.Struck) / fade);
                for (int i = 0; i < n && i < reach; i++)
                {
                    var a = pts[leg.Forward ? i : n - i];
                    var next = pts[leg.Forward ? i + 1 : n - i - 1];
                    var b = i + 1 <= reach ? next : Vector3.Lerp(a, next, reach - i);
                    s_points.Clear();
                    s_points.Add(a);
                    s_points.Add(b);
                    float intensity = settings.Bolts * 2.4f * life * (0.55f + 0.45f * Hash(leg.Serial * 31 + i, flicker));
                    writer.Bolt(s_points, 2, intensity, 0.4f, 2f, leg.Serial * 977 + i, eye, sites.Radius, settings.CoreWidth, jagFrame);
                    s_lit.Add(new Vector4(b.x, b.y, b.z, life));
                    bolts++;
                }
            }
            // a petal glows by how near the bolt runs to its short sides, and how fresh that stretch of bolt is
            for (int f = 0; f < sites.Faces.Length; f++)
            {
                var face = sites.Faces[f];
                float g = 0f;
                foreach (var lit in s_lit)
                {
                    var q = (Vector3)lit;
                    foreach (var back in face.Back)
                    {
                        float d = (back - q).magnitude / face.Sigma;
                        g = Mathf.Max(g, lit.w * Mathf.Exp(-d * d));
                    }
                }
                if (g > 0.01f) faceLight[f] = settings.Glow * 2.4f * g;
            }
            return bolts;
        }

        /// <summary>The flux arcs at <paramref name="seconds"/>: their bolts, and the source and sink light into
        /// <paramref name="faceLight"/>. Returns the number of discharges drawn.</summary>
        public static int Flux(ChargeSites sites, ChargeFluxChain chain, float seconds, in ChargeArcSettings settings,
            ChargeArcMeshWriter writer, Vector3 eye, float[] faceLight)
        {
            System.Array.Clear(faceLight, 0, faceLight.Length);
            if (seconds < 0f) return 0;
            float span = settings.Span, gap = settings.FluxGap * span, fork = settings.FluxFork;
            float charge = settings.FluxCharge * span, struck = charge + settings.FluxStrike * span;
            float held = struck + settings.FluxHold * span, gone = held + settings.FluxFade * span;
            int flicker = Mathf.FloorToInt(seconds * settings.FlickerHz), jagFrame = Mathf.FloorToInt(seconds * settings.JagHz);
            long now = (long)Mathf.Floor(seconds / gap);
            int drawn = 0;
            for (long k = now; k >= 0 && k > now - 4; k--)
            {
                float t = seconds - k * gap;
                if (t > gone || !chain.TryGet(k, settings, out var discharge)) continue;
                float fading = 1f - Smooth((t - held) / (gone - held));
                // the source charges, flares as it discharges, fades; the sink lights as the arc lands
                Light(sites, faceLight, discharge.Source,
                    settings.Glow * (t < charge ? 1.4f * Smooth(t / charge) : t < held ? 2.2f : 2.2f * fading));
                if (t < charge) continue;
                Light(sites, faceLight, discharge.Sink,
                    settings.Glow * (t < held ? 2.4f * Smooth((t - charge) / (struck - charge)) : 2.4f * fading));
                float grow = Mathf.Clamp01((t - charge) / (struck - charge));
                float intensity = settings.Bolts * (t < held ? 1f : fading) * (0.75f + 0.25f * Hash((int)k, flicker));
                if (intensity <= 0.01f) continue;
                var path = sites.PathOf(discharge.Source, discharge.Sink);
                int upto = Mathf.RoundToInt(grow * FluxSteps);
                int seed = (int)(k * 53);
                // five strands fork onto the corners at each end...
                for (int q = 0; q < path.SourceOffsets.Length; q++)
                {
                    s_points.Clear();
                    for (int i = 0; i <= upto; i++)
                    {
                        float u = (float)i / FluxSteps;
                        if (u <= fork + 0.06f) s_points.Add(path.Strand(q, u, fork));
                    }
                    writer.Bolt(s_points, s_points.Count, intensity * 1.1f, 0.22f, 0.9f, seed + q, eye, sites.Radius, settings.CoreWidth, jagFrame);
                    s_points.Clear();
                    for (int i = 0; i <= upto; i++)
                    {
                        float u = (float)i / FluxSteps;
                        if (u >= 1f - fork - 0.06f) s_points.Add(path.Strand(q, u, fork));
                    }
                    writer.Bolt(s_points, s_points.Count, intensity * 1.1f, 0.22f, 0.9f, seed + 7 + q, eye, sites.Radius, settings.CoreWidth, jagFrame);
                }
                // ...and are one thick beam between
                s_points.Clear();
                for (int i = 0; i <= upto; i++)
                {
                    float u = (float)i / FluxSteps;
                    if (u >= fork - 0.04f && u <= 1f - fork + 0.04f) s_points.Add(path.Centre(u));
                }
                writer.Bolt(s_points, s_points.Count, intensity * 1.6f, 0.3f, 3f, seed + 31, eye, sites.Radius, settings.CoreWidth, jagFrame);
                drawn++;
            }
            return drawn;
        }

        static void Light(ChargeSites sites, float[] faceLight, int site, float value)
        {
            foreach (int f in sites.FacesOfSite[site]) faceLight[f] = Mathf.Max(faceLight[f], value);
        }
    }
}
