using CosmicShore.Data;
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

        /// <summary>PeelTheCage cage-destruction target used when <see cref="ribcagePrismTarget"/> is 0 (auto/default).</summary>
        public const int DefaultPeelTheCagePrismTarget = 2000;

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

        /// <summary>
        /// Drumfire match length in SECONDS, used when <see cref="drumfireSeconds"/> is 0
        /// (auto/default). The only end-game number here that is a clock rather than a count:
        /// Drumfire has no race target, so this IS its end condition. 75s covers ONE unhurried
        /// pass down a firing lane with room to spare, which is all the mode is sized for: the
        /// drum holds roughly one pass of ammunition for a full lobby (Tools/Build/
        /// drumfire_arena.py measures it), so a longer clock would only leave pilots flying at
        /// a ball that is already gone.
        /// </summary>
        public const int DefaultDrumfireSeconds = 75;

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

        [Tooltip("PeelTheCage: hostile prisms a domain must DESTROY to win (race to N) - cage bars, " +
                 "rival trails and fauna bodies all count; your own team's trail never does. The " +
                 "25%/50% fauna-release rungs are fractions of THIS, so moving it moves the whole " +
                 "escalation ladder with it. 0 = default (2000).")]
        [Min(0)] public int ribcagePrismTarget = 2000;

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

        [Tooltip("Drumfire: how many SECONDS a match runs. Drumfire has no race target - the " +
                 "clock is the end condition and the volume each domain tears out of the drum " +
                 "is the score - so this is the one entry here that is a duration. 75 covers one " +
                 "unhurried pass down a lane; the drum only holds about one pass of ammunition " +
                 "for a full lobby, so raising it mostly adds time with nothing left to shoot. " +
                 "0 = default (75).")]
        [Min(0)] public int drumfireSeconds = 75;

        [Header("Build baseline - what a shipping build uses. Set via the tool's \"Set Build Values\" button.")]
        [Min(0)] public int hexRaceCrystalCountBuild = 0;
        [Min(0)] public int crystalCaptureCrystalCountBuild = 20;
        [Min(0)] public int joustCountBuild = 3;
        [Min(0)] public int maelstromWinTargetBuild = 6;
        [Min(0)] public int nucleusRushWaveTargetBuild = 3;
        [Min(0)] public int rampagePrismTargetBuild = 2000;
        [Min(0)] public int ribcagePrismTargetBuild = 2000;
        [Min(0)] public int wildlifeKillTargetBuild = 30;
        [Min(0)] public int dogFightPointTargetBuild = 90;
        [Min(0)] public int bendsPointTargetBuild = 3;
        [Min(0)] public int scarabScrambleGoalTargetBuild = 10;
        [Min(0)] public int salvoPrismTargetBuild = 700;
        [Min(0)] public int drumfireSecondsBuild = 75;

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
        /// PeelTheCage target ("race to N" hostile prisms destroyed): the configured value when
        /// &gt; 0, otherwise <see cref="DefaultPeelTheCagePrismTarget"/>.
        /// </summary>
        public int GetPeelTheCagePrismTarget() =>
            ribcagePrismTarget > 0 ? ribcagePrismTarget : DefaultPeelTheCagePrismTarget;

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
        /// Drumfire match length in seconds: the configured value when &gt; 0, otherwise
        /// <see cref="DefaultDrumfireSeconds"/>. Unlike every other accessor here this is not
        /// compared against a domain sum - it is handed to
        /// <c>DrumfireTimeTurnMonitor</c> as the countdown, and the winner is whichever domain
        /// leads on volume when it expires.
        /// </summary>
        public int GetDrumfireSeconds() =>
            drumfireSeconds > 0 ? drumfireSeconds : DefaultDrumfireSeconds;

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
                GameModes.PeelTheCage                   => ribcagePrismTarget > 0 ? ribcagePrismTarget : DefaultPeelTheCagePrismTarget,
                GameModes.WildlifeLiberation        => wildlifeKillTarget > 0 ? wildlifeKillTarget : DefaultWildlifeKillTarget,
                GameModes.DogFight                  => dogFightPointTarget > 0 ? dogFightPointTarget : DefaultDogFightPointTarget,
                GameModes.Bends                     => bendsPointTarget > 0 ? bendsPointTarget : DefaultBendsPointTarget,
                GameModes.ScarabScramble            => scarabScrambleGoalTarget > 0 ? scarabScrambleGoalTarget : DefaultScarabScrambleGoalTarget,
                GameModes.Salvo                     => salvoPrismTarget > 0 ? salvoPrismTarget : DefaultSalvoPrismTarget,
                _                                   => 0,
            };

            return target > 0;
        }

        /// <summary>True when every Live count (used at runtime) already equals its Build baseline.</summary>
        public bool LiveMatchesBuild =>
            hexRaceCrystalCount == hexRaceCrystalCountBuild &&
            crystalCaptureCrystalCount == crystalCaptureCrystalCountBuild &&
            joustCount == joustCountBuild &&
            maelstromWinTarget == maelstromWinTargetBuild &&
            nucleusRushWaveTarget == nucleusRushWaveTargetBuild &&
            rampagePrismTarget == rampagePrismTargetBuild &&
            ribcagePrismTarget == ribcagePrismTargetBuild &&
            wildlifeKillTarget == wildlifeKillTargetBuild &&
            dogFightPointTarget == dogFightPointTargetBuild &&
            bendsPointTarget == bendsPointTargetBuild &&
            scarabScrambleGoalTarget == scarabScrambleGoalTargetBuild &&
            salvoPrismTarget == salvoPrismTargetBuild &&
            drumfireSeconds == drumfireSecondsBuild;

        /// <summary>Copy the Build baseline onto the Live counts (build → live) - used by the build auto-restore.</summary>
        public void ApplyBuildValues()
        {
            hexRaceCrystalCount = hexRaceCrystalCountBuild;
            crystalCaptureCrystalCount = crystalCaptureCrystalCountBuild;
            joustCount = joustCountBuild;
            maelstromWinTarget = maelstromWinTargetBuild;
            nucleusRushWaveTarget = nucleusRushWaveTargetBuild;
            rampagePrismTarget = rampagePrismTargetBuild;
            ribcagePrismTarget = ribcagePrismTargetBuild;
            wildlifeKillTarget = wildlifeKillTargetBuild;
            dogFightPointTarget = dogFightPointTargetBuild;
            bendsPointTarget = bendsPointTargetBuild;
            scarabScrambleGoalTarget = scarabScrambleGoalTargetBuild;
            salvoPrismTarget = salvoPrismTargetBuild;
            drumfireSeconds = drumfireSecondsBuild;
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
            ribcagePrismTargetBuild = ribcagePrismTarget;
            wildlifeKillTargetBuild = wildlifeKillTarget;
            dogFightPointTargetBuild = dogFightPointTarget;
            bendsPointTargetBuild = bendsPointTarget;
            scarabScrambleGoalTargetBuild = scarabScrambleGoalTarget;
            salvoPrismTargetBuild = salvoPrismTarget;
            drumfireSecondsBuild = drumfireSeconds;
        }
    }
}
