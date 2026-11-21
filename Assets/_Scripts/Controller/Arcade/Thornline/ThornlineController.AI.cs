using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Thornline's AI (THORNLINE.md §4): what an AI Shrike aims at (<see cref="ThornlineAIPlan"/>, the same code
    /// <c>Tools/Build/thornline_course_harness/run.sh --ai</c> races offline) and how sharply it aims (a nose lead by
    /// difficulty). Input only: the plan returns a point and the Shrike's own autopilot presses the sling when its axis
    /// anchors near it. The vessel's code and assets are untouched.
    /// </summary>
    public partial class ThornlineController
    {
        sealed class RacerState
        {
            public int Seat;
            public int Threaded = -1;
            public float LastRingAt;
            public float RescueUntil = -1f;
        }

        readonly Dictionary<IPlayer, RacerState> _racers = new();

        // The plan's view of the course, refreshed once a frame (the alive and live flags change as thorns break).
        readonly List<Vector3> _planThorns = new();
        readonly List<float> _planSizes = new();
        readonly List<bool> _planAlive = new();
        readonly List<Vector3> _planCrystals = new();
        readonly List<int> _planCrystalLegs = new();
        readonly List<bool> _planCrystalLive = new();
        int _planFrame = -1;

        void RebuildPlanLists()
        {
            _planThorns.Clear(); _planSizes.Clear(); _planAlive.Clear();
            for (int i = 0; i < _thorns.Count; i++)
            {
                _planThorns.Add(_thorns[i].Thorn.Position);
                _planSizes.Add(_thorns[i].Thorn.Size);
                _planAlive.Add(true);
            }
            _planCrystals.Clear(); _planCrystalLegs.Clear(); _planCrystalLive.Clear();
            for (int i = 0; i < _crystals.Count; i++)
            {
                _planCrystals.Add(_crystals[i].Position);
                _planCrystalLegs.Add(_crystals[i].Leg);
                _planCrystalLive.Add(true);
            }
            _planFrame = -1;
        }

        void RefreshPlanFlags()
        {
            if (_planFrame == Time.frameCount) return;
            _planFrame = Time.frameCount;
            for (int i = 0; i < _thorns.Count && i < _planAlive.Count; i++) _planAlive[i] = Holds(_thorns[i].Prism);
            for (int i = 0; i < _crystals.Count && i < _planCrystalLive.Count; i++) _planCrystalLive[i] = IsLive(_crystals[i]);
        }

        /// <summary>A new race for this pilot: its seat (spreads the lanes), its stall clock, and its aim skill for its
        /// seat's difficulty - the nose lead on its AIPilot and the follow rate on its transformer, the Redline /
        /// Switchback pattern (a mode setting on the AI's skill; the vessel untouched).</summary>
        protected override void OnRacerArmed(IPlayer pilot)
        {
            if (pilot == null) return;
            _racers[pilot] = new RacerState { Seat = _racers.Count, LastRingAt = Time.time };
            var row = RowFor(pilot);
            var transformer = pilot.Vessel?.VesselStatus?.VesselTransformer;
            if (transformer) transformer.AINoseFollowPerSecond = row.NoseFollow;
            // TryGetComponent, not VesselStatus.AIPilot: that getter ADDS an autopilot (HeldByAutopilot).
            if (pilot.Vessel?.VesselStatus is Component status && status && status.TryGetComponent(out AIPilot autopilot))
            {
                autopilot.NoseLeadGain = row.NoseLeadGain;
                autopilot.NoseLeadMaxExtraDegrees = aiNoseLeadMaxExtraDegrees;
            }
        }

        ThornlineAIRow RowFor(IPlayer pilot)
        {
            int level = gameData ? (int)gameData.AIDifficultyFor(pilot) : 2;
            return ThornlineAIPlan.RowFor(level, aiNoseLeadByDifficulty, aiNoseFollowByDifficulty,
                                          aiTakesSnipes ? ThornlineAIPlan.SnipesFromDifficulty : int.MaxValue);
        }

        /// <summary>
        /// The AI Shrike aims at a lane point on a THORN, never at a ring and never at a thorn's centre: its autopilot
        /// slings when the weapons' axis anchors on a block within 20 degrees of this point, and holds the lance when two
        /// blocks of another domain sit on that axis (<see cref="ThornlineAIPlan"/> says why each choice).
        /// </summary>
        protected override bool TryOverrideAim(IPlayer pilot, out Vector3 target)
        {
            target = default;
            if (_thorns.Count < 2 || _course.Count == 0 || pilot == null) return false;
            var tf = pilot.Vessel?.Transform;
            if (!tf) return false;
            int threaded = pilot.RoundStats?.SwitchesThreaded ?? 0;
            if (threaded >= RaceLength) return false;
            if (!_racers.TryGetValue(pilot, out var state))
                _racers[pilot] = state = new RacerState { Seat = _racers.Count, LastRingAt = Time.time };

            float now = Time.time;
            if (threaded != state.Threaded)
            {
                state.Threaded = threaded;
                state.LastRingAt = now;
                state.RescueUntil = -1f;
            }
            // No ring for StallSeconds: point it at the nearest live thorn in range for a while, then start the clock again.
            if (state.RescueUntil < 0f && now - state.LastRingAt >= ThornlineAIPlan.StallSeconds)
                state.RescueUntil = now + ThornlineAIPlan.RescueSeconds;
            if (state.RescueUntil >= 0f && now >= state.RescueUntil)
            {
                state.RescueUntil = -1f;
                state.LastRingAt = now;
            }

            RefreshPlanFlags();
            var input = new ThornlineAIPlan.Inputs
            {
                Thorns = _planThorns, Sizes = _planSizes, Alive = _planAlive, Rings = _course,
                Crystals = _planCrystals, CrystalLegs = _planCrystalLegs, CrystalLive = _planCrystalLive,
                Self = tf.position, Threaded = RingIndexFor(threaded), Seat = state.Seat, Row = RowFor(pilot),
                Stalled = state.RescueUntil >= 0f,
            };
            return ThornlineAIPlan.Choose(input, out target) != ThornlineAim.None;
        }
    }
}
