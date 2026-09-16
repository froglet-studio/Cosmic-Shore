using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// Single source of truth for the per-mode end-game counts for SkimRace, Joust, and
    /// Crystal Capture (how many crystals / jousts end a turn) and for Maelstrom / Maelstrom
    /// (how many placement points a domain needs to win the whole shuffle - "race to N").
    ///
    /// Authored ONLY through <c>Tools &gt; Cosmic Shore &gt; End Game Conditions</c>
    /// (the <c>EndConditionOverridesWindow</c> editor tool) - there are intentionally no
    /// per-scene inspector override fields anymore. The turn monitors / <c>MaelstromDataSO</c>
    /// load this asset from <c>Resources/EndConditionOverrides</c> at runtime.
    ///
    /// Semantic: <b>0 = auto/default</b>, <b>&gt; 0 = explicit count</b>:
    ///   • SkimRace / Crystal Capture - 0 falls back to the track-waypoint auto-calc (then 39).
    ///   • Joust - 0 falls back to <see cref="DefaultJoustCount"/>.
    ///   • Maelstrom - 0 falls back to <see cref="DefaultMaelstromWinTarget"/>.
    ///
    /// Two value sets are stored: the <b>Live</b> counts (what the game actually uses at runtime)
    /// and the <b>Build baseline</b> (the values a shipping build must use, captured via the tool's
    /// "Set Build Values" button). Lower a Live count to end a mode quickly while testing; when
    /// <see cref="autoRestoreBuildValuesBeforeBuild"/> is on, a build first copies the Build baseline
    /// onto the Live counts (<see cref="ApplyBuildValues"/>), so test values are never shipped.
    ///
    /// See the <c>/EndGameConditions</c> skill (<c>.claude/skills/EndGameConditions/</c>).
    /// </summary>
    [CreateAssetMenu(
        fileName = "EndConditionOverrides",
        menuName = "ScriptableObjects/" + nameof(EndConditionOverridesSO))]
    public class EndConditionOverridesSO : ScriptableObject
    {
        /// <summary>Resources path the turn monitors load this from (must live at Assets/Resources/EndConditionOverrides.asset).</summary>
        public const string ResourcePath = "EndConditionOverrides";

        /// <summary>Joust target used when <see cref="joustCount"/> is 0 (auto/default).</summary>
        public const int DefaultJoustCount = 3;

        /// <summary>Maelstrom / Maelstrom win target used when <see cref="maelstromWinTarget"/> is 0 (auto/default).</summary>
        public const int DefaultMaelstromWinTarget = 6;

        /// <summary>Nucleus Rush (Brood Rush) wave target used when <see cref="nucleusRushWaveTarget"/> is 0 (auto/default).</summary>
        public const int DefaultBroodRushWaveTarget = 3;

        /// <summary>Rampage hostile-prism target used when <see cref="rampagePrismTarget"/> is 0 (auto/default).</summary>
        public const int DefaultRampagePrismTarget = 2000;

        /// <summary>Cleave hostile-prism destruction target used when <see cref="cleavePrismTarget"/> is 0 (auto/default).</summary>
        public const int DefaultCleavePrismTarget = 1500;

        /// <summary>Wildlife Liberation kill target used when <see cref="wildlifeKillTarget"/> is 0 (auto/default).</summary>
        public const int DefaultWildlifeKillTarget = 30;

        /// <summary>Dog Fight point target used when <see cref="dogFightPointTarget"/> is 0 (auto/default).</summary>
        public const int DefaultDogFightPointTarget = 90;

        /// <summary>The Bends bend target used when <see cref="bendsPointTarget"/> is 0 (auto/default).</summary>
        public const int DefaultBendsPointTarget = 3;
        /// <summary>Scarab Scramble goal target used when <see cref="scarabScrambleGoalTarget"/> is 0 (auto/default).</summary>
        public const int DefaultScarabScrambleGoalTarget = 10;
        /// <summary>Salvo hostile-prism target used when <see cref="salvoPrismTarget"/> is 0 (auto/default).</summary>
        public const int DefaultSalvoPrismTarget = 700;
        /// <summary>Hijack steal target used when <see cref="hijackStealTarget"/> is 0 (auto/default).</summary>
        public const int DefaultHijackStealTarget = 750;
        /// <summary>Tollway toll target used when <see cref="tollwayTollTarget"/> is 0 (auto/default).</summary>
        public const int DefaultTollwayTollTarget = 4;

        /// <summary>Wrecking Ball hostile-prism target used when <see cref="wreckingBallPrismTarget"/>
        /// is 0. Lower than Rampage's 2000 because the court forest is smaller than Rampage's
        /// (35-59 plants against 59-295) and a match should end with forest still standing.</summary>
        public const int DefaultWreckingBallPrismTarget = 1500;

        /// <summary>Undertow point target used when <see cref="undertowPointTarget"/> is 0. A bend
        /// (an opposing pilot caught in the plate) is 3 and a creature killed by it is 1
        /// (UndertowScoringRuleSO), so 12 is four clean bends, twelve kills, or any mix.</summary>
        public const int DefaultUndertowPointTarget = 12;

        /// <summary>Switchback course length used when <see cref="switchbackGateTarget"/> is 0
        /// (auto/default). It is BOTH the end-game target and the number of gates the course is
        /// built with - SwitchbackController reads this same getter - so the two cannot drift.</summary>
        public const int DefaultSwitchbackGateTarget = 20;

        /// <summary>Breakwater course length used when <see cref="breakwaterStationTarget"/> is 0
        /// (auto/default) - how many stations are LAID: a polar START GATE plus a fourteen-station
        /// closed CIRCUIT. 15 is what the arena model sizes every
        /// other number against (<c>Tools/Build/breakwater_arena.py</c>): change it and the prism
        /// count, the volume and the cell's PhaseThresholds all move with it.
        ///
        /// <para><b>This is no longer the end-game target.</b> The start gate is threaded once and
        /// the circuit every lap (<see cref="DefaultBreakwaterLaps"/>), so what a pilot must
        /// thread is <see cref="GetBreakwaterCrossingTarget"/> = 29, while what the controller
        /// lays is this 15. One number did both jobs while there was one lap; laps separate them,
        /// and the getters are named for which question they answer.</para></summary>
        public const int DefaultBreakwaterStationTarget = 15;

        /// <summary>Breakwater laps used when <see cref="breakwaterLaps"/> is 0 (auto/default).
        /// Each lap re-flies the same closed circuit FORWARD - see
        /// <c>BreakwaterCourseSettings.DefaultLaps</c> for why the first gate is a start gate off
        /// the circuit rather than on it. Raising this costs no arena mass at all: it re-uses the
        /// stations already laid.</summary>
        public const int DefaultBreakwaterLaps = 2;

        /// <summary>Skein course length used when <see cref="skeinRingTarget"/> is 0. Read by
        /// BOTH SkeinRingTurnMonitor (the target) and SkeinController (how many rings to lay), so
        /// the course and the number counting it cannot drift.</summary>
        public const int DefaultSkeinRingTarget = 24;

        /// <summary>Headlong RACE length used when <see cref="headlongGateTarget"/> is 0 - gate
        /// threadings, i.e. laps x rings. 24 = three laps of the shipped eight-gate circuit.
        /// Read by <c>HeadlongGateTurnMonitor</c> for the target and by <c>HeadlongController</c>
        /// to size the circuit, so the two cannot drift.</summary>
        public const int DefaultHeadlongGateTarget = 24;

        /// <summary>Redline RACE length used when <see cref="redlineGateTarget"/> is 0 - gate
        /// threadings, i.e. laps x rings. 24 = three laps of the shipped eight-gate circuit.
        /// Read by <c>RaceGateTurnMonitor</c> (through the controller) for the target and by
        /// <c>RedlineController</c> to size the circuit, so the two cannot drift.</summary>
        public const int DefaultRedlineGateTarget = 24;

        /// <summary>Regatta RACE length used when <see cref="regattaGateTarget"/> is 0 - gate
        /// threadings, i.e. laps x rings. 24 = three laps of the eight-ring circuit. The rings
        /// per lap are a property of the ARENA (RegattaCourse.RingsPerLap - the rails are laid
        /// through them), so the controller derives its lap count as target / rings and the
        /// target must be a whole number of laps; the generator asserts it.</summary>
        public const int DefaultRegattaGateTarget = 24;


        [Header("Live counts - used at runtime. 0 = auto/default (edit via FrogletTools > Game Modes > End Game Conditions)")]
        [Tooltip("SkimRace crystals to end the race. 0 = auto-calc from the track waypoints.")]
        [Min(0)] public int hexRaceCrystalCount = 0;

        [Tooltip("Crystal Capture crystals to end the turn. 0 = auto-calc from track waypoints.")]
        [Min(0)] public int crystalCaptureCrystalCount = 20;

        [Tooltip("Joust collisions to end the turn. 0 = default (3).")]
        [Min(0)] public int joustCount = 3;

        [Tooltip("Maelstrom (Maelstrom) placement points a domain needs to win the whole shuffle " +
                 "(race to N). 0 = default (6).")]
        [Min(0)] public int maelstromWinTarget = 6;

        [Tooltip("Nucleus Rush (Brood Rush) fauna waves a domain must claim to win (race to N; one " +
                 "wave every 30s spawn cycle, so 3 ≈ a 1.5–2.5 minute match). 0 = default (3).")]
        [Min(0)] public int nucleusRushWaveTarget = 3;

        [Tooltip("Rampage: hostile prisms (another domain's mass) a domain must destroy to win " +
                 "(race to N). 0 = default (2000).")]
        [Min(0)] public int rampagePrismTarget = 2000;

        [Tooltip("Cleave: hostile prisms a domain must DESTROY to win (race to N) - cage bars, " +
                 "rival trails and fauna bodies all count; your own team's trail never does. The " +
                 "25%/50% fauna-release rungs are fractions of THIS, so moving it moves the whole " +
                 "escalation ladder with it. 0 = default (2000).")]
        [Min(0)] public int cleavePrismTarget = 1500;

        [Tooltip("Cleave: per-INTENSITY override of the target above - element 0 is intensity 1. " +
                 "Empty, a 0 entry, or an intensity past the end falls back to the scalar. It " +
                 "exists because this mode's intensities are four different PLACES rather than " +
                 "four sizes of one: 1 and 2 are vast 2,160-radius arenas holding about a third " +
                 "of the mass of the compact 3 and 4, so a shared target would make the open " +
                 "arenas the longest matches in the mode.")]
        public List<int> cleavePrismTargetByIntensity = new() { 400, 400, 1500, 1500 };

        [Tooltip("Wildlife Liberation: creatures a domain must kill between them to win " +
                 "(race to N), summed across that domain's players like every other target " +
                 "here. 0 = default (30).")]
        [Min(0)] public int wildlifeKillTarget = 30;

        [Tooltip("Dog Fight points a DOMAIN needs to win. Points come from landed gunnery: a " +
                 "bullet hit scores 1 and a missile hit (direct strike or caught in the blast) " +
                 "scores 50, so this target reads as 'either 120 bullets or 3 rockets, or any " +
                 "mix'. Teammates pool - Dog Fight is a team race, not a free-for-all. " +
                 "0 = default (120).")]
        [Min(0)] public int dogFightPointTarget = 90;

        [Tooltip("The Bends: BENDS a DOMAIN needs to win - opposing pilots caught in your " +
                 "Dolphin crystal blast, one point each. Race to 3, like Joust: three clean " +
                 "hits, or one blast that catches a pair plus one more. Teammates pool. " +
                 "0 = default (3).")]
        [Min(0)] public int bendsPointTarget = 3;
        [Tooltip("Scarab Scramble goals a DOMAIN needs to win (race to N). A goal = one of your " +
                 "domain's forged balls threaded through any hoop; teammates pool. With abundant " +
                 "crystals and continuous play, 10 reads as a 3-5 minute party match. " +
                 "0 = default (10).")]
        [Min(0)] public int scarabScrambleGoalTarget = 10;
        [Tooltip("Salvo: hostile prisms (the Boneyard's wreckage, rival trails, fauna bodies) a " +
                 "domain must destroy between them to win (race to N), summed across that " +
                 "domain's players. Lower than Rampage's target because the Sparrow's salvos " +
                 "are crystal-rationed. 0 = default (700).")]
        [Min(0)] public int salvoPrismTarget = 700;
        [Tooltip("Hijack: prisms a DOMAIN must STEAL between them to win (race to N), summed " +
                 "across that domain's players. A prism is stolen by riding over it in another " +
                 "domain's colour or by landing a spike on it, so the number counts ownership " +
                 "flips, not destruction - the same prism can be stolen back and forth all " +
                 "match and pay both thieves. Sized against the intensity-1 yard (2,772 prisms, " +
                 "~1,848 of them hostile to any one domain), so 750 leaves the yard far from " +
                 "exhausted at the whistle. 0 = default (750).")]
        [Min(0)] public int hijackStealTarget = 750;

        [Tooltip("Switchback: gates in the course, which is both how many a pilot must thread " +
                 "to finish and how many rings are laid. Compared against a domain's LEAD " +
                 "RUNNER, not a sum - every pilot flies the same course, so a teammate does not " +
                 "shorten it. 0 = default (20).")]
        [Min(0)] public int switchbackGateTarget = 20;

        [Tooltip("Breakwater: how many stations are LAID - a polar start gate plus a closed " +
                 "circuit of the rest. Each is 117-257 prisms of arena, so raising this raises " +
                 "the cell's mass and its phase ladder with it. This is NOT the end-game target " +
                 "- a pilot threads 1 + (stations-1)*laps rings. 0 = default (15).")]
        [Min(0)] public int breakwaterStationTarget = 15;

        [Tooltip("Breakwater: how many laps of the circuit. The start gate is threaded once " +
                 "and the circuit every lap, so it costs no extra arena - 1 + 14*2 is 29 " +
                 "crossings. Compared against a domain's LEAD RUNNER, not a sum. 0 = default (2).")]
        [Min(0)] public int breakwaterLaps = 2;

        [Tooltip("Skein: rings in the cable course, which is both how many a pilot must thread " +
                 "to finish and how many the generator lays. 0 = use the default (24).")]
        [Min(0)] public int skeinRingTarget = 24;

        [Tooltip("Headlong: gate threadings that win the race - LAPS x RINGS, not rings. The " +
                 "controller lays target/laps rings, so this one number is both the finish line " +
                 "and the size of the circuit. 24 = three laps of eight. 0 uses the default.")]
        [Min(0)] public int headlongGateTarget = 24;

        [Tooltip("Redline: gate threadings that win the race - LAPS x RINGS, not rings. The " +
                 "controller lays target/laps rings, so this one number is both the finish line " +
                 "and the size of the circuit. 24 = three laps of eight. 0 uses the default.")]
        [Min(0)] public int redlineGateTarget = 24;

        [Tooltip("Regatta: gate threadings that win the race - LAPS x RINGS, not rings. The " +
                 "arena lays eight rings a lap with the rails threaded through them, so this " +
                 "must be a multiple of eight; the controller races laps = target / 8. 24 = " +
                 "three laps. 0 uses the default.")]
        [Min(0)] public int regattaGateTarget = 24;

        [Tooltip("TOLLWAY - how many TOLLS a domain must collect to win. A toll is any ball " +
                 "threading a ring one of that domain's pilots planted, so the count is a " +
                 "DOMAIN sum and teammates pool. Higher than a Joust race and lower than a " +
                 "goal race: a ring must be planted, survive, and be threaded, which is " +
                 "slower than shooting at a net and faster than tearing down a wreck.")]
        [Min(0)] public int tollwayTollTarget = 8;

        [Tooltip("Wrecking Ball: hostile prisms (the court forest, rival trails, fauna bodies) a " +
                 "DOMAIN must destroy between them to win (race to N) - with the ball OR the " +
                 "cavitation plate; your own team's mass never counts. 0 = default (1500).")]
        [Min(0)] public int wreckingBallPrismTarget = 1500;

        [Tooltip("Undertow: POINTS a DOMAIN needs to win (race to N). A bend - an opposing pilot " +
                 "caught in your cavitation plate - is 3 points and a creature the plate kills is " +
                 "1, summed across the domain's pilots. 0 = default (12).")]
        [Min(0)] public int undertowPointTarget = 12;


        [Header("Build baseline - what a shipping build uses. Set via the tool's \"Set Build Values\" button.")]
        [Min(0)] public int hexRaceCrystalCountBuild = 0;
        [Min(0)] public int crystalCaptureCrystalCountBuild = 20;
        [Min(0)] public int joustCountBuild = 3;
        [Min(0)] public int maelstromWinTargetBuild = 6;
        [Min(0)] public int nucleusRushWaveTargetBuild = 3;
        [Min(0)] public int rampagePrismTargetBuild = 2000;
        [Min(0)] public int cleavePrismTargetBuild = 1500;
        [HideInInspector] public List<int> cleavePrismTargetByIntensityBuild = new() { 400, 400, 1500, 1500 };
        [Min(0)] public int wildlifeKillTargetBuild = 30;
        [Min(0)] public int dogFightPointTargetBuild = 90;
        [Min(0)] public int bendsPointTargetBuild = 3;
        [Min(0)] public int scarabScrambleGoalTargetBuild = 10;
        [Min(0)] public int salvoPrismTargetBuild = 700;
        [Min(0)] public int switchbackGateTargetBuild = 20;
        [Min(0)] public int breakwaterStationTargetBuild = 15;

        [HideInInspector, Min(0)] public int breakwaterLapsBuild = 2;
        [Min(0)] public int skeinRingTargetBuild = 24;
        [Min(0)] public int headlongGateTargetBuild = 24;
        [Min(0)] public int redlineGateTargetBuild = 24;
        [Min(0)] public int regattaGateTargetBuild = 24;
        [Min(0)] public int hijackStealTargetBuild = 750;
        [Min(0)] public int tollwayTollTargetBuild = 8;
        [Min(0)] public int wreckingBallPrismTargetBuild = 1500;
        [Min(0)] public int undertowPointTargetBuild = 12;

        [Tooltip("When on, a build first copies the Build baseline onto the Live counts, so test values are never shipped.")]
        public bool autoRestoreBuildValuesBeforeBuild = true;

        static EndConditionOverridesSO _instance;

        /// <summary>
        /// Cached runtime accessor - loads the asset from <see cref="ResourcePath"/> once.
        /// Returns null only if the asset is missing (callers fall back to their own defaults).
        /// </summary>
        public static EndConditionOverridesSO Instance
        {
            get
            {
                if (_instance == null)
                    _instance = Resources.Load<EndConditionOverridesSO>(ResourcePath);
                return _instance;
            }
        }

        /// <summary>
        /// Crystal target for a crystal-collection mode. Returns the configured count when &gt; 0,
        /// otherwise <paramref name="autoCalcFallback"/> (the mode's waypoint/default calc).
        /// </summary>
        public int GetCrystalCount(GameModes mode, int autoCalcFallback)
        {
            int configured = mode switch
            {
                GameModes.SkimRace => hexRaceCrystalCount,
                GameModes.Scurry => crystalCaptureCrystalCount,
                _ => 0,
            };
            return configured > 0 ? configured : autoCalcFallback;
        }

        /// <summary>Joust target: the configured count when &gt; 0, otherwise <see cref="DefaultJoustCount"/>.</summary>
        public int GetJoustCount() =>
            joustCount > 0 ? joustCount : DefaultJoustCount;

        /// <summary>
        /// Maelstrom / Maelstrom win target ("race to N"): the configured value when &gt; 0,
        /// otherwise <see cref="DefaultMaelstromWinTarget"/>.
        /// </summary>
        public int GetMaelstromWinTarget() => maelstromWinTarget > 0 ? maelstromWinTarget : DefaultMaelstromWinTarget;

        /// <summary>
        /// Nucleus Rush (Brood Rush) wave target ("race to N" claimed fauna waves): the configured
        /// value when &gt; 0, otherwise <see cref="DefaultBroodRushWaveTarget"/>.
        /// </summary>
        public int GetBroodRushWaveTarget() =>
            nucleusRushWaveTarget > 0 ? nucleusRushWaveTarget : DefaultBroodRushWaveTarget;

        /// <summary>
        /// Rampage prism target ("race to N" hostile prisms destroyed): the configured value
        /// when &gt; 0, otherwise <see cref="DefaultRampagePrismTarget"/>.
        /// </summary>
        public int GetRampagePrismTarget() =>
            rampagePrismTarget > 0 ? rampagePrismTarget : DefaultRampagePrismTarget;

        /// <summary>
        /// Cleave target ("race to N" hostile prisms destroyed): the configured value when
        /// &gt; 0, otherwise <see cref="DefaultCleavePrismTarget"/>.
        /// </summary>
        public int GetCleavePrismTarget() =>
            cleavePrismTarget > 0 ? cleavePrismTarget : DefaultCleavePrismTarget;

        /// <summary>
        /// Cleave target for a given INTENSITY (1-based), falling back to
        /// <see cref="GetCleavePrismTarget"/> when that rung authors nothing.
        ///
        /// <para>The fallback chain is deliberate: a missing or 0 entry means "this rung has
        /// nothing to say", never "no target" — an author who sizes rung 1 and leaves rung 2
        /// blank gets the mode's scalar, not a match that ends on the first prism.</para>
        ///
        /// <para>Safe to read the intensity at the call site because
        /// <c>CleavePrismTurnMonitor</c> resolves the target SERVER-side and replicates the
        /// result; a client receives the number rather than computing it (the distinction
        /// Docs/ECOSYSTEM.md §28 records for <c>CellTypeChoiceOptions.IntensityWise</c>).</para>
        /// </summary>
        public int GetCleavePrismTarget(int intensity)
        {
            if (cleavePrismTargetByIntensity == null || cleavePrismTargetByIntensity.Count == 0)
                return GetCleavePrismTarget();

            int i = Mathf.Clamp(intensity - 1, 0, cleavePrismTargetByIntensity.Count - 1);
            int v = cleavePrismTargetByIntensity[i];
            return v > 0 ? v : GetCleavePrismTarget();
        }

        /// <summary>
        /// Wildlife Liberation kill target ("race to N creatures killed"): the configured value
        /// when &gt; 0, otherwise <see cref="DefaultWildlifeKillTarget"/>. Compared against a
        /// DOMAIN's summed kill count.
        /// </summary>
        public int GetWildlifeKillTarget() =>
            wildlifeKillTarget > 0 ? wildlifeKillTarget : DefaultWildlifeKillTarget;

        /// <summary>
        /// Dog Fight point target ("first domain to N points"): the configured value when
        /// &gt; 0, otherwise <see cref="DefaultDogFightPointTarget"/>. Compared against a DOMAIN
        /// SUM of <see cref="CosmicShore.Data.IRoundStats.CombatPoints"/>, so teammates pool.
        /// </summary>
        public int GetDogFightPointTarget() =>
            dogFightPointTarget > 0 ? dogFightPointTarget : DefaultDogFightPointTarget;

        /// <summary>
        /// The Bends bend target ("first domain to N bends"): the configured value when
        /// &gt; 0, otherwise <see cref="DefaultBendsPointTarget"/>. Compared against a DOMAIN
        /// SUM of <see cref="CosmicShore.Data.IRoundStats.CombatPoints"/> - the same field Dog
        /// Fight races on, because both modes score vessel-vs-vessel hits and only the WEIGHTING
        /// (which lives on each mode's ScoringRule) differs.
        /// </summary>
        public int GetBendsPointTarget() =>
            bendsPointTarget > 0 ? bendsPointTarget : DefaultBendsPointTarget;

        /// <summary>
        /// Scarab Scramble goal target ("first domain to N goals"): the configured value when
        /// &gt; 0, otherwise <see cref="DefaultScarabScrambleGoalTarget"/>. Compared against a
        /// DOMAIN SUM of <see cref="CosmicShore.Data.IRoundStats.GoalsScored"/>, so teammates pool.
        /// </summary>
        public int GetScarabScrambleGoalTarget() =>
            scarabScrambleGoalTarget > 0 ? scarabScrambleGoalTarget : DefaultScarabScrambleGoalTarget;

        /// <summary>
        /// Salvo prism target ("race to N" hostile prisms destroyed): the configured value when
        /// &gt; 0, otherwise <see cref="DefaultSalvoPrismTarget"/>. Compared against a DOMAIN's
        /// summed destruction count, so teammates pool.
        /// </summary>
        public int GetSalvoPrismTarget() =>
            salvoPrismTarget > 0 ? salvoPrismTarget : DefaultSalvoPrismTarget;

        /// <summary>
        /// Switchback course length ("thread all N gates"): the configured value when &gt; 0,
        /// otherwise <see cref="DefaultSwitchbackGateTarget"/>. Read twice on purpose - by
        /// <c>RaceGateTurnMonitor</c> for the target and by <c>SwitchbackController</c>
        /// for how many gates to lay - so the course a pilot flies and the number their goal row
        /// counts to are the same authority.
        /// </summary>
        public int GetSwitchbackGateTarget() =>
            switchbackGateTarget > 0 ? switchbackGateTarget : DefaultSwitchbackGateTarget;

        /// <summary>Skein course length ("thread all N rings"). Read twice on purpose - by
        /// SkeinRingTurnMonitor for the target and by SkeinController for how many to lay.</summary>
        public int GetSkeinRingTarget() =>
            skeinRingTarget > 0 ? skeinRingTarget : DefaultSkeinRingTarget;

        /// <summary>
        /// How many Breakwater stations are LAID: the configured value when &gt; 0, otherwise
        /// <see cref="DefaultBreakwaterStationTarget"/>. Read by <c>BreakwaterController</c> to
        /// build the course. This is the ARENA number, not the race number.
        /// </summary>
        public int GetBreakwaterStationTarget() =>
            breakwaterStationTarget > 0 ? breakwaterStationTarget : DefaultBreakwaterStationTarget;

        /// <summary>How many times a Breakwater course is flown: the configured value when
        /// &gt; 0, otherwise <see cref="DefaultBreakwaterLaps"/>.</summary>
        public int GetBreakwaterLaps() =>
            breakwaterLaps > 0 ? breakwaterLaps : DefaultBreakwaterLaps;

        /// <summary>
        /// The Breakwater RACE target - how many rings a pilot must thread ("thread all N
        /// switches"): 29 for a start gate plus a fourteen-station circuit over two laps. Read by
        /// <c>RaceGateTurnMonitor</c> for the end condition and the goal row, and it is
        /// derived from the two numbers above rather than authored, so the race can never ask for
        /// a crossing the course cannot offer. Compared against a domain's LEAD RUNNER
        /// (<c>ScoringMetrics.BestByDomain</c>), never a sum.
        /// </summary>
        public int GetBreakwaterCrossingTarget() =>
            BreakwaterCourseSettings.CrossingTarget(GetBreakwaterStationTarget(), GetBreakwaterLaps());

        /// Headlong race length ("thread N gates", i.e. laps x rings): the configured value when
        /// &gt; 0, otherwise <see cref="DefaultHeadlongGateTarget"/>. Read twice on purpose - by
        /// <c>HeadlongGateTurnMonitor</c> for the target and by <c>HeadlongController</c> to size
        /// the circuit - so the finish line and the course cannot drift apart.
        /// </summary>
        public int GetHeadlongGateTarget() =>
            headlongGateTarget > 0 ? headlongGateTarget : DefaultHeadlongGateTarget;

        /// <summary>
        /// Redline race length ("thread N gates", i.e. laps x rings): the configured value when
        /// &gt; 0, otherwise <see cref="DefaultRedlineGateTarget"/>. Read by
        /// <c>RedlineController.AuthoredGateTarget</c>, which the turn monitor asks in turn - so
        /// the finish line and the course cannot drift apart.
        /// </summary>
        public int GetRedlineGateTarget() =>
            redlineGateTarget > 0 ? redlineGateTarget : DefaultRedlineGateTarget;

        /// <summary>
        /// Regatta race length ("thread N gates", i.e. laps x rings): the configured value when
        /// &gt; 0, otherwise <see cref="DefaultRegattaGateTarget"/>. Read by
        /// <c>RegattaController.AuthoredGateTarget</c>, which the turn monitor asks in turn.
        /// </summary>
        public int GetRegattaGateTarget() =>
            regattaGateTarget > 0 ? regattaGateTarget : DefaultRegattaGateTarget;

        /// <summary>
        /// Hijack steal target ("race to N" prisms stolen): the configured value when &gt; 0,
        /// otherwise <see cref="DefaultHijackStealTarget"/>. Compared against a DOMAIN's summed
        /// steal count, so teammates pool.
        /// </summary>
        public int GetHijackStealTarget() =>
            hijackStealTarget > 0 ? hijackStealTarget : DefaultHijackStealTarget;

        /// <summary>
        /// Tollway toll target ("race to N" tolls collected): the configured value when &gt; 0,
        /// otherwise <see cref="DefaultTollwayTollTarget"/>. Compared against a DOMAIN's summed
        /// toll count, so teammates pool.
        /// </summary>
        public int GetTollwayTollTarget() =>
            tollwayTollTarget > 0 ? tollwayTollTarget : DefaultTollwayTollTarget;

        /// <summary>
        /// Wrecking Ball prism target ("race to N" hostile prisms destroyed): the configured value
        /// when &gt; 0, otherwise <see cref="DefaultWreckingBallPrismTarget"/>. Compared against a
        /// DOMAIN's summed destruction count, so teammates pool.
        /// </summary>
        public int GetWreckingBallPrismTarget() =>
            wreckingBallPrismTarget > 0 ? wreckingBallPrismTarget : DefaultWreckingBallPrismTarget;

        /// <summary>
        /// Undertow point target ("first domain to N points"): the configured value when &gt; 0,
        /// otherwise <see cref="DefaultUndertowPointTarget"/>. Compared against a DOMAIN's bends
        /// (CombatPoints) plus kills (LifeformsKilled), folded by UndertowScoringRuleSO.DomainValue.
        /// </summary>
        public int GetUndertowPointTarget() =>
            undertowPointTarget > 0 ? undertowPointTarget : DefaultUndertowPointTarget;

        /// <summary>
        /// The AUTHORED turn target for a mode - what a match of it races to. Returns false for a
        /// mode whose target is auto-calculated from its track (SkimRace with a 0 count), or that
        /// has no race target at all. Read by editor tooling only; nothing at runtime uses it.
        /// </summary>
        public bool TryGetAuthoredTurnTarget(GameModes mode, out int target)
        {
            target = mode switch
            {
                GameModes.SkimRace                   => hexRaceCrystalCount,
                GameModes.Scurry => crystalCaptureCrystalCount,
                GameModes.Joust          => joustCount > 0 ? joustCount : DefaultJoustCount,
                GameModes.BroodRush               => nucleusRushWaveTarget > 0 ? nucleusRushWaveTarget : DefaultBroodRushWaveTarget,
                GameModes.Rampage                   => rampagePrismTarget > 0 ? rampagePrismTarget : DefaultRampagePrismTarget,
                // Scalar only: this reader answers "what does a match of this mode race to" for
                // editor tooling, and Cleave's real answer is per-intensity (see
                // GetCleavePrismTarget(int)). Reporting rung 1 here would read as the mode's
                // target and be wrong for three quarters of its ladder.
                GameModes.Cleave                   => cleavePrismTarget > 0 ? cleavePrismTarget : DefaultCleavePrismTarget,
                GameModes.WildlifeLiberation        => wildlifeKillTarget > 0 ? wildlifeKillTarget : DefaultWildlifeKillTarget,
                GameModes.DogFight                  => dogFightPointTarget > 0 ? dogFightPointTarget : DefaultDogFightPointTarget,
                GameModes.Bends                     => bendsPointTarget > 0 ? bendsPointTarget : DefaultBendsPointTarget,
                GameModes.ScarabScramble            => scarabScrambleGoalTarget > 0 ? scarabScrambleGoalTarget : DefaultScarabScrambleGoalTarget,
                GameModes.Salvo                     => salvoPrismTarget > 0 ? salvoPrismTarget : DefaultSalvoPrismTarget,
                GameModes.Switchback                => switchbackGateTarget > 0 ? switchbackGateTarget : DefaultSwitchbackGateTarget,
                GameModes.Breakwater                => GetBreakwaterCrossingTarget(),
                GameModes.Skein                     => skeinRingTarget > 0 ? skeinRingTarget : DefaultSkeinRingTarget,
                GameModes.Headlong                  => headlongGateTarget > 0 ? headlongGateTarget : DefaultHeadlongGateTarget,
                GameModes.Redline                   => redlineGateTarget > 0 ? redlineGateTarget : DefaultRedlineGateTarget,
                GameModes.Regatta                   => regattaGateTarget > 0 ? regattaGateTarget : DefaultRegattaGateTarget,
                GameModes.Hijack                    => hijackStealTarget > 0 ? hijackStealTarget : DefaultHijackStealTarget,
                GameModes.Tollway                   => tollwayTollTarget > 0 ? tollwayTollTarget : DefaultTollwayTollTarget,
                GameModes.WreckingBall              => wreckingBallPrismTarget > 0 ? wreckingBallPrismTarget : DefaultWreckingBallPrismTarget,
                GameModes.Undertow                  => undertowPointTarget > 0 ? undertowPointTarget : DefaultUndertowPointTarget,
                _                                   => 0,
            };

            return target > 0;
        }

        /// <summary>Element-wise equality for a per-intensity ladder, treating null and empty as
        /// the same thing (both mean "this mode authors no ladder").</summary>
        static bool SameInts(List<int> a, List<int> b)
        {
            int na = a?.Count ?? 0, nb = b?.Count ?? 0;
            if (na != nb) return false;
            for (int i = 0; i < na; i++) if (a[i] != b[i]) return false;
            return true;
        }

        /// <summary>A COPY, never the same list: build and live must not alias, or capturing the
        /// baseline would make every later live edit silently edit the baseline too.</summary>
        static List<int> CopyInts(List<int> src) => src == null ? new List<int>() : new List<int>(src);

        /// <summary>True when every Live count (used at runtime) already equals its Build baseline.</summary>
        public bool LiveMatchesBuild =>
            hexRaceCrystalCount == hexRaceCrystalCountBuild &&
            crystalCaptureCrystalCount == crystalCaptureCrystalCountBuild &&
            joustCount == joustCountBuild &&
            maelstromWinTarget == maelstromWinTargetBuild &&
            nucleusRushWaveTarget == nucleusRushWaveTargetBuild &&
            rampagePrismTarget == rampagePrismTargetBuild &&
            cleavePrismTarget == cleavePrismTargetBuild &&
            SameInts(cleavePrismTargetByIntensity, cleavePrismTargetByIntensityBuild) &&
            wildlifeKillTarget == wildlifeKillTargetBuild &&
            dogFightPointTarget == dogFightPointTargetBuild &&
            bendsPointTarget == bendsPointTargetBuild &&
            scarabScrambleGoalTarget == scarabScrambleGoalTargetBuild &&
            salvoPrismTarget == salvoPrismTargetBuild &&
            switchbackGateTarget == switchbackGateTargetBuild &&
            breakwaterStationTarget == breakwaterStationTargetBuild &&
            breakwaterLaps == breakwaterLapsBuild &&
            skeinRingTarget == skeinRingTargetBuild &&
            headlongGateTarget == headlongGateTargetBuild &&
            redlineGateTarget == redlineGateTargetBuild &&
            regattaGateTarget == regattaGateTargetBuild &&
            hijackStealTarget == hijackStealTargetBuild &&
            tollwayTollTarget == tollwayTollTargetBuild &&
            wreckingBallPrismTarget == wreckingBallPrismTargetBuild &&
            undertowPointTarget == undertowPointTargetBuild;

        /// <summary>Copy the Build baseline onto the Live counts (build → live) - used by the build auto-restore.</summary>
        public void ApplyBuildValues()
        {
            hexRaceCrystalCount = hexRaceCrystalCountBuild;
            crystalCaptureCrystalCount = crystalCaptureCrystalCountBuild;
            joustCount = joustCountBuild;
            maelstromWinTarget = maelstromWinTargetBuild;
            nucleusRushWaveTarget = nucleusRushWaveTargetBuild;
            rampagePrismTarget = rampagePrismTargetBuild;
            cleavePrismTarget = cleavePrismTargetBuild;
            cleavePrismTargetByIntensity = CopyInts(cleavePrismTargetByIntensityBuild);
            wildlifeKillTarget = wildlifeKillTargetBuild;
            dogFightPointTarget = dogFightPointTargetBuild;
            bendsPointTarget = bendsPointTargetBuild;
            scarabScrambleGoalTarget = scarabScrambleGoalTargetBuild;
            salvoPrismTarget = salvoPrismTargetBuild;
            switchbackGateTarget = switchbackGateTargetBuild;
            breakwaterStationTarget = breakwaterStationTargetBuild;
            breakwaterLaps = breakwaterLapsBuild;
            skeinRingTarget = skeinRingTargetBuild;
            headlongGateTarget = headlongGateTargetBuild;
            redlineGateTarget = redlineGateTargetBuild;
            regattaGateTarget = regattaGateTargetBuild;
            hijackStealTarget = hijackStealTargetBuild;
            tollwayTollTarget = tollwayTollTargetBuild;
            wreckingBallPrismTarget = wreckingBallPrismTargetBuild;
            undertowPointTarget = undertowPointTargetBuild;
        }

        /// <summary>Snapshot the current Live counts as the Build baseline (live → build) - used by "Set Build Values".</summary>
        public void CaptureBuildValues()
        {
            hexRaceCrystalCountBuild = hexRaceCrystalCount;
            crystalCaptureCrystalCountBuild = crystalCaptureCrystalCount;
            joustCountBuild = joustCount;
            maelstromWinTargetBuild = maelstromWinTarget;
            nucleusRushWaveTargetBuild = nucleusRushWaveTarget;
            rampagePrismTargetBuild = rampagePrismTarget;
            cleavePrismTargetBuild = cleavePrismTarget;
            cleavePrismTargetByIntensityBuild = CopyInts(cleavePrismTargetByIntensity);
            wildlifeKillTargetBuild = wildlifeKillTarget;
            dogFightPointTargetBuild = dogFightPointTarget;
            bendsPointTargetBuild = bendsPointTarget;
            scarabScrambleGoalTargetBuild = scarabScrambleGoalTarget;
            salvoPrismTargetBuild = salvoPrismTarget;
            switchbackGateTargetBuild = switchbackGateTarget;
            breakwaterStationTargetBuild = breakwaterStationTarget;
            breakwaterLapsBuild = breakwaterLaps;
            skeinRingTargetBuild = skeinRingTarget;
            headlongGateTargetBuild = headlongGateTarget;
            redlineGateTargetBuild = redlineGateTarget;
            regattaGateTargetBuild = regattaGateTarget;
            hijackStealTargetBuild = hijackStealTarget;
            tollwayTollTargetBuild = tollwayTollTarget;
            wreckingBallPrismTargetBuild = wreckingBallPrismTarget;
            undertowPointTargetBuild = undertowPointTarget;
        }
    }
}
