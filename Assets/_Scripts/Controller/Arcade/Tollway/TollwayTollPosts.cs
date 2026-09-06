using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Tollway's <b>toll posts</b> — the court's own fixed sockets, and the answer to the one
    /// thing that made the mode's first cut degenerate: <i>a ring must be gotten into a certain
    /// place.</i>
    ///
    /// <para>Before this, a switch landed wherever the pilot's nose pointed, so the whole game
    /// collapsed into "plant a ring a hundred and fifty units in front of your own ball, nudge it
    /// through, repeat" — one step, no skill ceiling, and therefore not replayable. A post fixes
    /// the WHERE. It does <b>not</b> fix the facing: the ring still faces the line the placer flew
    /// (<see cref="PlaceSwitchActionExecutor"/> keeps using the vessel's course as the axis), so
    /// aiming the mouth at where you intend the ball to arrive from is still a real decision. The
    /// post takes away the one degree of freedom that was breaking the mode and leaves the two
    /// that were making it.</para>
    ///
    /// <para><b>Why that fixes it, structurally rather than statistically.</b> A ring's position is
    /// now not a function of the ball at all — it is one of N places the COURT chose — so "place it
    /// in front of the ball" is not a move that exists. What is left is the shot the mode is
    /// actually about: get to a post, plant facing the line you want, then drive a ball across the
    /// court and through a mouth a couple of dozen units wide. The ball's position is emergent, the
    /// post set is redrawn every match, and posts are consumed and re-contested, so the loop has no
    /// closed form.</para>
    ///
    /// <para><b>The layout is DERIVED, never replicated.</b> Placement runs on every peer (the
    /// action handler re-executes a press everywhere), so the socket book has to agree everywhere
    /// or peers would build switches in different places. It is therefore a pure function of one
    /// replicated int seed plus the replicated court radius — the SkimRace track-seed shape — and
    /// it deliberately takes NO input that lags: no ball positions, no vessel velocities, nothing
    /// a client can hold a different opinion about. Occupancy reads
    /// <see cref="ScarabSwitch.Live"/>, which is itself built per-peer from the same replicated
    /// presses, so the claim book cannot desync further than the switch list already does.</para>
    ///
    /// <para><b>A post is an EMBLEM, never a switch.</b> It is drawn in the published emblem
    /// grammar — a core sphere with a TILTED spinning halo at the mouth radius a ring will get —
    /// precisely because <c>Docs/ToySystem/ARCHITECTURE.md</c> § "The switch" reserves the
    /// continuous, square-across-the-path ring for something you THREAD. Threading a post does
    /// nothing, so a post that looked like a switch would be a lie about a trigger volume that
    /// does not exist. The halo also states the size of the mouth you are about to be given.</para>
    ///
    /// <para>Costs nothing conserved: markers are generated meshes, not prisms, so they are not
    /// food, not mass, and not on the cell's volume ladder. The monuments a paid toll raises are
    /// the mass in this mode.</para>
    /// </summary>
    public static class TollwayTollPosts
    {
        /// <summary>The golden angle, the Fibonacci-sphere spacing constant.</summary>
        const float GoldenAngle = 2.399963229728653f;

        /// <summary>
        /// Two switch centres this close are the same socket. Placement SNAPS a ring exactly onto
        /// a post position derived from the same numbers on every peer, so the test is an equality
        /// test with an epsilon for float drift — not a proximity radius. Using
        /// <see cref="ClaimRadius"/> here instead would let one planted ring silently close every
        /// neighbouring post that happened to fall inside it.
        /// </summary>
        const float OccupiedEpsilon = 1f;

        static readonly List<Vector3> s_positions = new();
        // Parallel to s_positions, and only populated when a marker parent was supplied. The HUD
        // arrow needs a Transform to point at (IObjectiveProvider's contract), so the marker IS
        // the thing a pilot is sent to — never a synthetic transform minted per query.
        static readonly List<Transform> s_markers = new();
        static float s_claimRadius;
        static GameObject s_markerRoot;

        /// <summary>Every post in the current court, in layout order. Empty before the server's
        /// seed and radius have both landed.</summary>
        public static IReadOnlyList<Vector3> Positions => s_positions;

        /// <summary>How near a requested ring centre must fall to a free post to be admitted.</summary>
        public static float ClaimRadius => s_claimRadius;

        /// <summary>True once a layout exists. Placement is refused before then, which is correct:
        /// there is nowhere legal to put a ring yet.</summary>
        public static bool HasLayout => s_positions.Count > 0;

        // ── Layout ───────────────────────────────────────────────────────────

        /// <summary>
        /// The pure half: fill <paramref name="into"/> with the post positions for this seed. No
        /// Unity randomness is touched — <c>UnityEngine.Random</c> is a shared global stream and
        /// borrowing it here would both perturb every other consumer and make the layout depend on
        /// what else ran first, which is the one thing a per-peer derivation cannot afford.
        /// </summary>
        public static void ComputeLayout(int seed, Vector3 centre, float courtRadius, int count,
                                         float innerFraction, float outerFraction, List<Vector3> into)
        {
            into.Clear();
            count = Mathf.Max(1, count);
            float inner = Mathf.Clamp01(Mathf.Min(innerFraction, outerFraction));
            float outer = Mathf.Clamp01(Mathf.Max(innerFraction, outerFraction));

            uint state = (uint)seed * 2654435761u ^ 0x9E3779B9u;
            if (state == 0u) state = 0x6D2B79F5u;

            // One seeded rotation of the whole shell, so two matches on the same court are not the
            // same map. Drawn first so the per-post radii below start from a known stream point.
            var spin = Quaternion.Euler(Next01(ref state) * 360f,
                                        Next01(ref state) * 360f,
                                        Next01(ref state) * 360f);

            for (int i = 0; i < count; i++)
            {
                // Fibonacci sphere: the even spread the platform already uses for 5+ spawn slots
                // (CellSpawnFormation.Symmetric), so no post is a long flight from every other.
                float k = (i + 0.5f) / count;
                float z = 1f - 2f * k;
                float r = Mathf.Sqrt(Mathf.Max(0f, 1f - z * z));
                float phi = i * GoldenAngle;
                var dir = spin * new Vector3(r * Mathf.Cos(phi), r * Mathf.Sin(phi), z);

                // Posts sit in a BAND, not on a shell: a shell would put every socket at one
                // distance from the middle and make "which post" a purely angular choice.
                float radius = Mathf.Lerp(inner, outer, Next01(ref state)) * courtRadius;
                into.Add(centre + dir * radius);
            }
        }

        /// <summary>
        /// Build (or rebuild) the court's posts and their markers. Safe to call again — the old
        /// markers come down first, so a court resize or a replay cannot leave two sets standing.
        /// </summary>
        public static void Build(int seed, Vector3 centre, float courtRadius, int count,
                                 float innerFraction, float outerFraction, float claimRadius,
                                 float socketRadius, Transform markerParent,
                                 ThemeManagerDataContainerSO theme)
        {
            Clear();
            if (courtRadius <= 0f) return;

            ComputeLayout(seed, centre, courtRadius, count, innerFraction, outerFraction, s_positions);
            s_claimRadius = Mathf.Max(1f, claimRadius);

            if (markerParent == null) return;

            s_markerRoot = new GameObject("TollPosts");
            s_markerRoot.transform.SetParent(markerParent, false);

            // Neutral: a post belongs to nobody until a ring is standing in it, and Domains.Blue is
            // the platform's "no team" sentinel. Read through ToyFactory so the marker is drawn in
            // the same live prism material the switch that lands here will wear.
            var material = ToyFactory.DomainPrismMaterial(theme, Domains.Blue);
            var accent = ToyFactory.DomainAccentColor(theme, Domains.Blue);

            for (int i = 0; i < s_positions.Count; i++)
            {
                var post = new GameObject($"TollPost::{i}");
                s_markers.Add(post.transform);
                post.transform.SetParent(s_markerRoot.transform, false);
                post.transform.position = s_positions[i];
                // Tilted out of every plane a ring will be drawn in, and spinning (AddRingBody
                // attaches ToyIdleSpin) — an emblem's halo, which the shape language reserves for
                // exactly this: a thing that marks a place rather than one you thread.
                post.transform.rotation = Quaternion.Euler(55f, 0f, 25f);

                ToyFactory.AddSphereBody(post.transform, Mathf.Max(1f, socketRadius * 0.18f),
                                         accent, material);
                ToyFactory.AddRingBody(post.transform, Mathf.Max(2f, socketRadius),
                                       accent, material);
            }
        }

        /// <summary>Tear the book and its markers down. Called on despawn and on replay, so a
        /// stale layout can never be handed to the next match.</summary>
        public static void Clear()
        {
            s_positions.Clear();
            s_markers.Clear();
            s_claimRadius = 0f;
            if (s_markerRoot) Object.Destroy(s_markerRoot);
            s_markerRoot = null;
        }

        // ── Queries ──────────────────────────────────────────────────────────

        /// <summary>Is post <paramref name="index"/> unclaimed — i.e. is no standing switch already
        /// sitting in it? Reads <see cref="ScarabSwitch.Live"/>, so a spent or retired ring frees
        /// its socket the instant it leaves that roster.</summary>
        public static bool IsFree(int index)
        {
            if (index < 0 || index >= s_positions.Count) return false;
            Vector3 p = s_positions[index];
            var live = ScarabSwitch.Live;
            for (int i = 0; i < live.Count; i++)
            {
                var sw = live[i];
                if (sw == null) continue;
                if ((sw.transform.position - p).sqrMagnitude <= OccupiedEpsilon * OccupiedEpsilon)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// The placement rule itself. A requested ring centre is admitted only if a FREE post lies
        /// within <see cref="ClaimRadius"/> of it, and then the ring is snapped exactly onto that
        /// post — the snap is what makes every peer agree on the centre, and what makes occupancy
        /// an equality test rather than a fuzzy one.
        /// </summary>
        public static bool TryResolve(Vector3 requested, out Vector3 position)
        {
            position = requested;
            int index = NearestFree(requested, out float distance);
            if (index < 0 || distance > s_claimRadius) return false;
            position = s_positions[index];
            return true;
        }

        /// <summary>Index of the nearest unclaimed post to <paramref name="from"/>, or -1.</summary>
        public static int NearestFree(Vector3 from, out float distance)
        {
            int best = -1;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < s_positions.Count; i++)
            {
                if (!IsFree(i)) continue;
                float sqr = (s_positions[i] - from).sqrMagnitude;
                if (sqr >= bestSqr) continue;
                bestSqr = sqr;
                best = i;
            }
            distance = best < 0 ? float.MaxValue : Mathf.Sqrt(bestSqr);
            return best;
        }

        /// <summary>Where the nearest unclaimed post is, or <paramref name="fallback"/> when every
        /// post in the court is already claimed. Used by the AI's steering hook.</summary>
        public static Vector3 NearestFreePosition(Vector3 from, Vector3 fallback)
        {
            int index = NearestFree(from, out _);
            return index < 0 ? fallback : s_positions[index];
        }

        /// <summary>The nearest unclaimed post's MARKER, or null — what the HUD arrow points at
        /// while a pilot has no ring standing.</summary>
        public static Transform NearestFreeMarker(Vector3 from)
        {
            int index = NearestFree(from, out _);
            if (index < 0 || index >= s_markers.Count) return null;
            var marker = s_markers[index];
            return marker ? marker : null;
        }

        // ── Deterministic stream (xorshift32) ────────────────────────────────

        static uint NextState(ref uint state)
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return state;
        }

        static float Next01(ref uint state) => (NextState(ref state) >> 8) * (1f / 16777216f);
    }
}
