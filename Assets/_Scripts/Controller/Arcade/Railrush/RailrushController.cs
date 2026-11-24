using CosmicShore.Data;
using CosmicShore.UI;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Railrush - the Urchin's CHARGE game (<c>GameModes.Railrush = 124</c>, RAILRUSH.md). Hijack's Switchyard,
    /// scored on chain-spike hits on rival hulls instead of steals.
    ///
    /// <para><b>Why it inherits Hijack.</b> The yard (three rings of open rails, each painted in three domain
    /// thirds, burrs on every launch line), its replicated prism ownership (which is what makes ride speed agree
    /// between machines) and its rail-riding AI are exactly the arena a joust on rails needs. Railrush owns only
    /// what differs: the rule (<see cref="RailrushScoringRuleSO"/>, CombatPoints), the scored stat the base zeroes
    /// and replicates (<see cref="ZeroScoredStats"/> and friends) and the AI's two choices - which rail to run
    /// (one that LAUNCHES at a rival) and when to stop riding and hunt.</para>
    ///
    /// <para><b>The lance is the vessel's own physics.</b> A spike's velocity is its muzzle speed PLUS the hull's
    /// (URCHIN_CHAIN_SPIKES.md, round 7), so a volley thrown at grind speed off a friendly third (300 u/s, 360 off
    /// the end) reaches about three times as far as one thrown at cruise. Nothing about the Urchin changes (the
    /// LOCKED vessel rule): the rails are where the speed comes from, and the mode's AI is taught to use them.</para>
    ///
    /// <para><b>The AI's skill is the seat's AI difficulty, never the intensity</b> (<c>AIDifficulty.cs</c>): the
    /// ladders below are indexed Easy, Medium, Hard, Legendary.</para>
    /// </summary>
    public class RailrushController : HijackController
    {
        [Header("Railrush AI (ladders: Easy, Medium, Hard, Legendary)")]
        [Tooltip("How far a rival may be for the AI to hunt it at all, world units, by AI difficulty. Past it the " +
                 "AI flies the rails and picks the one that launches it at the nearest rival.")]
        [SerializeField] float[] aiHuntRange = { 500f, 650f, 800f, 950f };

        [Tooltip("Seconds of the rival's own velocity the AI leads its aim by, by AI difficulty. 0 aims at where " +
                 "the rival IS, which a moving target has always left.")]
        [SerializeField] float[] aiLeadSeconds = { 0f, 0.15f, 0.3f, 0.45f };

        [Tooltip("Half-angle of the cone ahead the rival must be inside before the AI fires, degrees, by AI " +
                 "difficulty. Wider fires more often and misses more.")]
        [SerializeField] float[] aiFireConeDegrees = { 30f, 24f, 18f, 14f };

        [Header("Railrush AI - the spike's reach")]
        [Tooltip("The spike's muzzle speed, u/s - UrchinSpikeAction.asset projectileSpeed (the generator checks they " +
                 "agree). A spike flies at this plus the hull's own speed.")]
        [SerializeField, Min(1f)] float aiSpikeMuzzleSpeed = 60f;

        [Tooltip("The spike's flight time, seconds - UrchinSpikeAction.asset projectileTime (the generator checks).")]
        [SerializeField, Min(0.1f)] float aiSpikeFlightSeconds = 2f;

        [Tooltip("Fraction of the spike's full reach the AI fires inside - under 1 so a volley lands before it fades.")]
        [SerializeField, Range(0.2f, 1f)] float aiReachFraction = 0.8f;

        [Tooltip("The AI leaves the rail plan to hunt once a rival is inside this many times its current spike " +
                 "reach. The reach grows with the hull's speed, so a launched AI engages from far further out than " +
                 "a cruising one - the joust is meant to be thrown off the rail.")]
        [SerializeField, Min(1f)] float aiEngageReachMultiple = 1.5f;

        [Tooltip("How far past the rival the AI aims while hunting, so it flies THROUGH rather than orbiting a " +
                 "point it arrives at (Docs/AI_ORBIT_BREAK.md).")]
        [SerializeField, Min(20f)] float aiHuntOvershoot = 120f;

        bool _hintPosted;

        protected override void Update()
        {
            base.Update();
            if (_hintPosted || gameData == null || !gameData.IsTurnRunning) return;
            _hintPosted = true;
            var local = gameData.LocalPlayer;
            if (local != null) GameToastAPI.Post(GameToastSituation.RailrushHint, local.Domain);
        }

        protected override void OnResetForReplayCustom()
        {
            base.OnResetForReplayCustom();
            _hintPosted = false;
        }

        // ── The scored stat: combat points, with spike hits as the breakdown ──

        protected override void ZeroScoredStats(IRoundStats stats)
        {
            stats.CombatPoints = 0;
            stats.BulletHitsLanded = 0;
        }

        protected override int ReadScoredStat(IRoundStats stats) => stats.CombatPoints;
        protected override void WriteScoredStat(IRoundStats stats, int value) => stats.CombatPoints = value;
        protected override int ReadBreakdownStat(IRoundStats stats) => stats.BulletHitsLanded;
        protected override void WriteBreakdownStat(IRoundStats stats, int value) => stats.BulletHitsLanded = value;

        // ── The AI ──────────────────────────────────────────────────────────

        int Rung(IPlayer pilot) => Mathf.Clamp((int)gameData.AIDifficultyFor(pilot) - 1, 0, 3);

        static float Ladder(float[] ladder, int rung) =>
            ladder == null || ladder.Length == 0 ? 0f : ladder[Mathf.Clamp(rung, 0, ladder.Length - 1)];

        float SpikeReach(float hullSpeed) =>
            (Mathf.Max(0f, hullSpeed) + aiSpikeMuzzleSpeed) * aiSpikeFlightSeconds * aiReachFraction;

        /// <summary>The nearest live pilot of another domain within <paramref name="range"/>, or null.</summary>
        IPlayer NearestRival(Vector3 from, Domains domain, float range)
        {
            var players = gameData.Players;
            if (players == null) return null;
            IPlayer best = null;
            float bestSqr = range * range;
            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                if (p == null || p.Domain == domain) continue;
                var tf = p.Vessel?.Transform;
                if (tf == null) continue;
                float sqr = (tf.position - from).sqrMagnitude;
                if (sqr < bestSqr) { bestSqr = sqr; best = p; }
            }
            return best;
        }

        Vector3 LeadPoint(IPlayer rival, float leadSeconds)
        {
            var tf = rival.Vessel.Transform;
            var st = rival.Vessel.VesselStatus;
            Vector3 velocity = st != null ? st.Course * st.Speed : Vector3.zero;
            return tf.position + velocity * leadSeconds;
        }

        /// <summary>
        /// HUNT: on the rail it chose, the AI keeps the grind (the speed is the lance) and only fires when a rival is
        /// inside the cone ahead and inside the spike's reach. Off the rail - launched, or cruising - it takes the
        /// steering once a rival is inside <see cref="aiEngageReachMultiple"/> of its reach, aims THROUGH the lead
        /// point and fires inside the cone. Otherwise the heist plan flies the rails, with
        /// <see cref="ChooseRail"/> picking one that launches at a rival.
        /// </summary>
        protected override bool TryOverrideAITarget(IPlayer pilot, IVesselStatus self, Vector3 position,
                                                    bool onChosenRail, out Vector3 target)
        {
            target = default;
            int rung = Rung(pilot);
            var rival = NearestRival(position, pilot.Domain, Ladder(aiHuntRange, rung));
            if (rival == null) return false;

            Vector3 aim = LeadPoint(rival, Ladder(aiLeadSeconds, rung));
            Vector3 toAim = aim - position;
            float range = toAim.magnitude;
            float reach = SpikeReach(self.Speed);
            Vector3 forward = pilot.Vessel.Transform.forward;
            bool inCone = range > 1e-3f &&
                          Vector3.Dot(forward, toAim / range) >= Mathf.Cos(Ladder(aiFireConeDegrees, rung) * Mathf.Deg2Rad);

            if (inCone && range <= reach) SpikeVolley(pilot);

            // Grinding the chosen rail: keep it. The ride constrains position, not attitude, and turning the nose off
            // the rail would throw away the launch the grind is building.
            if (onChosenRail) return false;

            if (range > reach * aiEngageReachMultiple) return false;

            Vector3 through = range > 1e-3f ? toAim / range : forward;
            target = aim + through * aiHuntOvershoot;
            return true;
        }

        /// <summary>
        /// Run the rail that THROWS this pilot at a rival: how well the rail's launch line (its far-end tangent)
        /// points at the nearest rival past its end, how far the rail is, and how much of it is this pilot's colour
        /// (a friendly third grinds at full speed, a hostile one crawls). With no rival in the yard it falls back to
        /// the heist's choice, so the AI keeps flying the network instead of parking.
        /// </summary>
        protected override int ChooseRail(HijackYard yard, Vector3 from, Domains domain, int excluded)
        {
            int best = -1;
            float bestScore = 0f;
            for (int i = 0; i < yard.Rails.Count; i++)
            {
                if (i == excluded) continue;
                var r = yard.Rails[i];
                Vector3 start = yard.WorldPoint(r.LocalStart);
                Vector3 end = yard.WorldPoint(r.LocalEnd);
                Vector3 along = (end - start).normalized;

                var rival = NearestRival(end, domain, float.MaxValue);
                if (rival == null) return base.ChooseRail(yard, from, domain, excluded);

                Vector3 toRival = rival.Vessel.Transform.position - end;
                float align = toRival.sqrMagnitude > 1e-6f ? Mathf.Max(0f, Vector3.Dot(along, toRival.normalized)) : 0f;
                float distance = Vector3.Distance(from, start);
                float ownFraction = yard.OwnFractionOfRail(i, domain);

                float score = (0.2f + align) * (ownFraction + 0.3f) / (1f + distance / 300f);
                if (score > bestScore) { bestScore = score; best = i; }
            }
            return best;
        }
    }
}
