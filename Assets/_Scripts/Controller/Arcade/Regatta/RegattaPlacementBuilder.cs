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
        /// scaled finer than <see cref="RegattaPlacementBuilder.MinPrismAxis"/>).</summary>
        public int[] Carved;
        /// <summary>Non-null when the spawner could not be generated at all.</summary>
        public string Failure;

        public override string ToString() =>
            Failure != null
                ? $"{Kind}: FAILED ({Failure})"
                : $"{Kind}: {Laid} laid of {Native} (thinned {Thinned}, carved line {Carved[1]} ring {Carved[2]} " +
                  $"rail {Carved[3]} shell {Carved[4]} overlap {Carved[5]} too fine {Carved[6]})";
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

        /// <summary>The native layout's centre (the middle of its bounding box) and bounding radius (the farthest prism
        /// centre plus that prism's own half-length).</summary>
        public static void Bounds(List<PrismLay> native, out Vector3 centre, out float radius)
        {
            centre = Vector3.zero;
            radius = 0f;
            if (native == null || native.Count == 0) return;
            Vector3 lo = native[0].Point.Position, hi = lo;
            for (int i = 1; i < native.Count; i++)
            {
                lo = Vector3.Min(lo, native[i].Point.Position);
                hi = Vector3.Max(hi, native[i].Point.Position);
            }
            centre = (lo + hi) * 0.5f;
            for (int i = 0; i < native.Count; i++)
                radius = Mathf.Max(radius, (native[i].Point.Position - centre).magnitude
                                           + RegattaPlacementCarve.RadiusOf(native[i].Point.Scale));
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
            var report = new RegattaPlacementReport { Kind = p.Kind, Native = native?.Count ?? 0, Carved = new int[7] };
            if (native == null || native.Count == 0 || cap <= 0) { report.Thinned = report.Native; return report; }

            Bounds(native, out var c0, out float r0);
            float k = r0 > 1e-4f ? p.Scale / r0 : 1f;
            int stride = Mathf.Max(1, Mathf.CeilToInt(native.Count / (float)cap));
            int domainIndex = Array.IndexOf(RegattaCourseGenome.PlacementDomains, p.Domain);
            int prismIndex = Array.IndexOf(RegattaCourseGenome.PlacementPrisms, p.Prism);
            Domains? domain = domainIndex > 0
                ? (Domains)Enum.Parse(typeof(Domains), RegattaCourseGenome.PlacementDomains[domainIndex])
                : (Domains?)null;
            PrismKind? kind = prismIndex > 0
                ? (PrismKind)Enum.Parse(typeof(PrismKind), RegattaCourseGenome.PlacementPrisms[prismIndex])
                : (PrismKind?)null;

            var laid = new List<(Vector3 pos, float r)>();
            for (int i = 0; i < native.Count; i++)
            {
                if (i % stride != 0) { report.Thinned++; continue; }
                var lay = native[i];
                Vector3 pos = centre + rotation * ((lay.Point.Position - c0) * k);
                Vector3 scale = lay.Point.Scale * k;
                if (Mathf.Min(Mathf.Abs(scale.x), Mathf.Min(Mathf.Abs(scale.y), Mathf.Abs(scale.z))) < MinPrismAxis)
                {
                    report.Carved[(int)RegattaPlacementCarve.Verdict.Fine]++;
                    continue;
                }
                float r = RegattaPlacementCarve.RadiusOf(scale);
                var verdict = carve?.Test(pos, r) ?? RegattaPlacementCarve.Verdict.Laid;
                if (verdict != RegattaPlacementCarve.Verdict.Laid) { report.Carved[(int)verdict]++; continue; }
                output.Add(new PrismLay(new SpawnPoint(pos, rotation * lay.Point.Rotation, scale),
                                        domain ?? lay.Domain, kind ?? lay.Kind));
                laid.Add((pos, r));
            }
            // Claimed after the whole placement is laid: a spawner's own prisms keep whatever spacing it authored.
            foreach (var (pos, r) in laid) carve?.Claim(pos, r);
            report.Laid = laid.Count;
            return report;
        }

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
            int budget = RegattaCourseGenome.TotalPrismBudget;
            var native = new List<PrismLay>(4096);
            foreach (var p in genome.Placements)
            {
                if (!catalogue.TryGet(p.Kind, out var kind) || !kind.Usable)
                {
                    reports.Add(new RegattaPlacementReport { Kind = p.Kind, Carved = new int[7], Failure = "not a placeable kind" });
                    continue;
                }
                native.Clear();
                if (!TryGenerateNative(kind, p, native, out var failure))
                {
                    reports.Add(new RegattaPlacementReport { Kind = p.Kind, Carved = new int[7], Failure = failure });
                    CSDebug.LogWarning($"[Regatta] Course genome placement '{p.Kind}' could not be generated ({failure}); " +
                                       "it is left out. Re-run Tools/Build/regatta_gene_catalogue.py --measure to mark it unusable.");
                    continue;
                }
                RegattaCourseGenome.PlacementPose(gates, p, out var centre, out var rotation);
                int before = output.Count;
                var report = Place(native, p, centre, rotation, Mathf.Min(RegattaCourseGenome.PerPlacementCap, budget),
                                   carve, output);
                budget -= output.Count - before;
                reports.Add(report);
            }
            return reports;
        }
    }
}
