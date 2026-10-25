using System;
using CosmicShore.Data;
using CosmicShore.UI;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The race beats of a DOMAIN RACE - quarter, halfway, lead change, home stretch, final lap -
    /// posted as toasts, for a mode whose controller has no milestone sampler of its own.
    ///
    /// <para><b>A local poll, not a server post.</b> Every input is replicated: the score is the
    /// mode's <see cref="ScoringRuleSO.DomainValue"/> fold over RoundStats NetworkVariables, and
    /// the target is the rule's own. So each peer runs this against its own copy of the same
    /// numbers and reaches the same beats with nothing crossing the wire - the shape
    /// <c>StatToastDriver</c> and <c>RaceRankToastDriver</c> already use, and the reason no
    /// ClientRpc is added for it. A beat a peer reaches a poll later than another is a toast that
    /// lands a quarter-second later; nothing here changes game state.</para>
    ///
    /// <para><b>Silent seeding.</b> The first poll of a turn records where the race already is
    /// and announces nothing, so a late joiner (or a replay whose stats have not cleared yet)
    /// never hears a backlog. At a normal turn start every value is 0, so seeding costs no beat.</para>
    ///
    /// <para><b>Posting into nothing is the opt-out.</b> Every beat is posted unconditionally;
    /// a mode whose <c>GameToastConfigSO</c> does not author a situation shows nothing for it.
    /// The gate-race family shares one controller, so a mode in it that wants none of these
    /// simply does not author them.</para>
    ///
    /// <para>Every post carries the same four arguments: <c>{0}</c> the leading domain,
    /// <c>{1}</c> its score, <c>{2}</c> the target, <c>{3}</c> the name of its best single
    /// pilot (the lead runner in a gate race; the top contributor in a summed race).</para>
    ///
    /// <para><b>A TEAM-SUMMED race scales every threshold by the leading team's size.</b> The
    /// rule's target is ONE pilot's course (Regatta: 24 gates), but a summed
    /// <see cref="ScoringRuleSO.DomainValue"/> is every pilot's gates added up - so comparing the
    /// two fired halfway, home stretch and final lap early the moment a team had two pilots
    /// (a pair each a quarter of the way round read as "halfway"). A mode that sums passes
    /// <see cref="CountPilots"/>; the leading team's target, home stretch and final-lap mark
    /// are then that many courses' worth (<see cref="Evaluate"/>). Without it - every other mode -
    /// the arithmetic is exactly what it was.</para>
    /// </summary>
    public sealed class DomainRaceToasts
    {
        /// <summary>Fraction of the target at which the QUARTER beat fires, and below which a
        /// change of leader is not announced (an opening scramble is noise, not news).</summary>
        public const float QuarterFraction = 0.25f;

        /// <summary>Fraction of the target at which the HALFWAY beat fires.</summary>
        public const float HalfFraction = 0.5f;

        /// <summary>Minimum seconds between two lead-change toasts, so a neck-and-neck pair
        /// trading the lead every poll reads as one beat rather than a scrolling wall.</summary>
        public const float LeadChangeMinGapSeconds = 8f;

        /// <summary>Seconds between polls. The beats are coarse states.</summary>
        public const float PollSeconds = 0.5f;

        readonly ScoringRuleSO _rule;
        readonly Func<GameDataSO, Domains, int> _pilotsPerDomain;

        float _turnStartTime = float.NaN;
        bool _seeded;
        float _nextPoll;
        bool _quarterDone;
        bool _halfDone;
        bool _homeDone;
        bool _finalLapDone;
        Domains _leader = Domains.Blue;
        float _lastLeadToastTime = float.NegativeInfinity;

        /// <param name="pilotsPerDomain">For a TEAM-SUMMED race only (<see cref="CountPilots"/>):
        /// how many courses a domain's score is the sum of. Null = one, every other mode.</param>
        public DomainRaceToasts(ScoringRuleSO rule, Func<GameDataSO, Domains, int> pilotsPerDomain = null)
        {
            _rule = rule;
            _pilotsPerDomain = pilotsPerDomain;
        }

        /// <summary>The pilots flying for <paramref name="domain"/> - the size of its summed
        /// score. Read off the replicated RoundStats every peer holds.</summary>
        public static int CountPilots(GameDataSO gameData, Domains domain)
        {
            int n = 0;
            var list = gameData?.RoundStatsList;
            if (list == null) return 0;
            for (int i = 0, c = list.Count; i < c; i++)
                if (list[i] != null && list[i].Domain == domain) n++;
            return n;
        }

        /// <summary>Which beats a score has reached. Pure, so the shipped thresholds are the
        /// tested ones.</summary>
        public readonly struct Beats
        {
            public readonly bool Quarter, Half, Home, FinalLap;
            /// <summary>The target the beats were measured against - posted as <c>{2}</c>.</summary>
            public readonly int Target;

            public Beats(bool quarter, bool half, bool home, bool finalLap, int target)
            {
                Quarter = quarter; Half = half; Home = home; FinalLap = finalLap; Target = target;
            }
        }

        /// <summary>
        /// The beat thresholds for a leading score of <paramref name="best"/>.
        /// <paramref name="courseTarget"/>, <paramref name="homeStretchRemaining"/> and
        /// <paramref name="finalLapAt"/> are ONE pilot's; <paramref name="pilots"/> is how many
        /// such courses the score sums (1 for every mode but a team-summed one, and a value
        /// below 1 is read as 1), and every threshold scales by it.
        /// </summary>
        public static Beats Evaluate(int best, int courseTarget, int homeStretchRemaining, int finalLapAt,
                                     int pilots = 1)
        {
            int p = Mathf.Max(1, pilots);
            int target = courseTarget * p;
            int homeRemaining = homeStretchRemaining * p;
            int finalAt = finalLapAt * p;

            bool quarter = best >= QuarterFraction * target;
            bool half = best >= HalfFraction * target;
            bool home = homeStretchRemaining > 0 && target > homeRemaining * 2
                        && best >= target - homeRemaining && best < target;
            bool finalLap = finalLapAt > 0 && finalAt < target && best >= finalAt && best < target;
            return new Beats(quarter, half, home, finalLap, target);
        }

        /// <summary>
        /// Call every frame (it throttles itself) while the turn runs, on EVERY peer.
        /// <paramref name="homeStretchRemaining"/> &gt; 0 announces the leading domain coming
        /// within that many of the target (a gate race's last gates); 0 disables it.
        /// <paramref name="finalLapAt"/> &gt; 0 announces the leading domain's score reaching
        /// that value - the first gate of the last lap of a lapped course; 0 disables it.
        /// </summary>
        public void Tick(GameDataSO gameData, int homeStretchRemaining = 0, int finalLapAt = 0)
        {
            if (_rule == null || gameData == null || !gameData.IsTurnRunning) return;

            // A new turn (or a replay's fresh start) clears every beat. Keyed off the turn's own
            // start time, which every peer stamps in StartTurn, so this needs no reset call.
            if (!Mathf.Approximately(gameData.TurnStartTime, _turnStartTime))
            {
                _turnStartTime = gameData.TurnStartTime;
                _seeded = false;
                _quarterDone = _halfDone = _homeDone = _finalLapDone = false;
                _leader = Domains.Blue;
                _lastLeadToastTime = float.NegativeInfinity;
                _nextPoll = 0f;
            }

            if (Time.time < _nextPoll) return;
            _nextPoll = Time.time + PollSeconds;

            int target = _rule.TargetFor(gameData);
            if (target <= 0) return; // the monitor has not resolved the target yet

            // Strict leader: a tie at the top has no leader, so nobody "takes the lead" by
            // merely drawing level. Fixed domain order, so every peer agrees.
            var leader = Domains.Blue;
            int best = 0;
            bool tied = false;
            int dc = Mathf.Clamp(gameData.RequestedDomainCount, 1, GameDataSO.ActiveDomains.Length);
            for (int i = 0; i < dc; i++)
            {
                var d = GameDataSO.ActiveDomains[i];
                int value = _rule.DomainValue(gameData, d);
                if (value > best) { best = value; leader = d; tied = false; }
                else if (value == best && value > 0) tied = true;
            }
            var top = leader;   // the team the thresholds are sized by, tie or not
            if (tied) leader = Domains.Blue;

            // The LEADING team's size: its summed score is measured against its own courses.
            // Sized off the top team even in a tie, so a silent seed during a tie records the
            // same thresholds the next announced poll will test.
            int pilots = _pilotsPerDomain != null && top != Domains.Blue
                ? _pilotsPerDomain(gameData, top)
                : 1;
            var beats = Evaluate(best, target, homeStretchRemaining, finalLapAt, pilots);
            target = beats.Target;
            bool quarter = beats.Quarter;
            bool half = beats.Half;
            bool home = beats.Home;
            bool finalLap = beats.FinalLap;

            if (!_seeded)
            {
                _seeded = true;
                _quarterDone = quarter;
                _halfDone = half;
                _homeDone = home;
                _finalLapDone = finalLap;
                if (leader != Domains.Blue) _leader = leader;
                return;
            }

            if (leader == Domains.Blue || best <= 0) return;

            // One beat per poll, most significant first, so two thresholds crossed in the same
            // half-second do not stack two toasts; the other lands on the next poll.
            if (home && !_homeDone)
            {
                _homeDone = true;
                _halfDone = _quarterDone = true;   // never announce a smaller rung after this one
                Post(GameToastSituation.DomainRaceHomeStretch, gameData, leader, best, target);
            }
            else if (finalLap && !_finalLapDone)
            {
                _finalLapDone = true;
                Post(GameToastSituation.DomainRaceFinalLap, gameData, leader, best, target);
            }
            else if (half && !_halfDone)
            {
                _halfDone = _quarterDone = true;
                Post(GameToastSituation.DomainRaceHalf, gameData, leader, best, target);
            }
            else if (quarter && !_quarterDone)
            {
                _quarterDone = true;
                Post(GameToastSituation.DomainRaceQuarter, gameData, leader, best, target);
            }
            else if (leader != _leader && _leader != Domains.Blue && quarter
                     && Time.time - _lastLeadToastTime >= LeadChangeMinGapSeconds)
            {
                _lastLeadToastTime = Time.time;
                Post(GameToastSituation.DomainRaceLeadChanged, gameData, leader, best, target);
            }

            _leader = leader;
        }

        void Post(GameToastSituation situation, GameDataSO gameData, Domains leader, int score, int target)
        {
            GameToastAPI.Post(situation, leader,
                leader.ToString(), score.ToString(), target.ToString(), BestPilotOf(gameData, leader));
        }

        /// <summary>The leading domain's best single pilot by the mode's metric - its lead
        /// runner in a gate race, its top contributor in a summed race.</summary>
        string BestPilotOf(GameDataSO gameData, Domains domain)
        {
            string name = string.Empty;
            int best = int.MinValue;
            var list = gameData.RoundStatsList;
            for (int i = 0, n = list.Count; i < n; i++)
            {
                var stats = list[i];
                if (stats == null || stats.Domain != domain || string.IsNullOrEmpty(stats.Name)) continue;
                int v = _rule.LiveMetric(stats);
                if (v > best) { best = v; name = stats.Name; }
            }
            return name;
        }
    }
}
