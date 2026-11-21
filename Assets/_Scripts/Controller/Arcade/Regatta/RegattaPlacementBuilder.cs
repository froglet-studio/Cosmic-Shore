using System;
using System.Collections.Generic;
using System.Reflection;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>What happened to one placement when the arena laid it (for the race judge's and the search's logs).</summary>
    public struct RegattaPlacementReport
    {
        public string Kind;
        /// <summary>Prisms the spawner generated, before thinning and carving.</summary>
        public int Native;
        /// <summary>Prisms laid.</summary>
        public int Laid;
        /// <summary>Prisms removed by an even stride to keep the placement and the course under their caps.</summary>
        public int Thinned;
        /// <summary>Prisms carved, by rule (index = <see cref="RegattaPlacementCarve.Verdict"/>; 0 is unused, 6 is a prism
        /// scaled finer than <see cref="RegattaPlacementBuilder.MinPrismAxis"/>, 7 a prism in a sight tube).</summary>
        public int[] Carved;
        /// <summary>Non-null when the spawner could not be generated at all.</summary>
        public string Failure;

        public override string ToString() =>
            Failure != null
                ? $"{Kind}: FAILED ({Failure})"
                : $"{Kind}: {Laid} laid of {Native} (thinned {Thinned}, carved line {Carved[1]} ring {Carved[2]} " +
                  $"rail {Carved[3]} shell {Carved[4]} overlap {Carved[5]} too fine {Carved[6]} sight {Carved[7]})";
    }

    /// <summary>
    /// Lays a course genome's placements: each one's segment spawner is generated through the spawner's OWN code (a
    /// component on a hidden, inactive GameObject, its serialized fields set from the genome, its layout read through
    /// <see cref="SpawnableBase.GetTrailData"/>, never a bake), scaled uniformly to the placement's bounding radius,
    /// posed in the spine frame, recoloured and re-kinded if the genome says so, thinned to the caps, and carved by
    /// <see cref="RegattaPlacementCarve"/>. The arena (<see cref="SpawnableRegattaRails"/>) emits what survives as its
    /// own prisms, so Unity and Amoebius lay a genome the same way.
    /// </summary>
    public static class RegattaPlacementBuilder
    {
        /// <summary>A placement prism scaled finer than this on any axis (u) is left out: the prism scale animator floors
        /// an axis at 0.5 u, so a finer one would be drawn larger than laid and clip its neighbours.</summary>
        public const float MinPrismAxis = 0.5f;

        /// <summary>Generate a catalogue kind with the placement's parameters, at its native size and pose.</summary>
        public static bool TryGenerateNative(RegattaGeneCatalogue.Kind kind, RegattaPlacement p, List<PrismLay> into,
                                             out string failure)
        {
            failure = null;
            var type = ResolveType(kind.Type);
            if (type == null || !typeof(SpawnableBase).IsAssignableFrom(type))
            {
                failure = $"no SpawnableBase type '{kind.Type}'";
                return false;
            }

            var go = new GameObject($"RegattaPlacement {kind.Id}");
            go.SetActive(false);
            try
            {
                var spawnable = (SpawnableBase)go.AddComponent(type);
                SetField(spawnable, "seed", p.Seed != 0 ? p.Seed : StableSeed(kind.Id));
                if (kind.World) SetField(spawnable, "density", p.Density);
                foreach (var kv in p.Params)
                {
                    var prm = kind.FindParam(kv.Key);
                    if (prm != null) SetParam(spawnable, prm, kv.Value);
                }

                SpawnTrailData[] trails;
                using (PrismLayoutBake.Bypass())
                {
                    spawnable.InvalidateCache();
                    trails = spawnable.GetTrailData();
                }

                if (spawnable is CellEnvironmentSpawnableBase world && world.CachedLays != null)
                {
                    for (int i = 0; i < world.CachedLays.Count; i++) into.Add(world.CachedLays[i]);
                }
                else if (trails != null)
                {
                    foreach (var td in trails)
                        if (td?.Points != null)
                            foreach (var pt in td.Points)
                                into.Add(new PrismLay(pt, td.Domain, PrismKind.Plain));
                }
                return true;
            }
            catch (Exception e)
            {
                failure = $"{e.GetType().Name}: {e.Message}";
                return false;
            }
            finally
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(go);
                else UnityEngine.Object.DestroyImmediate(go);
            }
        }

        /// <summary>The seed a placement without one is generated from: the same for a kind on every machine.</summary>
        public static int StableSeed(string id)
        {
            unchecked
            {
                uint h = 2166136261u;
                foreach (char c in id ?? "") h = (h ^ c) * 16777619u;
                int s = (int)(h & 0x7fffffff);
                return s == 0 ? 1 : s;
            }
        }

        static readonly Dictionary<string, Type> TypeCache = new();

        static Type ResolveType(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (TypeCache.TryGetValue(name, out var t)) return t;
            t = typeof(SpawnableBase).Assembly.GetType(name);
            if (t == null)
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                    if ((t = asm.GetType(name)) != null) break;
            TypeCache[name] = t;
            return t;
        }

        static FieldInfo FindField(Type type, string name)
        {
            for (var t = type; t != null && t != typeof(MonoBehaviour); t = t.BaseType)
            {
                var f = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (f != null) return f;
            }
            return null;
        }

        static void SetField(object target, string name, object value)
        {
            var f = FindField(target.GetType(), name);
            if (f == null) return;
            f.SetValue(target, Convert.ChangeType(value, f.FieldType, System.Globalization.CultureInfo.InvariantCulture));
        }

        static void SetParam(object target, RegattaGeneCatalogue.Param prm, double value)
        {
            var f = FindField(target.GetType(), prm.Name);
            if (f == null) return;
            double v = prm.Clamp(value);
            var ft = f.FieldType;
            switch (prm.Type)
            {
                case RegattaGeneCatalogue.ParamType.Bool: f.SetValue(target, v >= 0.5); break;
                case RegattaGeneCatalogue.ParamType.Int: f.SetValue(target, (int)v); break;
                case RegattaGeneCatalogue.ParamType.Domain:
                    f.SetValue(target, Enum.Parse(typeof(Domains), RegattaCourseGenome.PlacementDomains[1 + (int)v]));
                    break;
                case RegattaGeneCatalogue.ParamType.Enum:
                    if (ft.IsEnum && (int)v < prm.Values.Count) f.SetValue(target, Enum.Parse(ft, prm.Values[(int)v]));
                    break;
                default:
                    f.SetValue(target, ft == typeof(int) ? (object)(int)Math.Round(v) : (float)v);
                    break;
            }
        }

        // ── The pure half: scale, pose, recolour, thin, carve ──────────────────────────────────────────────────

        /// <summary>The share of a layout's prisms its bounding radius holds. A spawner that projects (the Hopf
        /// fibration's stereographic projection, a pole near one of its points) can throw a few prisms hundreds of times
        /// farther out than the rest; sizing by the farthest one shrank the whole shape below the finest prism (it laid
        /// nothing). The outliers are scaled with the rest and the carve removes whatever lands out of bounds.</summary>
        public const float BoundsShare = 0.9f;

        /// <summary>The native layout's centre (the per-axis median of its prism centres) and bounding radius (the
        /// <see cref="BoundsShare"/> quantile of each prism's distance from it plus that prism's own half-length).</summary>
        static bool Finite(Vector3 v) => float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);

        public static void Bounds(List<PrismLay> native, out Vector3 centre, out float radius)
        {
            centre = Vector3.zero;
            radius = 0f;
            if (native == null || native.Count == 0) return;
            int n = native.Count;
            var axis = new float[n];
            for (int a = 0; a < 3; a++)
            {
                for (int i = 0; i < n; i++) axis[i] = native[i].Point.Position[a];
                Array.Sort(axis);
                centre[a] = (n & 1) == 1 ? axis[n / 2] : 0.5f * (axis[n / 2 - 1] + axis[n / 2]);
            }
            for (int i = 0; i < n; i++)
                axis[i] = (native[i].Point.Position - centre).magnitude + RegattaPlacementCarve.RadiusOf(native[i].Point.Scale);
            Array.Sort(axis);
            radius = axis[Mathf.Clamp(Mathf.CeilToInt(BoundsShare * n) - 1, 0, n - 1)];
        }

        /// <summary>
        /// Lay one placement's native prisms into <paramref name="output"/>: scaled so their bounding radius is the
        /// placement's <see cref="RegattaPlacement.Scale"/>, posed at <paramref name="centre"/> /
        /// <paramref name="rotation"/>, thinned by an even stride to <paramref name="cap"/>, carved, and claimed in the
        /// carve so later placements keep clear. Pure; returns the report.
        /// </summary>
        public static RegattaPlacementReport Place(List<PrismLay> native, RegattaPlacement p, Vector3 centre,
                                                   Quaternion rotation, int cap, RegattaPlacementCarve carve,
                                                   List<PrismLay> output)
        {
            var report = new RegattaPlacementReport { Kind = p.Kind, Native = native?.Count ?? 0, Carved = new int[8] };
            if (native == null || native.Count == 0 || cap <= 0) { report.Thinned = report.Native; return report; }

            Bounds(native, out var c0, out float r0);
            float k = r0 > 1e-4f ? p.Scale / r0 : 1f;
            // A Keep share thins first, so the stride only takes what the cap still needs.
            int stride = Mathf.Max(1, Mathf.CeilToInt(native.Count * Mathf.Clamp01(p.Keep) / (float)cap));
            int domainIndex = Array.IndexOf(RegattaCourseGenome.PlacementDomains, p.Domain);
            int prismIndex = Array.IndexOf(RegattaCourseGenome.PlacementPrisms, p.Prism);
            Domains? domain = domainIndex > 0
                ? (Domains)Enum.Parse(typeof(Domains), RegattaCourseGenome.PlacementDomains[domainIndex])
                : (Domains?)null;
            PrismKind? kind = prismIndex > 0
                ? (PrismKind)Enum.Parse(typeof(PrismKind), RegattaCourseGenome.PlacementPrisms[prismIndex])
                : (PrismKind?)null;

            var laid = new List<(Vector3 pos, float r)>();
            uint salt = (uint)(p.Seed != 0 ? p.Seed : StableSeed(p.Kind));
            for (int i = 0; i < native.Count; i++)
            {
                if (i % stride != 0) { report.Thinned++; continue; }
                if (p.Keep < 1f && Draw(salt, i, 0) >= p.Keep) { report.Thinned++; continue; }
                if (laid.Count >= cap) { report.Thinned++; continue; }
                var lay = native[i];
                Vector3 pos = centre + rotation * ((lay.Point.Position - c0) * k);
                if (p.Warp > 0f) pos += WarpAt(pos, p.Warp, p.WarpWavelength, salt);
                Vector3 scale = lay.Point.Scale * (k * p.PrismSize);
                Quaternion turn = p.Jitter > 0f
                    ? Quaternion.AngleAxis((Draw(salt, i, 1) * 2f - 1f) * p.Jitter,
                                           RaceCourseGeometry.SafeNormalize(new Vector3(Draw(salt, i, 2) - 0.5f,
                                               Draw(salt, i, 3) - 0.5f, Draw(salt, i, 4) - 0.5f), Vector3.up))
                    : Quaternion.identity;
                var layDomain = domain ?? (p.DomainMix != null ? MixDomain(p.DomainMix, Draw(salt, i, 5)) : lay.Domain);
                var layKind = kind ?? (p.PrismMix != null ? MixKind(p.PrismMix, Draw(salt, i, 6)) : lay.Kind);
                if (!Finite(pos) || !Finite(scale))
                {
                    // A spawner that lays a prism at infinity has a bug of its own; the course never takes it.
                    report.Carved[(int)RegattaPlacementCarve.Verdict.Shell]++;
                    continue;
                }
                if (Mathf.Min(Mathf.Abs(scale.x), Mathf.Min(Mathf.Abs(scale.y), Mathf.Abs(scale.z))) < MinPrismAxis)
                {
                    report.Carved[(int)RegattaPlacementCarve.Verdict.Fine]++;
                    continue;
                }
                float r = RegattaPlacementCarve.RadiusOf(scale);
                var verdict = carve?.Test(pos, r, p.CrossLine, p.CrossRail) ?? RegattaPlacementCarve.Verdict.Laid;
                if (verdict != RegattaPlacementCarve.Verdict.Laid) { report.Carved[(int)verdict]++; continue; }
                output.Add(new PrismLay(new SpawnPoint(pos, turn * rotation * lay.Point.Rotation, scale), layDomain, layKind));
                laid.Add((pos, r));
            }
            // Claimed after the whole placement is laid: a spawner's own prisms keep whatever spacing it authored.
            foreach (var (pos, r) in laid) carve?.Claim(pos, r);
            report.Laid = laid.Count;
            return report;
        }

        /// <summary>A seeded draw in [0, 1) for prism <paramref name="i"/>, channel <paramref name="ch"/> (pure, the
        /// same on every machine).</summary>
        public static float Draw(uint salt, int i, int ch)
        {
            unchecked
            {
                uint h = salt * 0x9E3779B1u ^ (uint)i * 0x85EBCA77u ^ (uint)(ch + 1) * 0xC2B2AE3Du;
                h ^= h >> 16; h *= 0x7FEB352Du; h ^= h >> 15; h *= 0x846CA68Bu; h ^= h >> 16;
                return (h >> 8) * (1f / 16777216f);
            }
        }

        /// <summary>The smooth displacement a warped placement bends a prism by: three crossed sine waves of one
        /// wavelength, phased by the placement's seed.</summary>
        public static Vector3 WarpAt(Vector3 p, float amplitude, float wavelength, uint salt)
        {
            float w = 2f * Mathf.PI / Mathf.Max(1f, wavelength);
            float a = Draw(salt, 0, 10) * 6.2832f, b = Draw(salt, 0, 11) * 6.2832f, c = Draw(salt, 0, 12) * 6.2832f;
            return amplitude * new Vector3(Mathf.Sin(w * p.y + a) * Mathf.Cos(w * p.z + b),
                                           Mathf.Sin(w * p.z + b) * Mathf.Cos(w * p.x + c),
                                           Mathf.Sin(w * p.x + c) * Mathf.Cos(w * p.y + a));
        }

        static readonly PrismKind[] MixKinds = { PrismKind.Plain, PrismKind.Shielded, PrismKind.SuperShielded, PrismKind.Danger };
        static readonly Domains[] MixDomains = { Domains.Jade, Domains.Ruby, Domains.Gold, Domains.Blue };

        static int Pick(float[] weights, float u)
        {
            float acc = 0f;
            for (int i = 0; i < weights.Length; i++) { acc += weights[i]; if (u < acc) return i; }
            return weights.Length - 1;
        }

        static PrismKind MixKind(float[] mix, float u) => MixKinds[Mathf.Clamp(Pick(mix, u), 0, 3)];
        static Domains MixDomain(float[] mix, float u) => MixDomains[Mathf.Clamp(Pick(mix, u), 0, 3)];

        /// <summary>
        /// Lay every placement of <paramref name="genome"/> (already repaired) into <paramref name="output"/>, in order,
        /// under <see cref="RegattaCourseGenome.PerPlacementCap"/> and <see cref="RegattaCourseGenome.TotalPrismBudget"/>.
        /// </summary>
        public static List<RegattaPlacementReport> LayAll(RegattaCourseGenome genome, RegattaGeneCatalogue catalogue,
                                                          IReadOnlyList<RaceGate> gates, RegattaPlacementCarve carve,
                                                          List<PrismLay> output)
        {
            var reports = new List<RegattaPlacementReport>();
            if (genome?.Placements == null || catalogue == null) return reports;
            int budget = RegattaCourseGenome.TotalPrismBudget, baseBudget = RegattaCourseGenome.BasePrismBudget;
            var native = new List<PrismLay>(4096);
            foreach (var p in genome.Placements)
            {
                if (!catalogue.TryGet(p.Kind, out var kind) || !kind.Usable)
                {
                    reports.Add(new RegattaPlacementReport { Kind = p.Kind, Carved = new int[8], Failure = "not a placeable kind" });
                    continue;
                }
                native.Clear();
                if (!TryGenerateNative(kind, p, native, out var failure))
                {
                    reports.Add(new RegattaPlacementReport { Kind = p.Kind, Carved = new int[8], Failure = failure });
                    CSDebug.LogWarning($"[Regatta] Course genome placement '{p.Kind}' could not be generated ({failure}); " +
                                       "it is left out. Re-run Tools/Build/regatta_gene_catalogue.py --measure to mark it unusable.");
                    continue;
                }
                RegattaCourseGenome.PlacementPose(gates, p, out var centre, out var rotation);
                int before = output.Count;
                int cap = p.Base ? baseBudget : Mathf.Min(RegattaCourseGenome.PerPlacementCap, budget);
                var report = Place(native, p, centre, rotation, cap, carve, output);
                if (p.Base) baseBudget -= output.Count - before;
                else budget -= output.Count - before;
                reports.Add(report);
            }
            return reports;
        }
    }
}
