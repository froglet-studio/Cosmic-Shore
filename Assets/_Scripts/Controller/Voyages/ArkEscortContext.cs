using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// What every escort seat shares on one Ark escort (<see cref="ArkEscortRun"/> owns one per run): the hostile
    /// creatures near the Ark <b>ranked by threat</b> (nearness to the hull, closing speed, grazing reach - not plain
    /// distance), seat claims with hysteresis so a squad spreads over the threats and nobody flaps between two, the
    /// <b>lane-clear</b> safety test every weapon is gated on (a shot's path must miss the hull), and the crystal
    /// sites the Scarab may ring. Ticked once a frame by the run.
    /// </summary>
    public sealed class ArkEscortContext
    {
        const float GrazeReach = 60f;
        const float ClosingNormaliser = 60f;   // u/s of closing speed that counts as "full" urgency

        readonly ArkEscortRun _run;
        readonly List<Fauna> _threats = new();
        readonly Dictionary<Fauna, float> _scores = new();
        readonly Dictionary<Fauna, (Vector3 pos, float t)> _seen = new();
        readonly Dictionary<int, Fauna> _claims = new();
        readonly List<Fauna> _stale = new();
        float _nextRank;
        float _rankedAt;
        Vector3 _centroid;

        /// <summary>Where creatures have been and free crystals lie: the Scarab's ring sites.</summary>
        public readonly ArkEscortCrystalSites Sites = new();

        public ArkEscortContext(ArkEscortRun run)
        {
            _run = run;
            EscortGeneRegistry.EnsureDefined();
        }

        public ArkEscortRun Run => _run;
        public Ark Ark => _run ? _run.Ark : null;
        public Cell Cell => _run ? _run.CurrentCell : null;
        public Domains Domain => Ark ? Ark.Domain : Domains.Blue;

        /// <summary>Half the hull's length: the sphere every lane test keeps clear (plus the lane margin).</summary>
        public float HullRadius => _run ? _run.HullHalfLength : 55f;

        /// <summary>The live threats, most urgent first (re-ranked every retargetSeconds).</summary>
        public IReadOnlyList<Fauna> Threats => _threats;
        public bool HasThreats => _threats.Count > 0;
        public Vector3 ThreatCentroid => _centroid;

        /// <summary>Install the mode-wide hooks (the Scarab's ring-site resolver). Paired with <see cref="Uninstall"/>.</summary>
        static readonly PlaceSwitchActionExecutor.SwitchPlacementResolver s_resolver = ArkEscortCrystalSites.Resolve;

        public void Install()
        {
            ArkEscortCrystalSites.Current = Sites;
            PlaceSwitchActionExecutor.PlacementResolver = s_resolver;
        }

        public void Uninstall()
        {
            if (ArkEscortCrystalSites.Current == Sites) ArkEscortCrystalSites.Current = null;
            if (PlaceSwitchActionExecutor.PlacementResolver == s_resolver)
                PlaceSwitchActionExecutor.PlacementResolver = null;
            _claims.Clear();
            _threats.Clear();
        }

        public void Tick()
        {
            var ark = Ark;
            if (!ark) return;
            if (Time.time >= _nextRank) Rank(ark);
            Sites.Sample(this);
        }

        // ── Threat ranking ───────────────────────────────────────────────────

        void Rank(Ark ark)
        {
            float now = Time.time;
            float dt = _rankedAt > 0f ? Mathf.Max(0.05f, now - _rankedAt) : 0f;
            _nextRank = now + EscortGeneRegistry.RetargetSeconds;
            _rankedAt = now;

            float guard = EscortGeneRegistry.GuardRadius;
            _run.CountThreats(ark.Position, guard, _threats);
            _scores.Clear();
            _claims.Clear();

            float wDist = EscortGeneRegistry.ThreatWeightDistance;
            float wClose = EscortGeneRegistry.ThreatWeightClosing;
            float hull = HullRadius;
            Vector3 sum = Vector3.zero;
            foreach (var f in _threats)
            {
                Vector3 pos = f.transform.position;
                float d = Mathf.Max(0f, (pos - ark.Position).magnitude - hull);
                float score = wDist * (1f - Mathf.Clamp01(d / Mathf.Max(1f, guard)));
                if (dt > 0f && _seen.TryGetValue(f, out var prev))
                {
                    float prevD = Mathf.Max(0f, (prev.pos - ark.Position).magnitude - hull);
                    float closing = (prevD - d) / Mathf.Max(0.05f, now - prev.t);
                    score += wClose * Mathf.Clamp01(closing / ClosingNormaliser);
                }
                if (d <= GrazeReach) score += 0.5f;
                _scores[f] = score;
                _seen[f] = (pos, now);
                sum += pos;
            }
            _centroid = _threats.Count > 0 ? sum / _threats.Count : ark.Position;
            _threats.Sort((a, b) => _scores[b].CompareTo(_scores[a]));

            // Forget creatures no longer in reach, so the history does not grow with the world.
            _stale.Clear();
            foreach (var kv in _seen)
                if (!kv.Key || !_scores.ContainsKey(kv.Key)) _stale.Add(kv.Key);
            foreach (var f in _stale) _seen.Remove(f);
        }

        public float ScoreOf(Fauna f) => f && _scores.TryGetValue(f, out float s) ? s : float.NegativeInfinity;

        public bool IsThreat(Fauna f) => f && f.IsAlivePrey && _scores.ContainsKey(f);

        /// <summary>
        /// The threat for <paramref name="seat"/>: its <paramref name="current"/> one while that is still a threat no
        /// other seat holds and no free threat beats it by the retarget margin; else the most urgent free one; with
        /// every threat taken, the seat's share of the ranking. Null with no threats.
        /// </summary>
        public Fauna Claim(int seat, Fauna current)
        {
            if (_threats.Count == 0) return null;
            float top = _scores[_threats[0]];
            float margin = EscortGeneRegistry.RetargetMargin * Mathf.Max(0.01f, Mathf.Abs(top));

            Fauna bestFree = null;
            for (int i = 0; i < _threats.Count; i++)
            {
                if (ClaimedByOther(_threats[i], seat)) continue;
                bestFree = _threats[i];
                break;
            }

            Fauna pick;
            if (IsThreat(current) && !ClaimedByOther(current, seat)
                && (bestFree == null || current == bestFree || ScoreOf(current) >= ScoreOf(bestFree) - margin))
                pick = current;
            else if (bestFree != null)
                pick = bestFree;
            else
                pick = _threats[Mathf.Abs(seat) % _threats.Count];

            _claims[seat] = pick;
            return pick;
        }

        bool ClaimedByOther(Fauna f, int seat)
        {
            foreach (var kv in _claims)
                if (kv.Key != seat && kv.Value == f) return true;
            return false;
        }

        // ── Geometry helpers ─────────────────────────────────────────────────

        /// <summary>A creature's heart (the joust surface and the fuze target), or its body.</summary>
        public static Vector3 HeartOf(Fauna f)
        {
            if (!f) return default;
            var heart = f.LivingHeart;
            return heart ? heart.transform.position : f.transform.position;
        }

        /// <summary>True when the segment <paramref name="from"/>-<paramref name="to"/> misses the Ark's hull sphere
        /// padded by the lane margin and <paramref name="extraPadding"/> (a blast radius).</summary>
        public bool LaneClear(Vector3 from, Vector3 to, float extraPadding = 0f)
        {
            var ark = Ark;
            if (!ark) return true;
            return SegmentClearsSphere(from, to, ark.Position, HullRadius + EscortGeneRegistry.ArkLaneMargin + extraPadding);
        }

        /// <summary>True when no point of segment a-b lies within <paramref name="radius"/> of <paramref name="centre"/>.</summary>
        public static bool SegmentClearsSphere(Vector3 a, Vector3 b, Vector3 centre, float radius)
        {
            Vector3 ab = b - a;
            float len2 = ab.sqrMagnitude;
            float t = len2 > 1e-6f ? Mathf.Clamp01(Vector3.Dot(centre - a, ab) / len2) : 0f;
            Vector3 nearest = a + ab * t;
            return (nearest - centre).sqrMagnitude > radius * radius;
        }

        /// <summary>The idle station for a seat: ahead of the bow on the seat's own side.</summary>
        public Vector3 Station(int seat, float radius)
        {
            var ark = Ark;
            if (!ark) return default;
            float angle = 120f * seat + 30f;
            var side = Quaternion.AngleAxis(angle, ark.Forward) * ark.transform.up;
            return ark.Position + ark.Forward * radius + side * radius;
        }

        /// <summary>A point <paramref name="standoff"/> short of <paramref name="at"/> on its Ark side: between the
        /// creature and the hull.</summary>
        public Vector3 ArkSideOf(Vector3 at, float standoff)
        {
            var ark = Ark;
            if (!ark) return at;
            Vector3 toArk = ark.Position - at;
            if (toArk.sqrMagnitude < 1e-4f) return at;
            return at + toArk.normalized * standoff;
        }
    }

    /// <summary>
    /// The escort's ring sites for the Scarab: a short memory of where live creature hearts have been and where free
    /// crystals lie (active and inactive elemental crystal locations), each stamped with when it was seen. The
    /// escort installs <see cref="Resolve"/> as the switch placement resolver, so a Scarab's ring lands only on such a
    /// site (or a plant's heart the press itself anchored to) and a press anywhere else is refused for free.
    /// Per-peer, not replicated: right for the single-host sim and solo play (see Docs/Voyages/ARK_ESCORT.md).
    /// </summary>
    public sealed class ArkEscortCrystalSites
    {
        const float SampleSeconds = 0.5f;

        public struct Site
        {
            public Vector3 Position;
            public float SeenAt;
        }

        readonly List<Site> _sites = new();
        float _nextSample;

        /// <summary>The live escort's sites, read by the static resolver.</summary>
        public static ArkEscortCrystalSites Current;

        public IReadOnlyList<Site> Sites => _sites;

        public void Sample(ArkEscortContext ctx)
        {
            float now = Time.time;
            if (now < _nextSample) return;
            _nextSample = now + SampleSeconds;
            float maxAge = EscortGeneRegistry.ScarabSiteMaxAge;
            for (int i = _sites.Count - 1; i >= 0; i--)
                if (now - _sites[i].SeenAt > maxAge) _sites.RemoveAt(i);

            var ark = ctx.Ark;
            if (!ark) return;
            float reach = EscortGeneRegistry.GuardRadius;
            float reach2 = reach * reach;
            foreach (var f in ctx.Threats)
            {
                var heart = f ? f.LivingHeart : null;
                if (!heart) continue;
                Add(heart.transform.position, now);
            }
            var cell = ctx.Cell;
            if (cell && cell.RuntimeData)
            {
                foreach (var item in cell.RuntimeData.CellItems)
                {
                    if (item is not Crystal c || !c || c.IsEmbedded) continue;
                    if ((c.transform.position - ark.Position).sqrMagnitude > reach2) continue;
                    Add(c.transform.position, now);
                }
            }
        }

        void Add(Vector3 at, float now)
        {
            // Merge with a site already within a ring's width, keeping it fresh.
            for (int i = 0; i < _sites.Count; i++)
            {
                if ((_sites[i].Position - at).sqrMagnitude > 20f * 20f) continue;
                _sites[i] = new Site { Position = at, SeenAt = now };
                return;
            }
            _sites.Add(new Site { Position = at, SeenAt = now });
        }

        /// <summary>The site nearest <paramref name="point"/> within <paramref name="reach"/>, if any.</summary>
        public bool TryNearest(Vector3 point, float reach, out Vector3 site)
        {
            site = default;
            float best = reach * reach;
            bool any = false;
            foreach (var s in _sites)
            {
                float d2 = (s.Position - point).sqrMagnitude;
                if (d2 > best) continue;
                best = d2;
                site = s.Position;
                any = true;
            }
            return any;
        }

        /// <summary>The site that best suits a ring guarding the Ark: within <paramref name="standoff"/> of the Ark,
        /// nearest the line from the Ark toward <paramref name="toward"/> (the threats).</summary>
        public bool TryBestFor(Vector3 arkPos, Vector3 toward, float standoff, out Vector3 site)
        {
            site = default;
            Vector3 dir = toward - arkPos;
            bool hasDir = dir.sqrMagnitude > 1e-3f;
            if (hasDir) dir.Normalize();
            float best = float.MaxValue;
            bool any = false;
            foreach (var s in _sites)
            {
                Vector3 rel = s.Position - arkPos;
                float d = rel.magnitude;
                if (d > standoff) continue;
                float cost = hasDir ? (rel - dir * Mathf.Max(0f, Vector3.Dot(rel, dir))).magnitude : d;
                if (cost >= best) continue;
                best = cost;
                site = s.Position;
                any = true;
            }
            return any;
        }

        /// <summary>The switch placement resolver: a plant-anchored ring stands; a free ring snaps to the nearest
        /// site within siteReach of where it would have gone, or is refused.</summary>
        public static bool Resolve(IVesselStatus status, Vector3 requested, bool anchored, out Vector3 resolved)
        {
            resolved = requested;
            if (anchored) return true;
            var sites = Current;
            if (sites == null) return false;
            return sites.TryNearest(requested, EscortGeneRegistry.ScarabSiteReach, out resolved);
        }
    }
}
