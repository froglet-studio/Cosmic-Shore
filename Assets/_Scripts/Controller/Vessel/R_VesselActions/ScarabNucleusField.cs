using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The server-side book for one CELL's nucleus ball field — the Scarab's seeding ability
    /// (design: R_VesselActions/SCARAB.md §4.6). It owns three things the individual ball cannot:
    /// which balls are studding this nucleus, which have been banked INSIDE it, and what happens
    /// when one too many goes in.
    ///
    /// THE LOOP, and why each half exists:
    ///   • Each Scarab passively plants a ball of its own domain in the nucleus surface
    ///     (<see cref="TrySeed"/>). It is already ownership-locked by the forge, so it is that
    ///     pilot's ball and an enemy must dash-STEAL it like any other.
    ///   • Struck OUTWARD it flies into the CYTOPLASM and lives there, bouncing off the nucleus
    ///     from the outside and the membrane from the inside — the same nucleus surface the court
    ///     ball bounces off from within. Deliberately inconsequential: a toy, not a scoring path.
    ///   • Struck INWARD it enters the NUCLEUS, which in Scarab Scramble is the court — so it
    ///     becomes a ball of consequence, a second source of them alongside the crystal forge.
    ///   • Bank one too many and the nucleus OVERLOADS: every ball detonates with an explosion
    ///     twice its own radius. Feeding the core is the greedy line, and the greedy line has a
    ///     cliff.
    ///
    /// One instance per Cell, created on demand by <see cref="ForCell"/> and living on the Cell's
    /// own GameObject — not a singleton, because "the nucleus" is a property of a cell and a
    /// session can hold more than one. It exists only where a Scarab has actually seeded, so a
    /// cell nothing has flown in carries no bookkeeping at all.
    ///
    /// SERVER ONLY. Every mutation here is a server write to replicated ball state, so clients need
    /// no counterpart: they see embedded balls, ejections, entries and the detonation purely as the
    /// ball variables and RPCs they already consume.
    /// </summary>
    public class ScarabNucleusField : MonoBehaviour
    {
        static readonly Dictionary<Cell, ScarabNucleusField> s_byCell = new();

        Cell _cell;
        ScarabNucleusFieldConfigSO _config;

        // Balls currently studding this nucleus, and balls banked inside it. Both are server-side
        // and both are swept of nulls on use — a ball can be despawned by a goal or a detonation at
        // any time, and neither list is worth a subscription to keep exact.
        readonly List<AstroLeagueBall> _embedded = new();
        readonly List<AstroLeagueBall> _inNucleus = new();

        static bool s_hooked;

        /// <summary>Get (or create) the field for <paramref name="cell"/>. Server-side callers only.</summary>
        public static ScarabNucleusField ForCell(Cell cell, ScarabNucleusFieldConfigSO config)
        {
            if (cell == null || config == null) return null;

            if (s_byCell.TryGetValue(cell, out var existing) && existing != null)
            {
                existing._config = config;
                return existing;
            }

            var field = cell.gameObject.AddComponent<ScarabNucleusField>();
            field._cell = cell;
            field._config = config;
            s_byCell[cell] = field;

            // ONE static subscription for every field: the ball raises release events without
            // knowing which cell it belongs to, so the handler routes by geometry (below).
            if (!s_hooked)
            {
                AstroLeagueBall.OnNucleusReleasedServer += HandleReleased;
                s_hooked = true;
            }
            return field;
        }

        void OnDestroy()
        {
            if (_cell != null) s_byCell.Remove(_cell);
        }

        /// <summary>The nucleus sphere this field studs: cell centre + the nucleus' VISUAL radius.</summary>
        public Vector3 Centre => _cell != null ? _cell.transform.position : Vector3.zero;

        /// <summary>
        /// The nucleus radius as GEOMETRY. Reads <c>NucleusVisualWorldRadius</c> rather than
        /// <c>NucleusWorldRadius</c> on purpose: the latter reports 0 whenever a mode has declared
        /// the nucleus play geometry instead of a territorial claim (<c>NucleusIsControlZone =
        /// false</c>, which Scarab Scramble and Astro League both set), and the ball needs the shape,
        /// not the claim. See Docs/ECOSYSTEM.md §25.1.
        /// </summary>
        public float NucleusRadius => _cell != null ? _cell.NucleusVisualWorldRadius : 0f;

        // ── Seeding ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Server: plant one ball of <paramref name="domain"/> in the nucleus surface. Returns null
        /// when the domain is already at its embedded cap, when the cell has no nucleus geometry, or
        /// when the config carries no ball prefab — all quiet, because seeding is ambient income and
        /// a refusal simply means the clock waits.
        /// </summary>
        public AstroLeagueBall TrySeed(Domains domain, float sizeScale)
        {
            if (_config == null || _config.ballPrefab == null) return null;
            if (!ScarabBallForge.CanSpawnLocally) return null;

            float radius = NucleusRadius;
            if (radius <= 1e-3f) return null;         // no nucleus in this cell — nothing to stud

            if (CountEmbedded(domain) >= Mathf.Max(1, _config.maxEmbeddedPerDomain)) return null;

            // A point anywhere on the sphere. Server-only, so no cross-peer determinism is owed —
            // the ball's own replicated position is what every client renders.
            Vector3 outward = Random.onUnitSphere;

            var ball = ScarabBallForge.Spawn(_config.ballPrefab, Centre + outward * radius,
                                             Vector3.zero, domain, sizeScale);
            if (ball == null) return null;

            // Sink it into the surface so it reads as EMBEDDED rather than resting on the shell.
            float sink = ball.BallWorldRadius() * Mathf.Clamp01(_config.embedSinkFraction);
            ball.EmbedOnNucleusServer(Centre + outward * (radius - sink), outward);

            _embedded.Add(ball);
            return ball;
        }

        int CountEmbedded(Domains domain)
        {
            int n = 0;
            for (int i = _embedded.Count - 1; i >= 0; i--)
            {
                var b = _embedded[i];
                if (b == null || !b.IsEmbeddedOnNucleus) { _embedded.RemoveAt(i); continue; }
                if (b.LastHitDomain == domain) n++;
            }
            return n;
        }

        // ── Release routing ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Static handler for every field: a ball was struck loose somewhere. Route it to the field
        /// whose nucleus it left — resolved by distance, because the ball carries no cell reference
        /// and a session may hold several cells. A release with no field within reach is simply a
        /// ball flying, which is a perfectly good outcome.
        /// </summary>
        static void HandleReleased(AstroLeagueBall ball, bool inward)
        {
            if (ball == null) return;

            ScarabNucleusField best = null;
            float bestSqr = float.MaxValue;
            foreach (var kv in s_byCell)
            {
                var f = kv.Value;
                if (f == null || f._cell == null) continue;
                float sqr = (ball.transform.position - f.Centre).sqrMagnitude;
                if (sqr >= bestSqr) continue;
                bestSqr = sqr;
                best = f;
            }
            best?.OnReleased(ball, inward);
        }

        void OnReleased(AstroLeagueBall ball, bool inward)
        {
            _embedded.Remove(ball);
            if (_config == null || _cell == null) return;   // cell torn down mid-flight — just let it fly

            if (!inward)
            {
                // OUTWARD → the cytoplasm. Held inside the membrane, bounced off the nucleus from
                // the outside: the core-obstacle boundary is the same nucleus sphere the court ball
                // rides from within, so one surface serves both sides.
                ball.SetBoundary(BuildCytoplasmBoundary());
                return;
            }

            // INWARD → into the nucleus. One too many overloads it, and the check runs BEFORE the
            // ball is banked so the limit counts what may rest inside, not what may enter.
            PruneInNucleus();
            if (_inNucleus.Count >= Mathf.Max(1, _config.nucleusEntryLimit))
            {
                Overload();
                return;
            }

            ball.SetBoundary(BuildCourtBoundary());
            _inNucleus.Add(ball);
        }

        void PruneInNucleus()
        {
            for (int i = _inNucleus.Count - 1; i >= 0; i--)
                if (_inNucleus[i] == null || _inNucleus[i].IsHidden) _inNucleus.RemoveAt(i);
        }

        // ── Boundaries ───────────────────────────────────────────────────────────────────

        /// <summary>Inside the nucleus: the court. A plain sphere, the shape Scramble already plays in.</summary>
        AstroLeagueBoundary BuildCourtBoundary()
        {
            float r = NucleusRadius;
            return new AstroLeagueBoundary(AstroLeagueBoundaryShape.Sphere, Centre,
                                           new Vector3(r, r, r), r);
        }

        /// <summary>
        /// Outside the nucleus: the cytoplasm. Outer sphere = the membrane (scaled in a little so a
        /// ball never rides the literal skin), core obstacle = the nucleus.
        /// </summary>
        AstroLeagueBoundary BuildCytoplasmBoundary()
        {
            float outer = Mathf.Max(NucleusRadius * 1.2f,
                                    (_cell != null ? _cell.MembraneRadius : 0f)
                                    * Mathf.Clamp01(_config.cytoplasmOuterFraction));
            return new AstroLeagueBoundary(AstroLeagueBoundaryShape.Sphere, Centre,
                                           new Vector3(outer, outer, outer), outer,
                                           coreObstacleRadius: NucleusRadius);
        }

        // ── Overload ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// The nucleus takes one ball too many: everything detonates, each with an explosion twice
        /// its own radius. Every ball leaves through its own scaled detonate-then-despawn beat, so
        /// the continuity law holds for all of them at once — nothing blinks out.
        /// </summary>
        void Overload()
        {
            float scale = Mathf.Max(0.1f, _config.detonationRadiusScale);
            int n;

            if (_config.detonateAllLiveBalls)
            {
                // The SHARED implementation, the same beat the per-CELL overload uses
                // (AstroLeagueBall.DetonateAllLooseInCellServer), so "too many balls" produces
                // one identical detonation wherever it is triggered.
                n = AstroLeagueBall.DetonateAllLiveServer(scale);
            }
            else
            {
                // Snapshot first: detonating despawns, which mutates the list underneath us.
                var doomed = new List<AstroLeagueBall>(_inNucleus);
                n = 0;
                for (int i = 0; i < doomed.Count; i++)
                {
                    if (doomed[i] == null) continue;
                    doomed[i].DetonateWithRadiusServer(scale);
                    n++;
                }
            }

            CSDebug.LogVerbose(CSLogChannel.ScarabNucleus,
                $"[ScarabNucleusField] Nucleus overload — detonated {n} ball(s) at {scale}x radius.");

            _inNucleus.Clear();
            _embedded.Clear();
        }
    }
}
