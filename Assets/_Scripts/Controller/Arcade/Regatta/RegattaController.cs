using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Regatta - the ARENA race: every playable hull on the same closed circuit of switch rings,
    /// with three super-shielded rails (one per domain) braided along the racing line. Every
    /// pilot flies LAPS of it in order. The race ends when the first pilot threads the last gate
    /// of the last lap, and the TEAM with the most gates threaded - every pilot's gates, summed
    /// (<see cref="RegattaScoringRuleSO"/>) - wins. Opponent AI fly the card's pinned
    /// <c>OpponentAIVessel</c> (the Squirrel) until the racing AI can drive every hull; ally AI
    /// fly what their teammates pick on the launch panel.
    ///
    /// <para><b>The mode is a question about what your hull is FOR.</b> A Rhino ramps to 1200 u/s
    /// on a straight and pays five seconds for every corner it cannot hold; a Manta trades Soar
    /// for yaw one trigger at a time; a Scarab's ceiling is its Time level; an Urchin latches onto
    /// the rail in its colour and lets the cable drive at 300 u/s through corners that cost it
    /// nothing; a Squirrel skims the same rail for the boost energy that is its only speed. The
    /// rails are the platform's ordinary conserved mass, super-shielded so a rider's speed
    /// resource cannot be shot out from under them (<see cref="SpawnableRegattaRails"/>).</para>
    ///
    /// <para><b>The grid is balanced by the CARD, not by the mode.</b> A 35 u/s Sparrow and a
    /// 1200 u/s Rhino cannot be equalised by any course, so the card authors per-hull STARTING
    /// ELEMENT levels (<c>SO_ArcadeGame.StartingElements</c>, a platform capability this mode
    /// introduced) from an offline lap-time model (<c>Tools/Build/regatta_balance.py</c>), and
    /// the comeback system does the rest during the race. Nothing here reads a hull, scales a
    /// speed, or knows which vessel is which - the controller is the gate-race platform plus
    /// the arena's rings.</para>
    ///
    /// <para><b>Everything about running the race is the platform's</b> (<see cref="GateRaceController"/>):
    /// the broadcast, the rings, the crossing test, the owner-detects/server-records round trip,
    /// the lead-runner fold, the AI steering. This class supplies the course (read off the arena
    /// prefab, Skein's pattern, so rails and rings cannot disagree), the lap count (derived from
    /// the authored target and the arena's ring count - one authority), a start line behind gate
    /// 0, and an attached AI's aim down its own rail.</para>
    /// </summary>
    public class RegattaController : GateRaceController, IPlayerSpawnLine
    {
        [Header("Regatta")]
        [Tooltip("The scene's Cell. Its EXPECTED config carries the arena prefab whose seed and " +
                 "intensity the rings are derived from. Optional - found if empty.")]
        [SerializeField] Cell arenaCell;

        [Tooltip("How far behind gate 0, along its axis, the start line sits. Every hull spawns " +
                 "here pointed through the first ring, which is fair by symmetry.")]
        [SerializeField, Min(1f)] float startLineStandoff = RegattaCourseSource.SceneStartLineStandoff;

        [Tooltip("Radius of the start ring around gate 0's axis. Wider than the mouth so the " +
                 "grid is not stacked on the rails.")]
        [SerializeField, Min(0f)] float startLineRadius = 120f;

        [Tooltip("How far down its OWN rail an attached AI aims. Far enough that the range falls " +
                 "every frame (which resets OrbitDetector) and the bearing stays near the tangent.")]
        [SerializeField, Min(1f)] float aiRailLeadDistance = 260f;

        protected override string ModeName => "Regatta";

        protected override RaceCourseSource CreateCourseSource() => new RegattaCourseSource();

        /// <summary>Points, not golf: a pilot scores their own gates and a team the SUM of its
        /// pilots' (<see cref="RegattaScoringRuleSO"/>), highest total winning - so the end-game
        /// domain totals, which sum Score, are the team standings the rule decided on.</summary>
        protected override bool UseGolfRules => false;

        /// <summary>The team score is a SUM of pilots' gates (<see cref="RegattaScoringRuleSO"/>),
        /// so halfway / home stretch / final lap are measured against the leading team's
        /// combined courses - its pilot count times the course - not one pilot's 24 gates, which
        /// a two-pilot team "reached halfway" on with each pilot a quarter of the way round.</summary>
        protected override bool RaceToastsSumTeams => true;

        /// <summary>
        /// The arena prefab, off the cell's EXPECTED config - never <c>Config</c>, which is a latch
        /// that lands a second after the first build attempt (SKEIN.md 13.2). Requires an
        /// IntensityWise cell, whose choice is derivable from the intensity the server holds.
        /// </summary>
        protected override CellConfigDataSO ResolveCourseConfig()
        {
            var cell = arenaCell != null ? arenaCell : FindAnyObjectByType<Cell>();
            return cell != null ? cell.ExpectedConfig : null;
        }

        bool TryResolveArena(out SpawnableRegattaRails arena, out string why) =>
            RegattaCourseSource.TryResolveArena(ResolveCourseConfig(), out arena, out why);

        /// <summary>
        /// Everyone starts on a ring behind gate 0, pointed through it. The platform's cell ring
        /// would put pilots 1120 u out facing the CENTRE with gate 0 on the pole above them,
        /// which for a grid of hulls whose cruise speeds span 35x is a first corner nobody asked
        /// for. Answerable during the spawn chain because the rings are a pure function of the
        /// prefab asset's seed and intensity; the fallback (no cell resolved yet) uses the
        /// default seed at the selected intensity, which is the shipped arena.
        /// </summary>
        public bool TryBuildSpawnPoses(int count, out Pose[] poses)
        {
            var gates = TryResolveArena(out var arena, out _)
                ? arena.BuildGatesNow()
                : RegattaCourse.BuildGates(RegattaCourse.DefaultSeed, Intensity);

            if (gates == null || gates.Count == 0)
            {
                poses = null;
                return false;
            }

            var first = gates[0];
            Vector3 centre = ResolveCellCentre();
            poses = CellSpawnFormation.BuildFacingRing(count, first.Position + centre, first.Axis,
                                                       startLineStandoff, startLineRadius);
            return true;
        }

        /// <summary>
        /// The Urchin's kit, for every AI Urchin on the circuit (<see cref="UrchinAutopilotDriver"/>,
        /// shared with Skein and Hijack). Created on first use rather than in a field initializer,
        /// so it loads its config on the main thread during play rather than while Unity
        /// deserialises the scene.
        /// </summary>
        UrchinAutopilotDriver _urchinAI;
        UrchinAutopilotDriver UrchinAI => _urchinAI ??= new UrchinAutopilotDriver();

        /// <summary>A race flies flat out: every Regatta AI holds full throttle (the hull's own speed verb still decides
        /// its boost). Before, the AI flew its skill throttle, 0.3-0.9.</summary>
        protected override float AIThrottleFloor => 1f;

        /// <summary>
        /// Regatta's short legs (461-858 u) are where committing from 420 u out crosses the plane beside the
        /// mouth and then circles the far-side point: the Regatta AI studio finished 17/32 Hard hull x intensity
        /// races with the shipped steering and 30/32 with this on (2026-10-15, the Regatta AI page, since deleted; REGATTA_AI_ROADMAP.md keeps its numbers).
        /// </summary>
        protected override bool AICommitFromApproachPoint => true;

        /// <summary>
        /// Regatta's AIs fly the course's rings and nothing else: never a crystal, the omni crystal included (Yash,
        /// 2026-10-17: "in Regatta no AI should target an omni crystal; only the waypoints of the track"). The scene's
        /// aiCrystalDetourSlack (220) let every gate-race AI swing up to 220 u off its line for a crystal near its route;
        /// Yash's editor flights showed Manta, Dolphin, Sparrow and Serpent AIs passing within 20 u of omni crystals and the
        /// Rhino of others. Set in code, like the commit fix above, so no scene value can turn it back on.
        /// </summary>
        protected override float AICrystalDetourSlack => 0f;

        /// <summary>
        /// While ATTACHED, ride the rail toward this pilot's next ring; while FREE, fly at the
        /// ring the platform's way (and lay a Track Projector rail when the ring is a long,
        /// straight, lined-up shot). Skein's override, on Regatta's course.
        ///
        /// <para><b>Why riding is not just "aim down the rail".</b> All three rails thread every
        /// ring, so a pilot on its OWN rail need only hold the throttle and let the cable drive -
        /// and <see cref="UrchinAutopilotDriver.TryRideToward"/> answers exactly that (every rail
        /// threads the objective ahead, so it RIDES, aiming down the rail's own tangent). What the
        /// old aim-down-the-rail override could not do was CHOOSE. The braid lays the three lanes
        /// 22 u apart, so an Urchin latches whichever it touches first; on a rival's lane it
        /// crawled at 20 u/s for the rest of the race, and on its own lane facing the wrong way
        /// round the closed loop it rode most of a lap backwards to reach the ring it had just
        /// passed under. Now (the rings' capture radius - 0.8 of a 54-110 u mouth - always
        /// contains a lane 22 u off the spine, so every lane "threads" every ring):</para>
        /// <list type="bullet">
        /// <item><b>A rival's lane is left.</b> Regatta's rails are super-shielded, so no spike can
        /// convert them (<see cref="UrchinAutopilotDriver.IsConvertible"/>) and the crawl is
        /// always "dry": the pilot Slips off unless the ring is a short crawl away. It is never
        /// spiked - a volley at a super-shielded rail would only spend the meter. With the Time-5
        /// Slipstream the rival's lane rides at full pace, is not a crawl, and is ridden.</item>
        /// <item><b>The wrong way round is reversed.</b> The ring threads BEHIND, so the pilot
        /// swings its nose round - the ride's direction is the pilot's facing.</item>
        /// <item><b>A stall is Slipped.</b> A reversal caught in the throttle deadband parks the
        /// ride; past <c>ParkedSeconds</c> the pilot lets go.</item>
        /// <item><b>Spikes still fire at convertible hostile mass</b> - anything other than the
        /// rails a pilot latches in this arena - exactly as in Skein and Hijack.</item>
        /// </list>
        ///
        /// <para>The course logic stays Regatta's: the ring is the same <see cref="_course"/> entry,
        /// through the same lap-wrapping <see cref="GateRaceController.RingIndexFor(int)"/>, that the
        /// platform's own gate aiming flies at. Every aim the driver returns while riding is down
        /// the rail's tangent, which keeps the range falling (OrbitDetector stays reset) and the
        /// authored <c>ram</c> throttle engaged. Off-rail this returns false and the platform's
        /// gate aiming takes over, including the crystal detour.</para>
        ///
        /// <para>Every other hull is untouched: none of them attaches, and the driver finds every
        /// control by capability, so it presses nothing on a hull that binds none of them.</para>
        /// </summary>
        protected override bool TryOverrideAim(IPlayer pilot, out Vector3 target)
        {
            target = default;
            var status = pilot?.Vessel?.VesselStatus;
            var tf = pilot?.Vessel?.Transform;
            if (status == null || tf == null) return false;

            bool hasGate = TryGetAIGate(pilot, out var gate);

            if (status.IsAttached && status.AttachedPrism != null)
            {
                // Finished, or the course has not landed: ride on down the rail.
                if (!hasGate)
                {
                    Vector3 along = status.Course.sqrMagnitude > 1e-4f ? status.Course.normalized : tf.forward;
                    target = tf.position + along * aiRailLeadDistance;
                    return true;
                }

                // False the frame it Slips off: fall through to flying at the ring.
                return UrchinAI.TryRideToward(pilot, gate.Position, gate.Radius, out target, aiRailLeadDistance);
            }

            if (hasGate) UrchinAI.TryProjectTrackToward(pilot, gate.Position, gate.Axis);

            // Free and an Urchin: head for ITS OWN lane, a little ahead of where it is, rather than the ring's spine the
            // three braided lanes wrap round. Aiming at the spine left the first lane it touched to the spawn slot -
            // two times in three a rival's, ridden at 20 u/s or slipped and re-latched (2026-10-14 Regatta audit).
            if (hasGate && status.VesselType == VesselClassType.Urchin && TryOwnLaneAhead(pilot, tf.position, out target))
                return true;
            return false;
        }

        [Tooltip("A free AI Urchin aims this many lane prisms ahead of the nearest point of its own-domain lane, so it " +
                 "joins the lane going the way the race goes. Lane prisms sit a few units apart.")]
        [SerializeField, Min(0)] int aiOwnLaneLeadPrisms = 12;

        SpawnableRegattaRails _railsForAI;
        readonly Dictionary<Domains, List<Prism>> _laneByDomain = new();

        /// <summary>
        /// A point on <paramref name="pilot"/>'s own-domain lane just ahead of the lane point nearest
        /// <paramref name="from"/>. The lanes are laid in gate order and close on themselves, so "ahead" is the next
        /// prisms in the list. False when the rails are not laid yet or the domain has no lane.
        /// </summary>
        bool TryOwnLaneAhead(IPlayer pilot, Vector3 from, out Vector3 target)
        {
            target = default;
            if (PrismTrailBuilder.IsLayingInProgress) return false;
            if (!_railsForAI && !RegattaCourseSource.TryResolveLiveRails(out _railsForAI, out _)) return false;

            if (!_laneByDomain.TryGetValue(pilot.Domain, out var lane) || lane == null || lane.Count == 0)
            {
                lane = null;
                var trails = _railsForAI.GetTrails();
                if (trails != null)
                    foreach (var trail in trails)
                    {
                        var list = trail?.TrailList;
                        if (list == null) continue;
                        foreach (var prism in list)
                        {
                            if (!prism) continue;
                            if (prism.Domain == pilot.Domain) lane = list;
                            break;   // a lane is one colour: its first live prism says which
                        }
                        if (lane != null) break;
                    }
                _laneByDomain[pilot.Domain] = lane;
                if (lane == null) return false;
            }

            int nearest = -1;
            float best = float.MaxValue;
            for (int i = 0; i < lane.Count; i++)
            {
                var prism = lane[i];
                if (!prism) continue;
                float d = (prism.transform.position - from).sqrMagnitude;
                if (d < best) { best = d; nearest = i; }
            }
            if (nearest < 0) return false;

            for (int step = aiOwnLaneLeadPrisms; step >= 0; step--)
            {
                var ahead = lane[(nearest + step) % lane.Count];
                if (!ahead) continue;
                target = ahead.transform.position;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Every hull but the Urchin (which rides its own lane) and the Squirrel (its own pilot) races its OWN line: the
        /// gap between the two rails that are not its domain's, at the hull's distance from the braid
        /// (<see cref="RaceLineStyleSO"/>, <c>Resources/RaceLines/&lt;Hull&gt;_Regatta</c>), aiming the hull's look-ahead
        /// along it. Yash, 2026-10-17: bigger hulls flew to the centre like the Squirrel - the middle of the braid, 22 u
        /// from all three rails - "it's always better to play from the sides". The line is <see cref="RegattaRaceLine"/>.
        /// A hull with no style asset, or before the rails are laid, flies the gate way. Every seat draws its own risk
        /// round the hull's, so a grid of one hull has careful and bold pilots.
        /// </summary>
        protected override bool TryRaceLine(IPlayer pilot, Vector3 self, out Vector3 aim, out Vector3 after)
        {
            aim = after = default;
            var status = pilot?.Vessel?.VesselStatus;
            if (status == null || status.VesselType == VesselClassType.Urchin || status.IsAttached) return false;

            if (!_lineStyles.TryGetValue(status.VesselType, out var style))
            {
                style = RaceLineStyleSO.Load(status.VesselType, GameModes.Regatta);
                _lineStyles[status.VesselType] = style;
            }
            if (!style) return false;

            ulong seat = pilot.PlayerNetId;
            if (!_pilotLines.TryGetValue(seat, out var state) || state.Line == null || state.Style != style)
            {
                if (!TryBuildLine(pilot.Domain, style, unchecked((int)seat * 7919 + 17), out state)) return false;
                _pilotLines[seat] = state;
            }

            int nearest = state.Line.Nearest(self, state.Hint);
            // Strayed (a break-off, a knock): find the line again from scratch.
            if ((state.Line[nearest] - self).sqrMagnitude > LineRefindDistance * LineRefindDistance)
                nearest = state.Line.Nearest(self, -1);
            state.Hint = nearest;
            _pilotLines[seat] = state;

            float look = style.Lookahead(status.Speed, state.Risk);
            aim = state.Line.PointAhead(nearest, look);
            // The corner the boost drivers judge is the turn AT the ring they are after (AIBoostContext's objective), so
            // "after" is the line a look-ahead past that ring, not past the steering point.
            after = TryGetAIGate(pilot, out var gate)
                ? state.Line.PointAhead(LineIndexAtRing(state.Line, gate.Position), look)
                : state.Line.PointAhead(nearest, 2f * look);
            return true;
        }

        readonly Dictionary<(RegattaRaceLine line, Vector3 ring), int> _lineIndexAtRing = new();

        /// <summary>The line point nearest a ring's centre - where the line threads it. Searched once per line and ring.</summary>
        int LineIndexAtRing(RegattaRaceLine line, Vector3 ring)
        {
            if (_lineIndexAtRing.TryGetValue((line, ring), out int i)) return i;
            i = line.Nearest(ring, -1);
            _lineIndexAtRing[(line, ring)] = i;
            return i;
        }

        const float LineRefindDistance = 250f;

        struct PilotLine
        {
            public RegattaRaceLine Line;
            public RaceLineStyleSO Style;
            public float Risk;
            public int Hint;
        }

        readonly Dictionary<VesselClassType, RaceLineStyleSO> _lineStyles = new();
        readonly Dictionary<ulong, PilotLine> _pilotLines = new();
        readonly Dictionary<(int gap, int radius), RegattaRaceLine> _gapLines = new();

        /// <summary>
        /// The line for a pilot of <paramref name="domain"/>: the gap between the two lanes that are not its colour (so
        /// each domain races its own side of the braid), at the radius the hull's style and this seat's risk give.
        /// Lines are shared by every pilot with the same gap and radius. False until the rails are laid.
        /// </summary>
        bool TryBuildLine(Domains domain, RaceLineStyleSO style, int seatSeed, out PilotLine state)
        {
            state = default;
            if (_course.Count == 0 || PrismTrailBuilder.IsLayingInProgress) return false;
            if (!_railsForAI && !RegattaCourseSource.TryResolveLiveRails(out _railsForAI, out _)) return false;
            var trails = _railsForAI.GetTrails();
            if (trails == null) return false;

            var lanes = new List<List<Vector3>>(3);
            int own = -1;
            foreach (var trail in trails)
            {
                var list = trail?.TrailList;
                if (list == null) continue;
                var points = new List<Vector3>(list.Count);
                Domains laneDomain = default;
                bool known = false;
                foreach (var prism in list)
                {
                    if (!prism) continue;
                    if (!known) { laneDomain = prism.Domain; known = true; }
                    points.Add(prism.transform.position);
                }
                if (points.Count < 3) continue;
                if (known && laneDomain == domain) own = lanes.Count;
                lanes.Add(points);
            }
            if (lanes.Count != 3) return false;
            if (own < 0) own = 0;   // a lobby without this domain's lane: any one gap is as fair as another

            float tightest = float.MaxValue;
            for (int i = 0; i < _course.Count; i++) tightest = Mathf.Min(tightest, _course[i].Radius);
            float risk = style.RiskFor(seatSeed);
            float radius = style.LaneRadius(tightest, RegattaCourse.DefaultRails.LaneOffset, risk);

            var key = (own, Mathf.RoundToInt(radius));
            if (!_gapLines.TryGetValue(key, out var line))
            {
                line = RegattaRaceLine.BuildGapLine(lanes[(own + 1) % 3], lanes[(own + 2) % 3], lanes[own], radius);
                if (line == null) return false;
                _gapLines[key] = line;
            }
            state = new PilotLine { Line = line, Style = style, Risk = risk, Hint = -1 };
            return true;
        }

        /// <summary>The ring <paramref name="pilot"/> must thread next, from the same course and
        /// the same progress counter the platform's own gate aiming reads.</summary>
        bool TryGetAIGate(IPlayer pilot, out RaceGate gate)
        {
            gate = default;
            if (_course.Count == 0) return false;
            int index = pilot.RoundStats?.SwitchesThreaded ?? 0;
            if (index < 0 || index >= RaceLength) return false;
            gate = _course[RingIndexFor(index)];
            return true;
        }

        public override void OnNetworkDespawn()
        {
            _urchinAI?.Clear();
            _pilotLines.Clear();
            _gapLines.Clear();
            _lineIndexAtRing.Clear();
            base.OnNetworkDespawn();
        }
    }
}
