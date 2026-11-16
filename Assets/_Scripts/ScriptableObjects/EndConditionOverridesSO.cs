using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using UnityEngine;
using UnityEngine.Serialization;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// Single source of truth for the per-mode end-game counts for SkimRace, Joust, and
    /// Crystal Capture (how many crystals / jousts end a turn) and for Maelstrom / Maelstrom
    /// (how many placement points a domain needs to win the whole shuffle - "race to N").
    ///
    /// Authored ONLY through <c>FrogletTools &gt; Game Modes &gt; End Game Conditions</c>
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
        public const int DefaultJoustCount = 2;

        /// <summary>Maelstrom / Maelstrom win target used when <see cref="maelstromWinTarget"/> is 0 (auto/default).</summary>
        public const int DefaultMaelstromWinTarget = 6;

        /// <summary>Nucleus Rush (Brood Rush) wave target used when <see cref="nucleusRushWaveTarget"/> is 0 (auto/default).</summary>
        public const int DefaultBroodRushWaveTarget = 3;

        /// <summary>Rampage hostile-prism target used when <see cref="rampagePrismTarget"/> is 0 (auto/default),
        /// and when a rung of <see cref="rampagePrismTargetByIntensity"/> authors nothing.</summary>
        public const int DefaultRampagePrismTarget = 2000;

        /// <summary>Cleave hostile-prism destruction target used when <see cref="cleavePrismTarget"/> is 0 (auto/default).</summary>
        public const int DefaultCleavePrismTarget = 1000;

        /// <summary>Wildlife Liberation kill target used when <see cref="wildlifeKillTarget"/> is 0 (auto/default).</summary>
        public const int DefaultWildlifeKillTarget = 30;

        /// <summary>Dog Fight point target used when <see cref="dogFightPointTarget"/> is 0 (auto/default).
        /// 90 until 2026-10-26, when no editor match had reached it inside 63-180 s.</summary>
        public const int DefaultDogFightPointTarget = 40;

        /// <summary>Grizzly Charge point target used when <see cref="grizzlyChargePointTarget"/> is 0. The
        /// mode runs Dog Fight's monitor and price list (a Grizzly blast is a rocket's 20-point middle
        /// tier), and shared Dog Fight's key until 2026-10-26: at 90 its editor matches already ended
        /// near 90 s (median 93 s over seven), so it keeps 90 while Dog Fight's own target fell.</summary>
        public const int DefaultGrizzlyChargePointTarget = 90;

        /// <summary>The Bends bend target used when <see cref="bendsPointTarget"/> is 0 (auto/default).</summary>
        public const int DefaultBendsPointTarget = 3;
        /// <summary>Scarab Scramble goal target used when <see cref="scarabScrambleGoalTarget"/> is 0 (auto/default).</summary>
        public const int DefaultScarabScrambleGoalTarget = 3;
        /// <summary>Astro League mercy-rule goal limit used when <see cref="astroLeagueGoalLimit"/> is 0 (auto/default).</summary>
        public const int DefaultAstroLeagueGoalLimit = 5;
        /// <summary>Salvo hostile-prism target used when <see cref="salvoPrismTarget"/> is 0 (auto/default).</summary>
        public const int DefaultSalvoPrismTarget = 700;
        /// <summary>Hijack steal target used when <see cref="hijackStealTarget"/> is 0 (auto/default).</summary>
        public const int DefaultHijackStealTarget = 500;
        /// <summary>Tollway toll target used when <see cref="tollwayTollTarget"/> is 0 (auto/default).</summary>
        public const int DefaultTollwayTollTarget = 3;

        /// <summary>Wrecking Ball hostile-prism target used when <see cref="wreckingBallPrismTarget"/>
        /// is 0. Lower than Rampage's 2000 because the court forest is smaller than Rampage's
        /// (35-59 plants against 59-295) and a match should end with forest still standing. 1500 until
        /// 2026-10-26, when a solo editor run had not reached it by 178 s.</summary>
        public const int DefaultWreckingBallPrismTarget = 650;

        /// <summary>Undertow point target used when <see cref="undertowPointTarget"/> is 0. A bend
        /// (an opposing pilot caught in the plate) is 3 and a creature killed by it is 1
        /// (UndertowScoringRuleSO), so 12 is four clean bends, twelve kills, or any mix.</summary>
        public const int DefaultUndertowPointTarget = 12;

        /// <summary>Dustup point target used when <see cref="dustupPointTarget"/> is 0. One
        /// DUSTING - an opposing Butterfly caught in your Scale Dust - is one point
        /// (DustupScoringRuleSO), so 4 is four clean passes over a rival (10 until 2026-10-26, when an
        /// editor match had not reached it by 211 s).</summary>
        public const int DefaultDustupPointTarget = 4;

        /// <summary>Hood snipe target used when <see cref="serpentHoodSnipeTarget"/> is 0. One
        /// SNIPE - a Sniper Shot that strips a rival pilot (a Debuff-class combat hit) - is one point
        /// (SerpentHoodScoringRuleSO), summed across the domain's pilots.</summary>
        public const int DefaultSerpentHoodSnipeTarget = 3;

        /// <summary>Jackstraw strike target used when <see cref="jackstrawStrikeTarget"/> is 0. One STRIKE - the
        /// Thresher's ball or chain reaching a rival hull (a Strike-class combat hit) - is one point
        /// (JackstrawScoringRuleSO), summed across the domain's pilots.</summary>
        public const int DefaultJackstrawStrikeTarget = 3;

        /// <summary>Riposte strike target used when <see cref="riposteStrikeTarget"/> is 0. One STRIKE - a Swordfish sword
        /// hit on a rival hull (a Strike-class combat hit, which lands only while the Swordfish is the faster) - is one
        /// point (RiposteScoringRuleSO), summed across the domain's pilots.</summary>
        public const int DefaultRiposteStrikeTarget = 6;
        /// <summary>Bullrush gore target used when <see cref="bullrushGoreTarget"/> is 0. One GORE - the Rhino ramming a
        /// rival faster than it and above the gore speed (a Strike-class combat hit) - is one point (a
        /// JackstrawScoringRuleSO asset), summed across the domain's pilots.</summary>
        public const int DefaultBullrushGoreTarget = 5;
        /// <summary>Windrow round length in seconds, used when <see cref="windrowRoundSeconds"/> is 0. TIMED, Tapestry's
        /// reason: its score (a domain's trail standing) is a live stock the plough's strikes can take away.</summary>
        public const int DefaultWindrowRoundSeconds = 90;
        /// <summary>Landslide hostile-prism target used when <see cref="landslidePrismTarget"/> is 0. Every prism is a
        /// plough strike (a thrown prism and the one it hits, both scored to the Rhino) in Cleave's arenas.</summary>
        public const int DefaultLandslidePrismTarget = 500;
        /// <summary>Flatline point target used when <see cref="flatlinePointTarget"/> is 0 and the rung authors
        /// nothing. Points are the fleet price (CombatHitDrain.PointsFor): a round on a rival hull is 1, a cord or
        /// thrown-saber Strike 8, ten points to the petal; the ring blast pays nothing (FlatlineScoringRuleSO),
        /// summed across the domain's pilots.</summary>
        public const int DefaultFlatlinePointTarget = 90;

        /// <summary>Kelpwood bite target used when <see cref="kelpwoodBiteTarget"/> is 0. One BITE - a creature an
        /// Angler locked on a rival reaching its hull (a Strike-class combat hit) - is one point
        /// (KelpwoodScoringRuleSO), summed across the domain's pilots.</summary>
        public const int DefaultKelpwoodBiteTarget = 12;

        /// <summary>Corona bloom target used when <see cref="coronaBloomTarget"/> is 0. One BLOOM HIT - a rival pilot
        /// caught in a bloom of a bomb a Manta planted (a Debuff-class combat hit) - is one point
        /// (CoronaScoringRuleSO), summed across the domain's pilots.</summary>
        public const int DefaultCoronaBloomTarget = 15;

        /// <summary>Shockfront ripple-hit target used when <see cref="shockfrontRippleTarget"/> is 0. One RIPPLE HIT - a
        /// rival pilot stripped by the front of a Stoat's annihilation ripple (a Debuff-class combat hit) - is one point
        /// (ShockfrontScoringRuleSO), summed across the domain's pilots.</summary>
        public const int DefaultShockfrontRippleTarget = 10;

        /// <summary>Fulmen hostile-prism target used when <see cref="fulmenPrismTarget"/> is 0. Prisms a DOMAIN's Fulmen
        /// bolts (and anything else it does) destroy in Rampage's forest, summed across its pilots; the generator
        /// (author_fulmen_assets.py) holds it under a quarter of intensity 4's forest.</summary>
        public const int DefaultFulmenPrismTarget = 1200;
        /// <summary>Railrush hit target used when <see cref="railrushHitTarget"/> is 0. One HIT - an Urchin's chain
        /// spike reaching a rival hull (a Bullet-class combat hit) - is one point (RailrushScoringRuleSO), summed
        /// across the domain's pilots. An ESTIMATE pending a timed match.</summary>
        public const int DefaultRailrushHitTarget = 20;
        /// <summary>Standoff blast target used when <see cref="standoffBlastTarget"/> is 0. One BLAST HIT - a rival pilot
        /// caught in a Grizzly's blast, a trigger bomb's or the charged cannon's (a MissileBlast-class combat hit) - is
        /// one point (StandoffScoringRuleSO), summed across the domain's pilots. A first cut: nobody has played it.</summary>
        public const int DefaultStandoffBlastTarget = 10;

        /// <summary>Harvest hostile-prism target used when <see cref="harvestPrismTarget"/> is 0. The Thresher's
        /// sheaves never grow back, so three domains' worth of it must fit in the smallest field (the generator,
        /// author_harvest_assets.py, asserts it); Wrecking Ball's 650 is the nearest played number.</summary>
        public const int DefaultHarvestPrismTarget = 750;
        /// <summary>Shatterplane prism target used when <see cref="shatterplanePrismTarget"/> is 0 and the rung authors
        /// nothing. Hostile prisms a DOMAIN breaks (the neutral plates), summed across its pilots (ShatterplaneScoringRuleSO,
        /// Rampage's rule). An ESTIMATE pending a timed match: about 30% of a rung's breakable plate mass
        /// (shatterplane_budget.py).</summary>
        public const int DefaultShatterplanePrismTarget = 1600;
        /// <summary>Stingherd sting target used when <see cref="stingherdStingTarget"/> is 0. One STING - a danger
        /// creature of a Jacks' herd touching a rival hull, a Strike-class combat hit (StingherdScoringRuleSO) - summed
        /// across the domain's pilots. A first cut, unplayed: Kelpwood's 12 bites is the nearest shipped number.</summary>
        public const int DefaultStingherdStingTarget = 12;
        /// <summary>Trawl prism target used when <see cref="trawlPrismTarget"/> is 0. Hostile prisms a DOMAIN destroys,
        /// summed across its pilots (TrawlScoringRuleSO, Rampage's rule): a prism the Jacks' net drags that runs into
        /// another destroys both, scored to the net. Harvest's field never regrows and the net cannot break shielded
        /// mass, so three domains' worth of the field's unshielded hostile prisms must fit in the smallest rung
        /// (author_trawl_assets.py asserts it). An estimate, unplayed.</summary>
        public const int DefaultTrawlPrismTarget = 300;

        /// <summary>Tapestry round length in seconds, used when <see cref="tapestryRoundSeconds"/>
        /// is 0. Tapestry is TIMED - its score is a live stock (mass standing), which can fall, so
        /// "most standing at the whistle" is the only honest end condition.</summary>
        public const int DefaultTapestryRoundSeconds = 90;

        /// <summary>Palisade round length in seconds, used when <see cref="palisadeRoundSeconds"/> is 0. Timed like
        /// Tapestry: the score is the Swordfish's mass standing (trail and afterimages), which rival swords cut.</summary>
        public const int DefaultPalisadeRoundSeconds = 90;
        /// <summary>Cleaner Ram ram target used when <see cref="cleanerRamStrikeTarget"/> is 0. One RAM - the Wrasse's
        /// charged kite reaching a rival hull (a Strike-class combat hit) - is one point (CleanerRamScoringRuleSO), summed
        /// across the domain's pilots. A first cut, unplayed: a ram needs a Charge loop first, so it is rarer than a bite.</summary>
        public const int DefaultCleanerRamStrikeTarget = 8;

        /// <summary>Reefbuild round length in seconds, used when <see cref="reefbuildRoundSeconds"/> is 0. TIMED, Tapestry's
        /// reason: the score (the reef standing) is a live stock that rival kites pop and blast.</summary>
        public const int DefaultReefbuildRoundSeconds = 90;

        /// <summary>Blastline hostile-prism target used when <see cref="blastlinePrismTarget"/> is 0. Hostile prisms a
        /// DOMAIN destroys with the Wrasse's Space blasts and kite pops, summed across its pilots (BlastlineScoringRuleSO,
        /// Rampage's rule). Rampage's forest regrows, so the field cannot run dry; a first cut, unplayed.</summary>
        public const int DefaultBlastlinePrismTarget = 600;

        /// <summary>Glasshouse round length in seconds, used when <see cref="glasshouseRoundSeconds"/> is 0. Glasshouse
        /// is TIMED, Tapestry's reason: its score (trail + garden standing in a domain's colour) is a live stock that
        /// grazers and rival schools eat, so "most standing at the whistle" is the only honest end condition. 90 s is
        /// the arcade's target length (Docs/ARCADE_MATCH_LENGTH.md).</summary>
        public const int DefaultGlasshouseRoundSeconds = 90;

        /// <summary>Chum round length in seconds, used when <see cref="chumRoundSeconds"/> is 0. Chum is TIMED: its reef is
        /// finite and the wild herds eat it too, so a prism target could fall out of every domain's reach once the shoals
        /// are stripped. 90 s is the arcade's target length (Docs/ARCADE_MATCH_LENGTH.md).</summary>
        public const int DefaultChumRoundSeconds = 90;
        /// <summary>Pod Hunt blast target used when <see cref="podHuntBlastTarget"/> is 0. One BLAST - a flown missile or
        /// the bomb bursting on a rival hull, a MissileBlast-class hit - is 1 (PodHuntScoringRuleSO), summed across the
        /// domain's pilots. A first cut, unplayed.</summary>
        public const int DefaultPodHuntBlastTarget = 15;

        /// <summary>Ice Shelf round length in seconds, used when <see cref="iceShelfRoundSeconds"/> is 0. TIMED, Glasshouse's
        /// reason: the score (the mass standing in a domain's colour, as the server counts it) is a live stock rival guns
        /// and grazers eat. 90 s is the arcade's target length (Docs/ARCADE_MATCH_LENGTH.md).</summary>
        public const int DefaultIceShelfRoundSeconds = 90;

        /// <summary>Depth Charge hostile-prism target used when <see cref="depthChargePrismTarget"/> is 0. Under Rampage's
        /// 2000 in the same forest: one Orca brings three guns, but every one of them is a slower, rarer thing than a
        /// skimming hull's contact. A first cut, unplayed.</summary>
        public const int DefaultDepthChargePrismTarget = 1500;
        /// <summary>Larder missile target used when <see cref="larderMissileTarget"/> is 0. One MISSILE HIT - a missile
        /// from a Shrike's plants reaching a rival hull (a missile-class combat hit) - is one point (LarderScoringRuleSO),
        /// summed across the domain's pilots. A first cut, unplayed.</summary>
        public const int DefaultLarderMissileTarget = 16;

        /// <summary>Thornbush round length in seconds, used when <see cref="thornbushRoundSeconds"/> is 0. Thornbush is
        /// TIMED, Tapestry's reason: its score (nests + trail standing in a domain's colour) is a live stock rival
        /// missiles and lances take down, so "most standing at the whistle" is the only honest end condition.</summary>
        public const int DefaultThornbushRoundSeconds = 90;

        /// <summary>Lancefall prism target used when <see cref="lancefallPrismTarget"/> is 0. Hostile prisms (the neutral
        /// plates) a DOMAIN breaks, summed across its pilots (LancefallScoringRuleSO, Rampage's rule). The plates never
        /// grow back, so three domains' worth must fit in the smallest rung's breakable plates (author_lancefall_assets.py
        /// asserts it). A first cut, unplayed.</summary>
        public const int DefaultLancefallPrismTarget = 600;
        /// <summary>Flitstrike strike target used when <see cref="flitstrikeStrikeTarget"/> is 0. One strike - a
        /// Honeyguide's Flit (or a Charge upgrade on its trigger) reaching a rival hull - is one point
        /// (FlitstrikeScoringRuleSO). The Flit is a short dart every 2 s, so a strike is earned, not sprayed.</summary>
        public const int DefaultFlitstrikeStrikeTarget = 5;

        /// <summary>Honeycomb round length in seconds, used when <see cref="honeycombRoundSeconds"/> is 0. TIMED,
        /// Tapestry's reason: the score is mass standing in a domain's colour, a live stock rival pecks chip. 90 s is the
        /// arcade's target length (Docs/ARCADE_MATCH_LENGTH.md).</summary>
        public const int DefaultHoneycombRoundSeconds = 90;

        /// <summary>Woodpecker hostile-prism target used when <see cref="woodpeckerPrismTarget"/> is 0. A Peck burst is
        /// three shots a second at best, each one prism, so 150 is a little under a minute of steady pecking for one
        /// pilot (Sirocco's 400 is a dust that takes a third of a stand per pass).</summary>
        public const int DefaultWoodpeckerPrismTarget = 150;

        /// <summary>Starweave round length in seconds, used when <see cref="starweaveRoundSeconds"/> is 0. Starweave is
        /// TIMED: its score is a live stock (volume standing), so only the clock can end it. 90 s is the arcade's target
        /// length (Docs/ARCADE_MATCH_LENGTH.md).</summary>
        public const int DefaultStarweaveRoundSeconds = 90;

        /// <summary>Haul round length in seconds, used when <see cref="haulRoundSeconds"/> is 0. Haul is TIMED: its score
        /// is a live stock (volume standing, which a rival's ball steals back), so only the clock can end it.</summary>
        public const int DefaultHaulRoundSeconds = 90;

        /// <summary>Hedgerow round length in seconds, used when <see cref="hedgerowRoundSeconds"/> is 0. Hedgerow is TIMED:
        /// its score is a live stock (volume standing, which a rival's wall steals back), so only the clock can end it.</summary>
        public const int DefaultHedgerowRoundSeconds = 90;

        /// <summary>Longshot round length in seconds, used when <see cref="longshotRoundSeconds"/> is 0. Longshot is TIMED:
        /// a rival's trail is always there to shoot, so only the clock can end it.</summary>
        public const int DefaultLongshotRoundSeconds = 90;
        /// <summary>Sentry zap target used when <see cref="sentryZapTarget"/> is 0. One ZAP - a Meerkat's sentinel
        /// striking a rival hull in its guard (a Bullet-class combat hit, about five a second while a rival stays in
        /// it) - is one point (SentryScoringRuleSO), summed across the domain's pilots. A first cut, unplayed.</summary>
        public const int DefaultSentryZapTarget = 40;

        /// <summary>Warren round length in seconds, used when <see cref="warrenRoundSeconds"/> is 0. Warren is TIMED,
        /// Tapestry's reason: its score (the mass a domain's Meerkats hold standing, trail and exit portals) is a live
        /// stock rival solitons swallow, so "most standing at the whistle" is the only honest end condition.</summary>
        public const int DefaultWarrenRoundSeconds = 90;

        /// <summary>Cave-in prism target used when <see cref="caveInPrismTarget"/> is 0. Hostile prisms a DOMAIN breaks
        /// (Shatterplane's neutral plates, rival mass), summed across its pilots (CaveInScoringRuleSO, Rampage's rule).
        /// A first cut, unplayed: under a fifth of the smallest rung's breakable plate mass.</summary>
        public const int DefaultCaveInPrismTarget = 1000;
        /// <summary>Snap ring-hit target used when <see cref="snapHitTarget"/> is 0. One RING HIT - a vortex ring the
        /// Goby &amp; Pistol fired reaching a rival hull (Vortex Sting, a MissileDirect-class combat hit) - is one point
        /// (SnapScoringRuleSO), summed across the domain's pilots. An ESTIMATE pending a timed match.</summary>
        public const int DefaultSnapHitTarget = 5;

        /// <summary>Den Builder round length in seconds, used when <see cref="denBuilderRoundSeconds"/> is 0. TIMED,
        /// Glasshouse's reason (the score is a live stock that grazers and rival rings eat). Longer than the arcade's 90 s
        /// because a den needs an island ridden first (900-1800 u of Tiny Wings hills per den).</summary>
        public const int DefaultDenBuilderRoundSeconds = 150;

        /// <summary>Riptide hostile-prism target used when <see cref="riptidePrismTarget"/> is 0. Hostile prisms a DOMAIN
        /// breaks (Shatterplane's neutral plates, rival trails and dens), summed across its pilots (RiptideScoringRuleSO,
        /// Rampage's rule). An ESTIMATE pending a timed match; three domains' worth fits the smallest plate field
        /// (author_riptide_assets.py asserts it).</summary>
        public const int DefaultRiptidePrismTarget = 600;

        /// <summary>Marionette strike target used when <see cref="marionetteStrikeTarget"/> is 0. One STRIKE - a possessed
        /// creature's attack landing on a rival hull - is one point, and so is each hull a Charge 5 burst catches
        /// (MarionetteScoringRuleSO), summed across the domain's pilots. A first cut, unplayed.</summary>
        public const int DefaultMarionetteStrikeTarget = 12;

        /// <summary>Gorge round length in seconds, used when <see cref="gorgeRoundSeconds"/> is 0. Gorge is TIMED, Chum's
        /// reason: the reef is finite and the wild herds eat it too, so a prism target could fall out of reach.</summary>
        public const int DefaultGorgeRoundSeconds = 90;

        /// <summary>Sirocco hostile-prism target used when <see cref="siroccoPrismTarget"/> is 0.
        /// Far under Rampage's 2000: the dust takes one prism in three it touches, where a Dolphin
        /// cone takes hundreds at once.</summary>
        public const int DefaultSiroccoPrismTarget = 400;

        /// <summary>Broadside points PER PILOT used when <see cref="broadsidePointsPerPilot"/>
        /// is 0. The mixed-fleet brawl prices a hit by its VERB (BroadsideScoringRuleSO): a round
        /// is 1, a contact strike 8, an area debuff 12, a rocket 10/20/30 by how close it got.
        ///
        /// <para>Unlike every other target on this asset this one is a RATE, not a total: the
        /// number a domain actually races to scales with how many pilots are on a side (see
        /// <see cref="BroadsideExtraPilotFraction"/>), because a second pilot roughly doubles a
        /// domain's scoring rate - the combat latch is per shooter-victim pair, so two pilots
        /// working one victim really do both score - and a fixed total would make a 4v4 a third
        /// the length of a 1v1. Modelled in Tools/Build/broadside_balance.py, which reads BOTH
        /// constants off this file rather than restating them.</para></summary>
        public const int DefaultBroadsidePointsPerPilot = 100;

        /// <summary>How much of a whole pilot's worth of target each pilot AFTER the first adds:
        /// <c>target = perPilot x (1 + this x (teamSize - 1))</c>, so 100 / 160 / 220 / 280 for a
        /// 1 / 2 / 3 / 4 pilot team.
        ///
        /// <para>Deliberately below 1: at 1.0 the target would rise exactly as fast as the team's
        /// rate and match length would be flat, which reads as a teammate contributing nothing.
        /// At 0.6 a fuller side finishes somewhat sooner - filling your team is a real advantage,
        /// stated rather than hidden - while a lone pilot is still asked for a number one hull
        /// can reach.</para></summary>
        public const float BroadsideExtraPilotFraction = 0.6f;

        /// <summary>Tandava's shatter threshold used when <see cref="tandavaBreakPercent"/> is 0: the pilots win when the
        /// swarm's body - whatever form it wears - is cut below this percent of that form's full body (Assets/_Scripts/
        /// Controller/Arcade/TANDAVA.md). The match also ends when the pilots break the halo, wipe it out or starve it
        /// (they win) or the Antlion eats its last feast (they lose); there is no clock.</summary>
        public const int DefaultTandavaBreakPercent = 35;

        /// <summary>Tandava's halo used when <see cref="tandavaHaloRingsToBreak"/> is 0: the pilots win when they break
        /// this many of the twelve halo rings round the Lord of the Dance before its drum stops (TANDAVA.md §3.6).</summary>
        public const int DefaultTandavaHaloRingsToBreak = 9;

        /// <summary>Switchback course length used when <see cref="switchbackGateTarget"/> is 0
        /// (auto/default). It is BOTH the end-game target and the number of gates the course is
        /// built with - SwitchbackController reads this same getter - so the two cannot drift.</summary>
        public const int DefaultSwitchbackGateTarget = 26;

        /// <summary>Waystation race length used when <see cref="waystationClusterTarget"/> is 0 - CLUSTERS, not rings:
        /// the race is that many clusters with a Drop Tower between each pair but the last (Docs/Studios/TIGER_MOTH_TIME.md).
        /// Five gives every intensity three towers and the long wormhole drop as the finale. The ring count the race ends
        /// on is the course's own (towers vary with the gaps), which <c>RaceGateTurnMonitor</c> takes from the laid course.
        /// Replaced the 15-ring target when the Drop Tower course replaced the cluster-only one (2026-11-01).</summary>
        public const int DefaultWaystationClusterTarget = 5;

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

        /// <summary>Skein course length used when <see cref="skeinRingTarget"/> is 0. Read
        /// through <c>SkeinController.AuthoredGateTarget</c> by <c>RaceGateTurnMonitor</c> (the
        /// target), by <c>SkeinController</c> (how many rings to lay) and by
        /// <c>SpawnableSkein</c> (the ring count its cable is built and re-rolled for), so the
        /// course, the arena and the number counting it cannot drift. Must equal
        /// <c>SkeinCourseSettings.ForIntensity</c>'s GateCount and skein_budget.py's GATE_COUNT,
        /// the count the ring spacing is proven at.</summary>
        public const int DefaultSkeinRingTarget = 24;

        /// <summary>Headlong RACE length used when <see cref="headlongGateTarget"/> is 0 - gate
        /// threadings, i.e. laps x rings. 40 = five laps of the shipped eight-gate circuit (three
        /// until 2026-10-26: the ring count only spaces a fixed loop, so laps are the length lever).
        /// Read by <c>RaceGateTurnMonitor</c> (through the controller) for the target and by
        /// <c>HeadlongController</c> to size the circuit, so the two cannot drift.</summary>
        public const int DefaultHeadlongGateTarget = 40;

        /// <summary>Redline RACE length used when <see cref="redlineGateTarget"/> is 0 - gate
        /// threadings, i.e. laps x rings. 64 = eight laps of the shipped eight-gate circuit (five
        /// until 2026-11-11, when Soar was lit from the gun and a lap fell from ~28 s to ~5-11 s;
        /// three until 2026-10-26). Read by <c>RaceGateTurnMonitor</c> (through the controller) for the
        /// target and by <c>RedlineController</c> to size the circuit, so the two cannot drift.</summary>
        public const int DefaultRedlineGateTarget = 64;

        /// <summary>Grizzly Time RACE length used when <see cref="grizzlyTimeGateTarget"/> is 0 -
        /// gate threadings, i.e. laps x rings. 16 = two laps of the shipped eight-gate circuit
        /// (24 = three laps until 2026-10-26; 28 = two laps of fourteen until the bomb launch was
        /// tripled, 2026-10-08).
        /// Read by <c>GrizzlyTimeController</c> both to size the circuit and (through
        /// it) by the turn monitor for the finish line, so the two cannot drift.</summary>
        public const int DefaultGrizzlyTimeGateTarget = 16;

        /// <summary>Slingshot RACE length used when <see cref="slingshotGateTarget"/> is 0 - gate
        /// threadings, i.e. laps x rings. 16 = two laps of the shipped eight-gate circuit (the
        /// Stoat cruises at 60 u/s, so two laps of the race shell is already a long race).
        /// Read by <c>SlingshotController</c> both to size the circuit and (through it) by the
        /// turn monitor for the finish line, so the two cannot drift.</summary>
        public const int DefaultSlingshotGateTarget = 16;

        /// <summary>Warpline RACE length used when <see cref="warplineGateTarget"/> is 0 - gate
        /// threadings, i.e. laps x rings. 10 = two laps of a five-ring circuit: few rings and long
        /// legs, so the race is won on the warp (the Stoat's pathfinder boost) between them.
        /// Read by <c>WarplineController</c> both to size the circuit and (through it) by the
        /// turn monitor for the finish line, so the two cannot drift.</summary>
        public const int DefaultWarplineGateTarget = 10;

        /// <summary>Thresher Time RACE length used when <see cref="thresherTimeGateTarget"/> is 0 -
        /// gate threadings, i.e. laps x rings. 16 = two laps of the shipped eight-gate circuit, three
        /// until 2026-10-26 (Grizzly Time's lap count: the Thresher's full-sling circle, 229 u, is within 8% of the
        /// Grizzly's full-launch one, so the race is cut on the same octagon).
        /// Read by <c>ThresherTimeController</c> both to size the circuit and (through it) by the
        /// turn monitor for the finish line, so the two cannot drift.</summary>
        public const int DefaultThresherTimeGateTarget = 16;

        /// <summary>Wrasse Run RACE length used when <see cref="wrasseRunGateTarget"/> is 0 - gate
        /// threadings, i.e. laps x rings. 16 = two laps of an eight-ring circuit: a ring counts only when
        /// the Wrasse's kite threads it, at the Grouper's 60 u/s cruise plus the boosts Time loops save.
        /// Read by <c>WrasseRunController</c> both to size the circuit and (through it) by the turn
        /// monitor for the finish line, so the two cannot drift.</summary>
        public const int DefaultWrasseRunGateTarget = 16;

        /// <summary>Burrow Run RACE length used when <see cref="burrowRunGateTarget"/> is 0 - gate
        /// threadings, i.e. laps x rings. 16 = two laps of an eight-ring circuit, the Vessel Studio's Meerkat race.
        /// Read by <c>BurrowRunCourseSource</c> both to size the circuit and (through it) by the turn
        /// monitor for the finish line, so the two cannot drift.</summary>
        public const int DefaultBurrowRunGateTarget = 16;

        /// <summary>Scarab Rally RACE length used when <see cref="scarabRallyGateTarget"/> is 0 - gate threadings - an OPEN chain flown once (Switchback's shape), so this is the number of rings laid. 24 ~ a 90 s experienced run at the Scarab's 216 u/s.
        /// Read by <c>ScarabRallyController</c> to size the course and, through it, by the turn monitor.</summary>
        public const int DefaultScarabRallyGateTarget = 24;

        /// <summary>Serpent Time RACE length used when <see cref="serpentTimeGateTarget"/> is 0 - gate threadings, i.e. laps x rings. 16 = two laps of the eight-gate circuit.
        /// Read by <c>SerpentTimeController</c> to size the course and, through it, by the turn monitor.</summary>
        public const int DefaultSerpentTimeGateTarget = 16;

        /// <summary>Brittle Star Time RACE length used when <see cref="brittleStarTimeGateTarget"/> is 0 - gate threadings, i.e. laps x rings. 16 = two laps of the eight-gate circuit.
        /// Read by <c>BrittleStarTimeController</c> to size the course and, through it, by the turn monitor.</summary>
        public const int DefaultBrittleStarTimeGateTarget = 16;

        /// <summary>LanternRun RACE length used when <see cref="lanternRunGateTarget"/> is 0 - gate threadings, i.e. laps x rings. 16 = two laps of the eight-gate circuit.
        /// Read by <c>LanternRunController</c> to size the course and, through it, by the turn monitor.</summary>
        public const int DefaultLanternRunGateTarget = 16;

        /// <summary>Thornline RACE length used when <see cref="thornlineGateTarget"/> is 0 - gate threadings - an OPEN chain
        /// of thorns flown once, a ring at the middle of every leg. 16 ~ a 50-65 s run of the Shrike's one-second slings and
        /// their reloads. The course tests prove the chain at 16 (THORNLINE.md).
        /// Read by <c>ThornlineCourseSource</c> to size the course and, through it, by the turn monitor.</summary>
        public const int DefaultThornlineGateTarget = 16;

        /// <summary>Spearline RACE length used when <see cref="spearlineGateTarget"/> is 0 - gate threadings: 3 laps of
        /// Headlong's 8-ring circuit, the rings sealed (SPEARLINE.md). Read by <c>SpearlineCourseSource</c>.</summary>
        public const int DefaultSpearlineGateTarget = 24;

        /// <summary>Decoy Run RACE length used when <see cref="decoyRunGateTarget"/> is 0 - rings threaded in order,
        /// flown once. The course tests prove the course at 12 (DECOYRUN.md).</summary>
        public const int DefaultDecoyRunGateTarget = 12;

        /// <summary>Breach RACE length used when <see cref="breachGateTarget"/> is 0 - rings threaded in order,
        /// flown once. The course tests prove the course at 16 (BREACH.md).</summary>
        public const int DefaultBreachGateTarget = 16;

        /// <summary>Slip Line RACE length used when <see cref="slipLineGateTarget"/> is 0 - rings threaded in order,
        /// flown once. The course tests prove the course at 16 (SLIPLINE.md).</summary>
        public const int DefaultSlipLineGateTarget = 16;

        /// <summary>Perch Run RACE length used when <see cref="perchRunGateTarget"/> is 0 - rings threaded in order,
        /// flown once. The course tests prove the course at 16 (PERCHRUN.md).</summary>
        public const int DefaultPerchRunGateTarget = 16;

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
        [Min(0)] public int crystalCaptureCrystalCount = 14;

        [Tooltip("Joust collisions to end the turn. 0 = default (2).")]
        [Min(0)] public int joustCount = 2;

        [Tooltip("Maelstrom (Maelstrom) placement points a domain needs to win the whole shuffle " +
                 "(race to N). 0 = default (6).")]
        [Min(0)] public int maelstromWinTarget = 6;

        [Tooltip("Nucleus Rush (Brood Rush) fauna waves a domain must claim to win (race to N; one " +
                 "wave every 30s spawn cycle, so 3 ≈ a 1.5–2.5 minute match). 0 = default (3).")]
        [Min(0)] public int nucleusRushWaveTarget = 3;

        [Tooltip("Rampage: hostile prisms (another domain's mass) a domain must destroy to win " +
                 "(race to N). 0 = default (2000). The per-intensity list below overrides it.")]
        [Min(0)] public int rampagePrismTarget = 2000;

        [Tooltip("Rampage: per-INTENSITY override of the target above - element 0 is intensity 1. " +
                 "Empty, a 0 entry, or an intensity past the end falls back to the scalar. The " +
                 "forest shrinks five-fold up the ladder (about 59,600 prisms at intensity 1 against " +
                 "11,900 at 4) and the Dolphin's blast is gated by crystals that grow scarce, so one " +
                 "number made a 4 several times longer than a 1.")]
        public List<int> rampagePrismTargetByIntensity = new() { 2000, 1700, 1300, 1000 };

        [Tooltip("Cleave: hostile prisms a domain must DESTROY to win (race to N) - cage bars, " +
                 "rival trails and fauna bodies all count; your own team's trail never does. The " +
                 "25%/50% fauna-release rungs are fractions of THIS, so moving it moves the whole " +
                 "escalation ladder with it. 0 = default (1000).")]
        [Min(0)] public int cleavePrismTarget = 1000;

        [Tooltip("Cleave: per-INTENSITY override of the target above - element 0 is intensity 1. " +
                 "Empty, a 0 entry, or an intensity past the end falls back to the scalar. It " +
                 "exists because this mode's intensities are four different PLACES rather than " +
                 "four sizes of one: 1 and 2 are vast 2,160-radius arenas you cross, 3 and 4 are " +
                 "compact 720-radius objects you peel. Every rung is made of the same small " +
                 "prisms, so the counts are comparable and the target is what says how much of a " +
                 "place a match asks you to get through.")]
        public List<int> cleavePrismTargetByIntensity = new() { 950, 950, 1000, 1000 };

        [Tooltip("Wildlife Liberation: creatures a domain must kill between them to win " +
                 "(race to N), summed across that domain's players like every other target " +
                 "here. 0 = default (30).")]
        [Min(0)] public int wildlifeKillTarget = 30;

        [Tooltip("Dog Fight points a DOMAIN needs to win. Points come from landed gunnery: a " +
                 "bullet hit scores 1 and a rocket scores its best tier - shockwave 10, blast 20, " +
                 "direct strike 30 - so this target reads as 'either 40 bullets or four rocket " +
                 "shockwaves, or any mix'. Teammates pool - Dog Fight is a team race, not a " +
                 "free-for-all. 0 = default (40).")]
        [Min(0)] public int dogFightPointTarget = 40;

        [Tooltip("Grizzly Charge points a DOMAIN needs to win - Dog Fight's price list, where a " +
                 "Grizzly blast landed on a rival is a rocket's 20-point blast tier and a round is 1. " +
                 "Its own key, so Dog Fight's target can move without it. 0 = default (90).")]
        [Min(0)] public int grizzlyChargePointTarget = 90;

        [Tooltip("The Bends: BENDS a DOMAIN needs to win - opposing pilots caught in your " +
                 "Dolphin crystal blast, one point each. Race to 3, like Joust: three clean " +
                 "hits, or one blast that catches a pair plus one more. Teammates pool. " +
                 "0 = default (3).")]
        [Min(0)] public int bendsPointTarget = 3;
        [Tooltip("Scarab Scramble goals a DOMAIN needs to win (race to N). A goal = one of your " +
                 "domain's forged balls threaded through any hoop; teammates pool. With abundant " +
                 "crystals and continuous play, 10 was meant as a 3-5 minute party match; editor " +
                 "matches had not reached it after 148 s and 748 s, so 3. 0 = default (3).")]
        [Min(0)] public int scarabScrambleGoalTarget = 3;
        [Tooltip("Astro League: goals a DOMAIN needs to end the match early (the mercy rule). " +
                 "The match is otherwise TIMED (AstroLeagueSettingsSO.matchDurationSeconds), with " +
                 "golden-goal overtime on a tie, so this caps a blowout rather than setting the " +
                 "length. 0 = default (5).")]
        [Min(0)] public int astroLeagueGoalLimit = 5;
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
                 "~1,848 of them hostile to any one domain), so 500 leaves the yard far from " +
                 "exhausted at the whistle. 0 = default (500).")]
        [Min(0)] public int hijackStealTarget = 500;

        [Tooltip("Switchback: gates in the course, which is both how many a pilot must thread " +
                 "to finish and how many rings are laid. Compared against a domain's LEAD " +
                 "RUNNER, not a sum - every pilot flies the same course, so a teammate does not " +
                 "shorten it. 0 = default (26).")]
        [Min(0)] public int switchbackGateTarget = 26;

        [Tooltip("Waystation: CLUSTERS in the race, with a Drop Tower in every gap but the last. The finish line is " +
                 "every ring the course lays, towers included, measured against a domain's LEAD RUNNER. 0 = default (5).")]
        [Min(0)] public int waystationClusterTarget = 5;

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
                 "and the size of the circuit. 40 = five laps of eight. 0 uses the default.")]
        [Min(0)] public int headlongGateTarget = 40;

        [Tooltip("Redline: gate threadings that win the race - LAPS x RINGS, not rings. The " +
                 "controller lays target/laps rings, so this one number is both the finish line " +
                 "and the size of the circuit. 64 = eight laps of eight. 0 uses the default.")]
        [Min(0)] public int redlineGateTarget = 64;

        [Tooltip("Grizzly Time: gate threadings that win the race - LAPS x RINGS, not rings. The " +
                 "controller lays target/laps rings, so this one number is both the finish line " +
                 "and the size of the circuit. 16 = two laps of eight. 0 uses the default.")]
        [Min(0)] public int grizzlyTimeGateTarget = 16;

        [Tooltip("Slingshot: gate threadings that win the race - LAPS x RINGS, not rings. The " +
                 "controller lays target/laps rings, so this one number is both the finish line " +
                 "and the size of the circuit. 16 = two laps of eight. 0 uses the default.")]
        [Min(0)] public int slingshotGateTarget = 16;

        [Tooltip("Warpline: gate threadings that win the race - LAPS x RINGS, not rings. The " +
                 "controller lays target/laps rings, so this one number is both the finish line " +
                 "and the size of the circuit. 10 = two laps of five. 0 uses the default.")]
        [Min(0)] public int warplineGateTarget = 10;

        [Tooltip("Thresher Time: gate threadings that win the race - LAPS x RINGS, not rings. The " +
                 "controller lays target/laps rings, so this one number is both the finish line " +
                 "and the size of the circuit. 16 = two laps of eight. 0 uses the default.")]
        [Min(0)] public int thresherTimeGateTarget = 16;

        [Tooltip("Wrasse Run: gate threadings that win the race - LAPS x RINGS, not rings. The " +
                 "controller lays target/laps rings, so this one number is both the finish line " +
                 "and the size of the circuit. 16 = two laps of eight. 0 uses the default.")]
        [Min(0)] public int wrasseRunGateTarget = 16;

        [Tooltip("Burrow Run: gate threadings that win the race - LAPS x RINGS, not rings. The " +
                 "controller lays target/laps rings, so this one number is both the finish line " +
                 "and the size of the circuit. 16 = two laps of eight. 0 uses the default.")]
        [Min(0)] public int burrowRunGateTarget = 16;
        [Tooltip("Scarab Rally: gate threadings - an OPEN chain flown once (Switchback's shape), so this is the number of rings laid. 24 ~ a 90 s experienced run at the Scarab's 216 u/s. 0 uses the default.")]
        [Min(0)] public int scarabRallyGateTarget = 24;

        [Tooltip("Serpent Time: gate threadings, i.e. laps x rings. 16 = two laps of the eight-gate circuit. 0 uses the default.")]
        [Min(0)] public int serpentTimeGateTarget = 16;

        [Tooltip("Brittle Star Time: gate threadings, i.e. laps x rings. 16 = two laps of the eight-gate circuit. 0 uses the default.")]
        [Min(0)] public int brittleStarTimeGateTarget = 16;

        [Tooltip("Lantern Run race length in gate threadings (laps x rings). 0 = use the default (16).")]
        [Min(0)] public int lanternRunGateTarget = 16;

        [Tooltip("Thornline race length in rings (an open chain of thorns, one ring a leg). 0 = use the default (16). " +
                 "The course is proven at 16; another count re-spaces the snipes.")]
        [Min(0)] public int thornlineGateTarget = 16;

        [Tooltip("Spearline race length in rings (3 laps x 8 rings of Headlong's circuit, sealed). 0 = use the default (24).")]
        [Min(0)] public int spearlineGateTarget = 24;

        [Tooltip("Decoy Run race length in rings (flown once). 0 = use the default (12). The course is proven at 12.")]
        [Min(0)] public int decoyRunGateTarget = 12;

        [Tooltip("Breach race length in rings (flown once). 0 = use the default (16). The course is proven at 16.")]
        [Min(0)] public int breachGateTarget = 16;

        [Tooltip("Slip Line race length in rings (flown once). 0 = use the default (16). The course is proven at 16.")]
        [Min(0)] public int slipLineGateTarget = 16;

        [Tooltip("Perch Run race length in rings (flown once). 0 = use the default (16). The course is proven at 16.")]
        [Min(0)] public int perchRunGateTarget = 16;


        [Tooltip("Regatta: gate threadings that win the race - LAPS x RINGS, not rings. The " +
                 "arena lays eight rings a lap with the rails threaded through them, so this " +
                 "must be a multiple of eight; the controller races laps = target / 8. 24 = " +
                 "three laps. 0 uses the default.")]
        [Min(0)] public int regattaGateTarget = 24;

        [Tooltip("TOLLWAY - how many TOLLS a domain must collect to win. A toll is any ball " +
                 "threading a ring one of that domain's pilots planted, so the count is a " +
                 "DOMAIN sum and teammates pool. Higher than a Joust race and lower than a " +
                 "goal race: a ring must be planted, survive, and be threaded, which is " +
                 "slower than shooting at a net and faster than tearing down a wreck. 0 = default (3).")]
        [Min(0)] public int tollwayTollTarget = 3;

        [Tooltip("Wrecking Ball: hostile prisms (the court forest, rival trails, fauna bodies) a " +
                 "DOMAIN must destroy between them to win (race to N) - with the ball OR the " +
                 "cavitation plate; your own team's mass never counts. 0 = default (650).")]
        [Min(0)] public int wreckingBallPrismTarget = 650;

        [Tooltip("Undertow: POINTS a DOMAIN needs to win (race to N). A bend - an opposing pilot " +
                 "caught in your cavitation plate - is 3 points and a creature the plate kills is " +
                 "1, summed across the domain's pilots. 0 = default (12).")]
        [Min(0)] public int undertowPointTarget = 12;

        [Tooltip("Dustup: POINTS a DOMAIN needs to win (race to N). One dusting - an opposing " +
                 "Butterfly passing through your Scale Dust - is one point, summed across the " +
                 "domain's pilots. 0 = default (4).")]
        [Min(0)] public int dustupPointTarget = 4;

        [Tooltip("Hood: SNIPES a DOMAIN needs to win (race to N). One Sniper Shot that " +
                 "strips a rival pilot is one snipe, summed across the domain's pilots. 0 = default (3).")]
        [FormerlySerializedAs("serpentGulagSnipeTarget")]
        [Min(0)] public int serpentHoodSnipeTarget = 3;

        [Tooltip("Jackstraw: STRIKES a DOMAIN needs to win (race to N). The Thresher's ball or chain reaching " +
                 "a rival hull is one strike, summed across the domain's pilots. 0 = default (3).")]
        [Min(0)] public int jackstrawStrikeTarget = 3;

        [Tooltip("Riposte: STRIKES a DOMAIN needs to win (race to N). A Swordfish sword hit on a rival hull is one " +
                 "strike, summed across the domain's pilots. 0 = default (6).")]
        [Min(0)] public int riposteStrikeTarget = 6;
        [Tooltip("Bullrush: GORES a DOMAIN needs to win (race to N), the Rhino's Charge game. A gore - ramming a rival " +
                 "while faster than it and above the gore speed - is one point, summed across the domain's pilots. " +
                 "0 = default (5).")]
        [Min(0)] public int bullrushGoreTarget = 5;
        [Tooltip("Windrow: round length in SECONDS, the Rhino's Mass game. Timed - the domain with the most trail " +
                 "STANDING at the whistle wins. 0 = default (90).")]
        [Min(0)] public int windrowRoundSeconds = 90;
        [Tooltip("Landslide: hostile prisms a DOMAIN's plough strikes must destroy to win (race to N), the Rhino's " +
                 "Space game in Cleave's arenas. 0 = default (500).")]
        [Min(0)] public int landslidePrismTarget = 500;
        [Tooltip("Flatline: POINTS a DOMAIN needs to win (race to N), the Brittle Star's charge game. A round " +
                 "on a rival hull is 1 point, a cord or thrown-saber Strike 8 (the fleet price, ten points to " +
                 "the petal); the ring blast scores nothing. 0 = default (90). The per-intensity list below " +
                 "overrides it.")]
        [Min(0)] public int flatlinePointTarget = 90;

        [Tooltip("Flatline: per-INTENSITY override of the target above - element 0 is intensity 1. Empty, a 0 " +
                 "entry, or an intensity past the end falls back to the scalar. 90 at every rung (Shombith, " +
                 "2026-11-10); a rung can be re-tuned on its own once timed matches say so.")]
        public List<int> flatlinePointTargetByIntensity = new() { 90, 90, 90, 90 };

        [Tooltip("Kelpwood: BITES a DOMAIN needs to win (race to N). A creature an Angler locked on a rival biting " +
                 "its hull is one bite, summed across the domain's pilots. 0 = default (12).")]
        [Min(0)] public int kelpwoodBiteTarget = 12;

        [Tooltip("Corona: BLOOM HITS a DOMAIN needs to win (race to N). A rival pilot caught in a bloom of a bomb a " +
                 "Manta planted is one bloom hit, summed across the domain's pilots. 0 = default (15).")]
        [Min(0)] public int coronaBloomTarget = 15;

        [Tooltip("Shockfront: RIPPLE HITS a DOMAIN needs to win (race to N). A rival pilot stripped by the front of a " +
                 "Stoat's annihilation ripple is one hit, summed across the domain's pilots. 0 = default (10).")]
        [Min(0)] public int shockfrontRippleTarget = 10;

        [Tooltip("Fulmen: hostile prisms a DOMAIN must destroy between them to win (race to N) - Rampage's forest burnt " +
                 "by the Stoat's omni-crystal bolts; your own colour never counts. 0 = default (1200).")]
        [Min(0)] public int fulmenPrismTarget = 1200;
        [Tooltip("Railrush: spike HITS a DOMAIN needs to win (race to N), the Urchin's charge game. A chain spike " +
                 "reaching a rival hull is one hit, summed across the domain's pilots. 0 = default (20).")]
        [Min(0)] public int railrushHitTarget = 20;
        [Tooltip("Standoff: BLAST HITS a DOMAIN needs to win (race to N). A rival pilot caught in a Grizzly's blast " +
                 "(a trigger bomb or the charged cannon) is one blast hit, summed across the domain's pilots. 0 = " +
                 "default (10).")]
        [Min(0)] public int standoffBlastTarget = 10;

        [Tooltip("Harvest: hostile prisms (other domains' sheaves and neutral ones, rival trails, fauna) a DOMAIN " +
                 "must destroy between them to win (race to N) - with the Thresher's chain, ball or blast; your own " +
                 "colour never counts. 0 = default (750).")]
        [Min(0)] public int harvestPrismTarget = 750;
        [Tooltip("Shatterplane: hostile PRISMS a DOMAIN must break to win (race to N), the Brittle Star's space game. The " +
                 "plates are neutral, so every plate tile a round, a cord or a ring breaks counts; ribs and screens never " +
                 "break. 0 = default (1600).")]
        [Min(0)] public int shatterplanePrismTarget = 1600;

        [Tooltip("Shatterplane: per-INTENSITY override of the target above - element 0 is intensity 1. Empty, a 0 " +
                 "entry or a missing rung falls back to the scalar.")]
        public List<int> shatterplanePrismTargetByIntensity = new() { 1600, 1600, 2000, 1000 };

        [Tooltip("Stingherd: STINGS a DOMAIN needs to win (race to N). A danger creature of a Jacks' herd touching a " +
                 "rival hull is one sting, summed across the domain's pilots. 0 = default (12).")]
        [Min(0)] public int stingherdStingTarget = 12;

        [Tooltip("Trawl: hostile prisms a DOMAIN must destroy between them to win (race to N) - a prism the Jacks' net " +
                 "drags that runs into another destroys both; your own colour never counts. 0 = default (300).")]
        [Min(0)] public int trawlPrismTarget = 300;

        [Tooltip("Tapestry: round length in SECONDS. The mode is timed - the domain with the most " +
                 "prism volume STANDING when the clock runs out wins. 0 = default (90).")]
        [Min(0)] public int tapestryRoundSeconds = 90;

        [Tooltip("Palisade: round length in SECONDS. Timed - the domain with the most Swordfish mass (trail and " +
                 "afterimages) STANDING when the clock runs out wins. 0 = default (90).")]
        [Min(0)] public int palisadeRoundSeconds = 90;

        [Tooltip("Glasshouse: round length in SECONDS. The mode is timed - the domain with the most mass standing in " +
                 "its own colour (its Anglers' trail plus the garden grown from their seeds) when the clock runs out " +
                 "wins. 0 = default (90).")]
        [Min(0)] public int glasshouseRoundSeconds = 90;

        [Tooltip("Chum: round length in SECONDS, the Angler's space game. The mode is timed - the domain whose pilots " +
                 "took the most prisms off the board (their schools' meals off the reef plus their hulls) when the clock " +
                 "runs out wins. 0 = default (90).")]
        [Min(0)] public int chumRoundSeconds = 90;
        [Tooltip("Pod Hunt: BLASTS a DOMAIN needs to win (race to N). A crew's flown missile or bomb bursting on a rival " +
                 "hull is 1. 0 = default (15).")]
        [Min(0)] public int podHuntBlastTarget = 15;

        [Tooltip("Ice Shelf: round length in SECONDS. The mode is timed - the domain with the most mass standing in its " +
                 "own colour (trail, pen strokes and shields, as the server counts it) when the clock runs out wins. " +
                 "0 = default (90).")]
        [Min(0)] public int iceShelfRoundSeconds = 90;

        [Tooltip("Depth Charge: hostile prisms (Rampage's forest, rival mass) a DOMAIN must destroy to win (race to N); " +
                 "your own team's mass never counts. 0 = default (1500).")]
        [Min(0)] public int depthChargePrismTarget = 1500;
        [Tooltip("Larder: MISSILE HITS a DOMAIN needs to win (race to N), the Shrike's charge game. A missile from a " +
                 "Shrike's plants reaching a rival hull is one hit, summed across the domain's pilots. 0 = default (16).")]
        [Min(0)] public int larderMissileTarget = 16;

        [Tooltip("Thornbush: round length in SECONDS, the Shrike's mass game. The mode is timed - the domain with the " +
                 "most mass standing in its own colour (nests plus trail) when the clock runs out wins. 0 = default (90).")]
        [Min(0)] public int thornbushRoundSeconds = 90;

        [Tooltip("Lancefall: hostile PRISMS a DOMAIN must break to win (race to N), the Shrike's space game. The plates " +
                 "are neutral, so every plate tile the Void Lance breaks counts. 0 = default (600).")]
        [Min(0)] public int lancefallPrismTarget = 600;
        [Tooltip("Flitstrike: STRIKES a DOMAIN needs to win (race to N). One strike - a Honeyguide's Flit (or a Charge " +
                 "upgrade on its left trigger) reaching a rival hull - is 1. 0 = default (5).")]
        [Min(0)] public int flitstrikeStrikeTarget = 5;

        [Tooltip("Honeycomb: round length in SECONDS. The mode is timed - the domain with the most mass standing in its " +
                 "own colour (trail and hive comb) when the clock runs out wins. 0 = default (90).")]
        [Min(0)] public int honeycombRoundSeconds = 90;

        [Tooltip("Woodpecker: hostile prisms (Rampage's cactus forest, rival trails, fauna bodies) a DOMAIN must break " +
                 "with the Honeyguide's pecks and Space upgrades to win (race to N). 0 = default (150).")]
        [Min(0)] public int woodpeckerPrismTarget = 150;

        [Tooltip("Starweave: round length in SECONDS, the Brittle Star's mass game. The mode is timed - the domain " +
                 "with the most volume standing in its colour when the clock runs out wins. 0 = default (90).")]
        [Min(0)] public int starweaveRoundSeconds = 90;

        [Tooltip("Haul: round length in SECONDS, the Thresher's mass game. The mode is timed - the domain with the most " +
                 "volume standing in its colour (stolen bales, stolen rival mass and its own trail) wins. 0 = default (90).")]
        [Min(0)] public int haulRoundSeconds = 90;

        [Tooltip("Hedgerow: round length in SECONDS, the Serpent's mass game. The mode is timed - the domain with the most " +
                 "volume standing in its colour (walled hedges, walled rival mass and its own trail) wins. 0 = default (90).")]
        [Min(0)] public int hedgerowRoundSeconds = 90;

        [Tooltip("Longshot: round length in SECONDS, the Serpent's space game. The mode is timed - the domain that broke the " +
                 "most hostile prisms (lance prisms and rivals' mass) wins. 0 = default (90).")]
        [Min(0)] public int longshotRoundSeconds = 90;
        [Tooltip("Sentry: ZAPS a DOMAIN needs to win (race to N). A Meerkat's sentinel striking a rival hull in its " +
                 "guard is one zap (about five a second while the rival stays in it), summed across the domain's " +
                 "pilots. 0 = default (40).")]
        [Min(0)] public int sentryZapTarget = 40;

        [Tooltip("Warren: round length in SECONDS. The mode is timed - the domain with the most mass standing in its own " +
                 "colour (its Meerkats' trail and exit portals) when the clock runs out wins. 0 = default (90).")]
        [Min(0)] public int warrenRoundSeconds = 90;

        [Tooltip("Cave-in: hostile prisms (Shatterplane's neutral plates, rival mass) a DOMAIN must break to win (race " +
                 "to N); a swallowed prism fired into another breaks both. 0 = default (1000).")]
        [Min(0)] public int caveInPrismTarget = 1000;
        [Tooltip("Cleaner Ram: RAMS a DOMAIN needs to win (race to N). The Wrasse's charged kite reaching a rival hull " +
                 "is one ram, summed across the domain's pilots. 0 = default (8).")]
        [Min(0)] public int cleanerRamStrikeTarget = 8;

        [Tooltip("Reefbuild: round length in SECONDS. The mode is timed - the domain with the most prism volume " +
                 "STANDING (Mass clusters and trail) when the clock runs out wins. 0 = default (90).")]
        [Min(0)] public int reefbuildRoundSeconds = 90;

        [Tooltip("Blastline: hostile prisms (Rampage's forest, rival trails and clusters, fauna) a DOMAIN must destroy " +
                 "between them to win (race to N) - with the Wrasse's Space blasts and kite pops; your own colour " +
                 "never counts. 0 = default (600).")]
        [Min(0)] public int blastlinePrismTarget = 600;
        [Tooltip("Snap: RING HITS a DOMAIN needs to win (race to N), the Goby & Pistol's Charge game. A vortex ring the " +
                 "Pistol fired reaching a rival hull is one hit, summed across the domain's pilots. 0 = default (5).")]
        [Min(0)] public int snapHitTarget = 5;

        [Tooltip("Den Builder: round length in SECONDS, the Goby & Pistol's Mass game. Timed - the domain with the most " +
                 "mass standing in its colour (dens, perches, the islands' trail) when the clock runs out wins. " +
                 "0 = default (150).")]
        [Min(0)] public int denBuilderRoundSeconds = 150;

        [Tooltip("Riptide: hostile PRISMS a DOMAIN must break to win (race to N), the Goby & Pistol's Space game - the " +
                 "plates' tiles the undertow pulls into the Pistol's plane and its rings smash, rival trails and dens. " +
                 "0 = default (600).")]
        [Min(0)] public int riptidePrismTarget = 600;

        [Tooltip("Marionette: STRIKES a DOMAIN needs to win (race to N), the Jewel Wasp's charge game. A possessed " +
                 "creature's attack landing on a rival hull is one, and so is each hull a Charge 5 burst catches. " +
                 "0 = default (12).")]
        [Min(0)] public int marionetteStrikeTarget = 12;

        [Tooltip("Gorge: round length in SECONDS, the Jewel Wasp's space game. The mode is timed - the domain that took " +
                 "the most prisms off the board (sucked in by the creatures they rode, or broken by their hulls) when " +
                 "the clock runs out wins. 0 = default (90).")]
        [Min(0)] public int gorgeRoundSeconds = 90;

        [Tooltip("Sirocco: hostile prisms (Rampage's cactus forest, rival trails, fauna bodies) a " +
                 "DOMAIN must destroy with the Scale Dust to win (race to N); your own team's " +
                 "mass never counts. 0 = default (400).")]
        [Min(0)] public int siroccoPrismTarget = 400;

        [Tooltip("Broadside: points PER PILOT on a domain. The number a domain actually races " +
                 "to is this x (1 + 0.6 x (teamSize - 1)) - 100/160/220/280 for a 1/2/3/4 pilot " +
                 "team - because a second pilot roughly doubles a side's scoring rate. Every " +
                 "hull on the card scores into one total, priced by the VERB that landed the " +
                 "hit: a round is 1, a contact strike 8, an area debuff 12, a rocket 10/20/30. " +
                 "0 = default (100).")]
        [Min(0)] public int broadsidePointsPerPilot = 100;

        [Tooltip("Tandava: the pilots win when the swarm's body is cut below this PERCENT of its form's full body (it " +
                 "shatters). They also win by breaking the halo, wiping it out or starving it (there is no clock), and " +
                 "lose if the Antlion eats its last feast. 0 = default (35).")]
        [Range(0, 100)] public int tandavaBreakPercent = 35;

        [Tooltip("Tandava: halo rings (twelve round the Lord of the Dance) the pilots must break before its drum stops to " +
                 "break the dance and win. Clamped to the halo's ring count. 0 = default (9).")]
        [FormerlySerializedAs("tandavaFlamesToBreak")]
        [Range(0, 12)] public int tandavaHaloRingsToBreak = 9;


        [Header("Build baseline - what a shipping build uses. Set via the tool's \"Set Build Values\" button.")]
        [Min(0)] public int hexRaceCrystalCountBuild = 0;
        [Min(0)] public int crystalCaptureCrystalCountBuild = 14;
        [Min(0)] public int joustCountBuild = 2;
        [Min(0)] public int maelstromWinTargetBuild = 6;
        [Min(0)] public int nucleusRushWaveTargetBuild = 3;
        [Min(0)] public int rampagePrismTargetBuild = 2000;
        [HideInInspector] public List<int> rampagePrismTargetByIntensityBuild = new() { 2000, 1700, 1300, 1000 };
        [Min(0)] public int cleavePrismTargetBuild = 1000;
        [HideInInspector] public List<int> cleavePrismTargetByIntensityBuild = new() { 950, 950, 1000, 1000 };
        [Min(0)] public int wildlifeKillTargetBuild = 30;
        [Min(0)] public int dogFightPointTargetBuild = 40;
        [Min(0)] public int grizzlyChargePointTargetBuild = 90;
        [Min(0)] public int bendsPointTargetBuild = 3;
        [Min(0)] public int scarabScrambleGoalTargetBuild = 3;
        [Min(0)] public int astroLeagueGoalLimitBuild = 5;
        [Min(0)] public int salvoPrismTargetBuild = 700;
        [Min(0)] public int switchbackGateTargetBuild = 26;
        [Min(0)] public int breakwaterStationTargetBuild = 15;
        [Min(0)] public int waystationClusterTargetBuild = 5;

        [HideInInspector, Min(0)] public int breakwaterLapsBuild = 2;
        [Min(0)] public int skeinRingTargetBuild = 24;
        [Min(0)] public int headlongGateTargetBuild = 40;
        [Min(0)] public int redlineGateTargetBuild = 64;
        [Min(0)] public int grizzlyTimeGateTargetBuild = 16;
        [Min(0)] public int slingshotGateTargetBuild = 16;
        [Min(0)] public int warplineGateTargetBuild = 10;
        [Min(0)] public int thresherTimeGateTargetBuild = 16;
        [Min(0)] public int wrasseRunGateTargetBuild = 16;
        [Min(0)] public int burrowRunGateTargetBuild = 16;
        [Min(0)] public int scarabRallyGateTargetBuild = 24;
        [Min(0)] public int serpentTimeGateTargetBuild = 16;
        [Min(0)] public int brittleStarTimeGateTargetBuild = 16;
        [Min(0)] public int lanternRunGateTargetBuild = 16;
        [Min(0)] public int thornlineGateTargetBuild = 16;
        [Min(0)] public int spearlineGateTargetBuild = 24;
        [Min(0)] public int decoyRunGateTargetBuild = 12;
        [Min(0)] public int breachGateTargetBuild = 16;
        [Min(0)] public int slipLineGateTargetBuild = 16;
        [Min(0)] public int perchRunGateTargetBuild = 16;
        [Min(0)] public int regattaGateTargetBuild = 24;
        [Min(0)] public int hijackStealTargetBuild = 500;
        [Min(0)] public int tollwayTollTargetBuild = 3;
        [Min(0)] public int wreckingBallPrismTargetBuild = 650;
        [Min(0)] public int undertowPointTargetBuild = 12;
        [Min(0)] public int dustupPointTargetBuild = 4;
        [FormerlySerializedAs("serpentGulagSnipeTargetBuild")]
        [Min(0)] public int serpentHoodSnipeTargetBuild = 3;
        [Min(0)] public int jackstrawStrikeTargetBuild = 3;
        [Min(0)] public int riposteStrikeTargetBuild = 6;
        [Min(0)] public int bullrushGoreTargetBuild = 5;
        [Min(0)] public int windrowRoundSecondsBuild = 90;
        [Min(0)] public int landslidePrismTargetBuild = 500;
        [Min(0)] public int flatlinePointTargetBuild = 90;
        [HideInInspector] public List<int> flatlinePointTargetByIntensityBuild = new() { 90, 90, 90, 90 };
        [Min(0)] public int kelpwoodBiteTargetBuild = 12;
        [Min(0)] public int coronaBloomTargetBuild = 15;
        [Min(0)] public int shockfrontRippleTargetBuild = 10;
        [Min(0)] public int fulmenPrismTargetBuild = 1200;
        [Min(0)] public int railrushHitTargetBuild = 20;
        [Min(0)] public int standoffBlastTargetBuild = 10;
        [Min(0)] public int harvestPrismTargetBuild = 750;
        [Min(0)] public int shatterplanePrismTargetBuild = 1600;
        [HideInInspector] public List<int> shatterplanePrismTargetByIntensityBuild = new() { 1600, 1600, 2000, 1000 };
        [Min(0)] public int stingherdStingTargetBuild = 12;
        [Min(0)] public int trawlPrismTargetBuild = 300;
        [Min(0)] public int tapestryRoundSecondsBuild = 90;
        [Min(0)] public int palisadeRoundSecondsBuild = 90;
        [Min(0)] public int glasshouseRoundSecondsBuild = 90;
        [Min(0)] public int chumRoundSecondsBuild = 90;
        [Min(0)] public int starweaveRoundSecondsBuild = 90;
        [Min(0)] public int haulRoundSecondsBuild = 90;
        [Min(0)] public int hedgerowRoundSecondsBuild = 90;
        [Min(0)] public int longshotRoundSecondsBuild = 90;
        [Min(0)] public int marionetteStrikeTargetBuild = 12;
        [Min(0)] public int gorgeRoundSecondsBuild = 90;
        [Min(0)] public int podHuntBlastTargetBuild = 15;
        [Min(0)] public int iceShelfRoundSecondsBuild = 90;
        [Min(0)] public int depthChargePrismTargetBuild = 1500;
        [Min(0)] public int larderMissileTargetBuild = 16;
        [Min(0)] public int thornbushRoundSecondsBuild = 90;
        [Min(0)] public int lancefallPrismTargetBuild = 600;
        [Min(0)] public int flitstrikeStrikeTargetBuild = 5;
        [Min(0)] public int honeycombRoundSecondsBuild = 90;
        [Min(0)] public int woodpeckerPrismTargetBuild = 150;
        [Min(0)] public int sentryZapTargetBuild = 40;
        [Min(0)] public int warrenRoundSecondsBuild = 90;
        [Min(0)] public int caveInPrismTargetBuild = 1000;
        [Min(0)] public int cleanerRamStrikeTargetBuild = 8;
        [Min(0)] public int reefbuildRoundSecondsBuild = 90;
        [Min(0)] public int blastlinePrismTargetBuild = 600;
        [Min(0)] public int snapHitTargetBuild = 5;
        [Min(0)] public int denBuilderRoundSecondsBuild = 150;
        [Min(0)] public int riptidePrismTargetBuild = 600;
        [Min(0)] public int siroccoPrismTargetBuild = 400;
        [Min(0)] public int broadsidePointsPerPilotBuild = 100;
        [Range(0, 100)] public int tandavaBreakPercentBuild = 35;
        [FormerlySerializedAs("tandavaFlamesToBreakBuild")]
        [Range(0, 12)] public int tandavaHaloRingsToBreakBuild = 9;

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
        /// Rampage target for a given INTENSITY (1-based), falling back to
        /// <see cref="GetRampagePrismTarget()"/> when that rung authors nothing - Cleave's
        /// fallback chain (<see cref="GetCleavePrismTarget(int)"/>), for the same reason. Read
        /// SERVER-side by <c>RampagePrismTurnMonitor</c> and replicated, so a client receives the
        /// number rather than deriving it from an intensity it may not have yet.
        /// </summary>
        public int GetRampagePrismTarget(int intensity)
        {
            if (rampagePrismTargetByIntensity == null || rampagePrismTargetByIntensity.Count == 0)
                return GetRampagePrismTarget();

            int i = Mathf.Clamp(intensity - 1, 0, rampagePrismTargetByIntensity.Count - 1);
            int v = rampagePrismTargetByIntensity[i];
            return v > 0 ? v : GetRampagePrismTarget();
        }

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
        /// Grizzly Charge point target: the configured value when &gt; 0, otherwise
        /// <see cref="DefaultGrizzlyChargePointTarget"/>. Read by <c>DogFightPointTurnMonitor</c>
        /// when the match is Grizzly Charge, which races on Dog Fight's monitor and price list.
        /// </summary>
        public int GetGrizzlyChargePointTarget() =>
            grizzlyChargePointTarget > 0 ? grizzlyChargePointTarget : DefaultGrizzlyChargePointTarget;

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
        /// Astro League mercy-rule goal limit: the configured value when &gt; 0, otherwise
        /// <see cref="DefaultAstroLeagueGoalLimit"/>. Resolved once by AstroLeagueController on the
        /// server and replicated to clients as GameDataSO.GoalTargetCount.
        /// </summary>
        public int GetAstroLeagueGoalLimit() =>
            astroLeagueGoalLimit > 0 ? astroLeagueGoalLimit : DefaultAstroLeagueGoalLimit;

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

        /// <summary>
        /// Waystation race length in CLUSTERS: the configured value when &gt; 0, otherwise
        /// <see cref="DefaultWaystationClusterTarget"/>. Read by <c>WaystationCourseSource</c>, which lays that many
        /// clusters (towers between them) and reports the rings it laid as the finish line.
        /// </summary>
        public int GetWaystationClusterTarget() =>
            waystationClusterTarget > 0 ? waystationClusterTarget : DefaultWaystationClusterTarget;

        /// <summary>Skein course length ("thread all N rings"). Read on purpose by every party
        /// that needs it - <c>RaceGateTurnMonitor</c> for the target (through the controller),
        /// <c>SkeinController</c> for how many to lay, <c>SpawnableSkein</c> for the ring count
        /// its cable is built for.</summary>
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

        /// <summary>
        /// Headlong race length ("thread N gates", i.e. laps x rings): the configured value when
        /// &gt; 0, otherwise <see cref="DefaultHeadlongGateTarget"/>. Read twice on purpose - by
        /// <c>RaceGateTurnMonitor</c> (through <c>HeadlongController.AuthoredGateTarget</c>) for
        /// the target and by <c>HeadlongController</c> to size the circuit - so the finish line
        /// and the course cannot drift apart.
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
        /// Grizzly Time race length ("thread N gates", i.e. laps x rings): the configured value
        /// when &gt; 0, otherwise <see cref="DefaultGrizzlyTimeGateTarget"/>. Read by
        /// <c>GrizzlyTimeController.AuthoredGateTarget</c>, which the turn monitor asks in turn.
        /// </summary>
        public int GetGrizzlyTimeGateTarget() =>
            grizzlyTimeGateTarget > 0 ? grizzlyTimeGateTarget : DefaultGrizzlyTimeGateTarget;

        /// <summary>
        /// Slingshot race length ("thread N gates", i.e. laps x rings): the configured value when
        /// &gt; 0, otherwise <see cref="DefaultSlingshotGateTarget"/>. Read by
        /// <c>SlingshotController.AuthoredGateTarget</c>, which the turn monitor asks in turn.
        /// </summary>
        public int GetSlingshotGateTarget() =>
            slingshotGateTarget > 0 ? slingshotGateTarget : DefaultSlingshotGateTarget;

        /// <summary>
        /// Warpline race length ("thread N gates", i.e. laps x rings): the configured value when
        /// &gt; 0, otherwise <see cref="DefaultWarplineGateTarget"/>. Read by
        /// <c>WarplineController.AuthoredGateTarget</c>, which the turn monitor asks in turn.
        /// </summary>
        public int GetWarplineGateTarget() =>
            warplineGateTarget > 0 ? warplineGateTarget : DefaultWarplineGateTarget;

        /// <summary>
        /// Thresher Time race length ("thread N gates", i.e. laps x rings): the configured value
        /// when &gt; 0, otherwise <see cref="DefaultThresherTimeGateTarget"/>. Read by
        /// <c>ThresherTimeController.AuthoredGateTarget</c>, which the turn monitor asks in turn.
        /// </summary>
        public int GetThresherTimeGateTarget() =>
            thresherTimeGateTarget > 0 ? thresherTimeGateTarget : DefaultThresherTimeGateTarget;

        /// <summary>
        /// Wrasse Run race length ("thread N gates", i.e. laps x rings): the configured value when
        /// &gt; 0, otherwise <see cref="DefaultWrasseRunGateTarget"/>. Read by
        /// <c>WrasseRunCourseSource.AuthoredGateTarget</c>, which the turn monitor asks in turn.
        /// </summary>
        public int GetWrasseRunGateTarget() =>
            wrasseRunGateTarget > 0 ? wrasseRunGateTarget : DefaultWrasseRunGateTarget;

        /// <summary>Scarab Rally race length: the configured value when &gt; 0, otherwise
        /// <see cref="DefaultScarabRallyGateTarget"/>.</summary>
        public int GetScarabRallyGateTarget() =>
            scarabRallyGateTarget > 0 ? scarabRallyGateTarget : DefaultScarabRallyGateTarget;

        /// <summary>Serpent Time race length: the configured value when &gt; 0, otherwise
        /// <see cref="DefaultSerpentTimeGateTarget"/>.</summary>
        public int GetSerpentTimeGateTarget() =>
            serpentTimeGateTarget > 0 ? serpentTimeGateTarget : DefaultSerpentTimeGateTarget;

        /// <summary>Brittle Star Time race length: the configured value when &gt; 0, otherwise
        /// <see cref="DefaultBrittleStarTimeGateTarget"/>.</summary>
        public int GetBrittleStarTimeGateTarget() =>
            brittleStarTimeGateTarget > 0 ? brittleStarTimeGateTarget : DefaultBrittleStarTimeGateTarget;

        /// <summary>Lantern Run race length (laps x rings): the authored value when above 0, otherwise
        /// <see cref="DefaultLanternRunGateTarget"/>.</summary>
        public int GetLanternRunGateTarget() =>
            lanternRunGateTarget > 0 ? lanternRunGateTarget : DefaultLanternRunGateTarget;

        /// <summary>Thornline race length (rings): the authored value when above 0, otherwise
        /// <see cref="DefaultThornlineGateTarget"/>.</summary>
        public int GetThornlineGateTarget() =>
            thornlineGateTarget > 0 ? thornlineGateTarget : DefaultThornlineGateTarget;

        /// <summary>Spearline race length (rings): the authored value when above 0, otherwise
        /// <see cref="DefaultSpearlineGateTarget"/>.</summary>
        public int GetSpearlineGateTarget() =>
            spearlineGateTarget > 0 ? spearlineGateTarget : DefaultSpearlineGateTarget;

        /// <summary>Decoy Run race length (rings): the authored value when above 0, otherwise
        /// <see cref="DefaultDecoyRunGateTarget"/>.</summary>
        public int GetDecoyRunGateTarget() =>
            decoyRunGateTarget > 0 ? decoyRunGateTarget : DefaultDecoyRunGateTarget;

        /// <summary>Breach race length (rings): the authored value when above 0, otherwise
        /// <see cref="DefaultBreachGateTarget"/>.</summary>
        public int GetBreachGateTarget() =>
            breachGateTarget > 0 ? breachGateTarget : DefaultBreachGateTarget;

        /// <summary>Slip Line race length (rings): the authored value when above 0, otherwise
        /// <see cref="DefaultSlipLineGateTarget"/>.</summary>
        public int GetSlipLineGateTarget() =>
            slipLineGateTarget > 0 ? slipLineGateTarget : DefaultSlipLineGateTarget;

        /// <summary>Perch Run race length (rings): the authored value when above 0, otherwise
        /// <see cref="DefaultPerchRunGateTarget"/>.</summary>
        public int GetPerchRunGateTarget() =>
            perchRunGateTarget > 0 ? perchRunGateTarget : DefaultPerchRunGateTarget;

        /// <summary>
        /// Burrow Run race length ("thread N gates", i.e. laps x rings): the configured value when
        /// &gt; 0, otherwise <see cref="DefaultBurrowRunGateTarget"/>. Read by
        /// <c>BurrowRunCourseSource.AuthoredGateTarget</c>, which the turn monitor asks in turn.
        /// </summary>
        public int GetBurrowRunGateTarget() =>
            burrowRunGateTarget > 0 ? burrowRunGateTarget : DefaultBurrowRunGateTarget;

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
        /// Dustup point target ("first domain to N dustings"): the configured value when &gt; 0,
        /// otherwise <see cref="DefaultDustupPointTarget"/>. Compared against a DOMAIN's summed
        /// CombatPoints.
        /// </summary>
        public int GetDustupPointTarget() =>
            dustupPointTarget > 0 ? dustupPointTarget : DefaultDustupPointTarget;

        /// <summary>
        /// Hood snipe target ("first domain to N snipes"): the configured value when &gt; 0,
        /// otherwise <see cref="DefaultSerpentHoodSnipeTarget"/>. Compared against a DOMAIN's summed
        /// CombatPoints.
        /// </summary>
        public int GetSerpentHoodSnipeTarget() =>
            serpentHoodSnipeTarget > 0 ? serpentHoodSnipeTarget : DefaultSerpentHoodSnipeTarget;

        /// <summary>
        /// Jackstraw strike target ("first domain to N strikes"): the configured value when &gt; 0, otherwise
        /// <see cref="DefaultJackstrawStrikeTarget"/>. Compared against a DOMAIN's summed CombatPoints.
        /// </summary>
        public int GetJackstrawStrikeTarget() =>
            jackstrawStrikeTarget > 0 ? jackstrawStrikeTarget : DefaultJackstrawStrikeTarget;

        /// <summary>Riposte strike target ("first domain to N strikes"): the configured value when &gt; 0, otherwise
        /// <see cref="DefaultRiposteStrikeTarget"/>. Compared against a DOMAIN's summed CombatPoints.</summary>
        public int GetRiposteStrikeTarget() =>
            riposteStrikeTarget > 0 ? riposteStrikeTarget : DefaultRiposteStrikeTarget;

        /// <summary>
        /// Bullrush gore target ("first domain to N gores"): the configured value when &gt; 0, otherwise
        /// <see cref="DefaultBullrushGoreTarget"/>. Compared against a DOMAIN's summed CombatPoints.
        /// </summary>
        public int GetBullrushGoreTarget() =>
            bullrushGoreTarget > 0 ? bullrushGoreTarget : DefaultBullrushGoreTarget;

        /// <summary>
        /// Windrow round length in seconds: the configured value when &gt; 0, otherwise
        /// <see cref="DefaultWindrowRoundSeconds"/>. Read by WindrowTimeTurnMonitor at StartMonitor on every peer.
        /// </summary>
        public int GetWindrowRoundSeconds() =>
            windrowRoundSeconds > 0 ? windrowRoundSeconds : DefaultWindrowRoundSeconds;

        /// <summary>
        /// Landslide prism target ("first domain to N hostile prisms"): the configured value when &gt; 0, otherwise
        /// <see cref="DefaultLandslidePrismTarget"/>. Compared against a DOMAIN's summed HostilePrismsDestroyed.
        /// </summary>
        public int GetLandslidePrismTarget() =>
            landslidePrismTarget > 0 ? landslidePrismTarget : DefaultLandslidePrismTarget;

        /// <summary>
        /// Flatline point target ("first domain to N points"): the configured value when &gt; 0, otherwise
        /// <see cref="DefaultFlatlinePointTarget"/>. Compared against a DOMAIN's summed CombatPoints.
        /// </summary>
        public int GetFlatlinePointTarget() =>
            flatlinePointTarget > 0 ? flatlinePointTarget : DefaultFlatlinePointTarget;

        /// <summary>
        /// Flatline target for a given INTENSITY (1-based), falling back to <see cref="GetFlatlinePointTarget()"/>
        /// when that rung authors nothing - Cleave's fallback chain (<see cref="GetCleavePrismTarget(int)"/>), and
        /// resolved on the server for the same reason (<c>FlatlinePointTurnMonitor</c> replicates the number).
        /// </summary>
        public int GetFlatlinePointTarget(int intensity)
        {
            if (flatlinePointTargetByIntensity == null || flatlinePointTargetByIntensity.Count == 0)
                return GetFlatlinePointTarget();

            int i = Mathf.Clamp(intensity - 1, 0, flatlinePointTargetByIntensity.Count - 1);
            int v = flatlinePointTargetByIntensity[i];
            return v > 0 ? v : GetFlatlinePointTarget();
        }

        /// <summary>
        /// Kelpwood bite target ("first domain to N bites"): the configured value when &gt; 0, otherwise
        /// <see cref="DefaultKelpwoodBiteTarget"/>. Compared against a DOMAIN's summed CombatPoints.
        /// </summary>
        public int GetKelpwoodBiteTarget() =>
            kelpwoodBiteTarget > 0 ? kelpwoodBiteTarget : DefaultKelpwoodBiteTarget;

        /// <summary>
        /// Corona bloom target ("first domain to N bloom hits"): the configured value when &gt; 0, otherwise
        /// <see cref="DefaultCoronaBloomTarget"/>. Compared against a DOMAIN's summed CombatPoints.
        /// </summary>
        public int GetCoronaBloomTarget() =>
            coronaBloomTarget > 0 ? coronaBloomTarget : DefaultCoronaBloomTarget;

        /// <summary>
        /// Shockfront ripple target ("first domain to N ripple hits"): the configured value when &gt; 0, otherwise
        /// <see cref="DefaultShockfrontRippleTarget"/>. Compared against a DOMAIN's summed CombatPoints.
        /// </summary>
        public int GetShockfrontRippleTarget() =>
            shockfrontRippleTarget > 0 ? shockfrontRippleTarget : DefaultShockfrontRippleTarget;

        /// <summary>
        /// Fulmen prism target ("first domain to N hostile prisms"): the configured value when &gt; 0, otherwise
        /// <see cref="DefaultFulmenPrismTarget"/>. Compared against a DOMAIN's summed HostilePrismsDestroyed.
        /// </summary>
        public int GetFulmenPrismTarget() =>
            fulmenPrismTarget > 0 ? fulmenPrismTarget : DefaultFulmenPrismTarget;
        /// Railrush hit target ("first domain to N spike hits"): the configured value when &gt; 0, otherwise
        /// <see cref="DefaultRailrushHitTarget"/>. Compared against a DOMAIN's summed CombatPoints.
        /// </summary>
        public int GetRailrushHitTarget() =>
            railrushHitTarget > 0 ? railrushHitTarget : DefaultRailrushHitTarget;
        /// Standoff blast target ("first domain to N blast hits"): the configured value when &gt; 0, otherwise
        /// <see cref="DefaultStandoffBlastTarget"/>. Compared against a DOMAIN's summed CombatPoints.
        /// </summary>
        public int GetStandoffBlastTarget() =>
            standoffBlastTarget > 0 ? standoffBlastTarget : DefaultStandoffBlastTarget;

        /// <summary>
        /// Harvest prism target ("first domain to N hostile prisms"): the configured value when &gt; 0, otherwise
        /// <see cref="DefaultHarvestPrismTarget"/>. Compared against a DOMAIN's summed HostilePrismsDestroyed.
        /// </summary>
        public int GetHarvestPrismTarget() =>
            harvestPrismTarget > 0 ? harvestPrismTarget : DefaultHarvestPrismTarget;
        /// Shatterplane prism target ("first domain to N hostile prisms broken"): the configured value when &gt; 0,
        /// otherwise <see cref="DefaultShatterplanePrismTarget"/>. Compared against a DOMAIN's summed
        /// HostilePrismsDestroyed.
        /// </summary>
        public int GetShatterplanePrismTarget() =>
            shatterplanePrismTarget > 0 ? shatterplanePrismTarget : DefaultShatterplanePrismTarget;

        /// <summary>
        /// Shatterplane target for a given INTENSITY (1-based), falling back to <see cref="GetShatterplanePrismTarget()"/>
        /// for an empty list, a 0 entry or a missing rung. Resolved on the server (<c>ShatterplanePrismTurnMonitor</c>
        /// replicates the number), Cleave's rule.
        /// </summary>
        public int GetShatterplanePrismTarget(int intensity)
        {
            if (shatterplanePrismTargetByIntensity == null || shatterplanePrismTargetByIntensity.Count == 0)
                return GetShatterplanePrismTarget();
            int i = Mathf.Clamp(intensity - 1, 0, shatterplanePrismTargetByIntensity.Count - 1);
            int v = shatterplanePrismTargetByIntensity[i];
            return v > 0 ? v : GetShatterplanePrismTarget();
        }

        /// <summary>
        /// Stingherd sting target ("first domain to N stings"): the configured value when &gt; 0, otherwise
        /// <see cref="DefaultStingherdStingTarget"/>. Compared against a DOMAIN's summed CombatPoints.
        /// </summary>
        public int GetStingherdStingTarget() =>
            stingherdStingTarget > 0 ? stingherdStingTarget : DefaultStingherdStingTarget;

        /// <summary>
        /// Trawl prism target ("first domain to N hostile prisms"): the configured value when &gt; 0, otherwise
        /// <see cref="DefaultTrawlPrismTarget"/>. Compared against a DOMAIN's summed HostilePrismsDestroyed.
        /// </summary>
        public int GetTrawlPrismTarget() =>
            trawlPrismTarget > 0 ? trawlPrismTarget : DefaultTrawlPrismTarget;

        /// <summary>
        /// Tapestry round length in seconds: the configured value when &gt; 0, otherwise
        /// <see cref="DefaultTapestryRoundSeconds"/>. Read by TapestryTimeTurnMonitor at
        /// StartMonitor on every peer, so the clock and the whistle cannot disagree.
        /// </summary>
        public int GetTapestryRoundSeconds() =>
            tapestryRoundSeconds > 0 ? tapestryRoundSeconds : DefaultTapestryRoundSeconds;

        /// <summary>Palisade round length in seconds: the configured value when &gt; 0, otherwise
        /// <see cref="DefaultPalisadeRoundSeconds"/>. Read by PalisadeTimeTurnMonitor at StartMonitor on every peer.</summary>
        public int GetPalisadeRoundSeconds() =>
            palisadeRoundSeconds > 0 ? palisadeRoundSeconds : DefaultPalisadeRoundSeconds;

        /// <summary>
        /// Glasshouse round length in seconds: the configured value when &gt; 0, otherwise
        /// <see cref="DefaultGlasshouseRoundSeconds"/>. Read by GlasshouseTimeTurnMonitor at StartMonitor on every peer.
        /// </summary>
        public int GetGlasshouseRoundSeconds() =>
            glasshouseRoundSeconds > 0 ? glasshouseRoundSeconds : DefaultGlasshouseRoundSeconds;

        /// <summary>
        /// Chum round length in seconds: the configured value when &gt; 0, otherwise
        /// <see cref="DefaultChumRoundSeconds"/>. Read by ChumTimeTurnMonitor at StartMonitor on every peer.
        /// </summary>
        public int GetChumRoundSeconds() =>
            chumRoundSeconds > 0 ? chumRoundSeconds : DefaultChumRoundSeconds;
        /// Pod Hunt blast target ("first domain to N blasts"): the configured value when &gt; 0, otherwise
        /// <see cref="DefaultPodHuntBlastTarget"/>. Compared against a DOMAIN's summed CombatPoints.
        /// </summary>
        public int GetPodHuntBlastTarget() =>
            podHuntBlastTarget > 0 ? podHuntBlastTarget : DefaultPodHuntBlastTarget;

        /// <summary>
        /// Ice Shelf round length in seconds: the configured value when &gt; 0, otherwise
        /// <see cref="DefaultIceShelfRoundSeconds"/>. Read by IceShelfTimeTurnMonitor at StartMonitor on every peer.
        /// </summary>
        public int GetIceShelfRoundSeconds() =>
            iceShelfRoundSeconds > 0 ? iceShelfRoundSeconds : DefaultIceShelfRoundSeconds;

        /// <summary>
        /// Depth Charge prism target ("race to N" hostile prisms destroyed): the configured value when &gt; 0,
        /// otherwise <see cref="DefaultDepthChargePrismTarget"/>. Compared against a DOMAIN's summed HostilePrismsDestroyed.
        /// </summary>
        public int GetDepthChargePrismTarget() =>
            depthChargePrismTarget > 0 ? depthChargePrismTarget : DefaultDepthChargePrismTarget;

        /// <summary>
        /// Larder missile target ("first domain to N missile hits"): the configured value when &gt; 0, otherwise
        /// <see cref="DefaultLarderMissileTarget"/>. Compared against a DOMAIN's summed CombatPoints.
        /// </summary>
        public int GetLarderMissileTarget() =>
            larderMissileTarget > 0 ? larderMissileTarget : DefaultLarderMissileTarget;

        /// <summary>
        /// Thornbush round length in seconds: the configured value when &gt; 0, otherwise
        /// <see cref="DefaultThornbushRoundSeconds"/>. Read by ThornbushTimeTurnMonitor at StartMonitor on every peer.
        /// </summary>
        public int GetThornbushRoundSeconds() =>
            thornbushRoundSeconds > 0 ? thornbushRoundSeconds : DefaultThornbushRoundSeconds;

        /// <summary>
        /// Lancefall prism target ("first domain to N hostile prisms"): the configured value when &gt; 0, otherwise
        /// <see cref="DefaultLancefallPrismTarget"/>. Compared against a DOMAIN's summed HostilePrismsDestroyed.
        /// </summary>
        public int GetLancefallPrismTarget() =>
            lancefallPrismTarget > 0 ? lancefallPrismTarget : DefaultLancefallPrismTarget;
        /// Flitstrike strike target ("first domain to N strikes"): the configured value when &gt; 0, otherwise
        /// <see cref="DefaultFlitstrikeStrikeTarget"/>. Compared against a DOMAIN's summed CombatPoints, so teammates pool.
        /// </summary>
        public int GetFlitstrikeStrikeTarget() =>
            flitstrikeStrikeTarget > 0 ? flitstrikeStrikeTarget : DefaultFlitstrikeStrikeTarget;

        /// <summary>
        /// Honeycomb round length in seconds: the configured value when &gt; 0, otherwise
        /// <see cref="DefaultHoneycombRoundSeconds"/>. Read by HoneycombTimeTurnMonitor at StartMonitor on every peer.
        /// </summary>
        public int GetHoneycombRoundSeconds() =>
            honeycombRoundSeconds > 0 ? honeycombRoundSeconds : DefaultHoneycombRoundSeconds;

        /// <summary>
        /// Woodpecker prism target ("race to N" hostile prisms destroyed): the configured value when &gt; 0, otherwise
        /// <see cref="DefaultWoodpeckerPrismTarget"/>. Compared against a DOMAIN's summed destruction count.
        /// </summary>
        public int GetWoodpeckerPrismTarget() =>
            woodpeckerPrismTarget > 0 ? woodpeckerPrismTarget : DefaultWoodpeckerPrismTarget;

        /// <summary>
        /// Starweave round length in seconds: the configured value when &gt; 0, otherwise
        /// <see cref="DefaultStarweaveRoundSeconds"/>. Read by StarweaveTimeTurnMonitor at StartMonitor on every peer.
        /// </summary>
        public int GetStarweaveRoundSeconds() =>
            starweaveRoundSeconds > 0 ? starweaveRoundSeconds : DefaultStarweaveRoundSeconds;

        /// <summary>
        /// Haul round length in seconds: the configured value when &gt; 0, otherwise
        /// <see cref="DefaultHaulRoundSeconds"/>. Read by HaulTimeTurnMonitor at StartMonitor on every peer.
        /// </summary>
        public int GetHaulRoundSeconds() =>
            haulRoundSeconds > 0 ? haulRoundSeconds : DefaultHaulRoundSeconds;

        /// <summary>
        /// Hedgerow round length in seconds: the configured value when &gt; 0, otherwise
        /// <see cref="DefaultHedgerowRoundSeconds"/>. Read by HedgerowTimeTurnMonitor at StartMonitor on every peer.
        /// </summary>
        public int GetHedgerowRoundSeconds() =>
            hedgerowRoundSeconds > 0 ? hedgerowRoundSeconds : DefaultHedgerowRoundSeconds;

        /// <summary>
        /// Longshot round length in seconds: the configured value when &gt; 0, otherwise
        /// <see cref="DefaultLongshotRoundSeconds"/>. Read by LongshotTimeTurnMonitor at StartMonitor on every peer.
        /// </summary>
        public int GetLongshotRoundSeconds() =>
            longshotRoundSeconds > 0 ? longshotRoundSeconds : DefaultLongshotRoundSeconds;
        /// Sentry zap target ("first domain to N zaps"): the configured value when &gt; 0, otherwise
        /// <see cref="DefaultSentryZapTarget"/>. Compared against a DOMAIN's summed CombatPoints.
        /// </summary>
        public int GetSentryZapTarget() =>
            sentryZapTarget > 0 ? sentryZapTarget : DefaultSentryZapTarget;

        /// <summary>
        /// Warren round length in seconds: the configured value when &gt; 0, otherwise
        /// <see cref="DefaultWarrenRoundSeconds"/>. Read by WarrenTimeTurnMonitor at StartMonitor on every peer.
        /// </summary>
        public int GetWarrenRoundSeconds() =>
            warrenRoundSeconds > 0 ? warrenRoundSeconds : DefaultWarrenRoundSeconds;

        /// <summary>
        /// Cave-in prism target ("race to N" hostile prisms broken): the configured value when &gt; 0, otherwise
        /// <see cref="DefaultCaveInPrismTarget"/>. Compared against a DOMAIN's summed hostile prisms destroyed.
        /// </summary>
        public int GetCaveInPrismTarget() =>
            caveInPrismTarget > 0 ? caveInPrismTarget : DefaultCaveInPrismTarget;
        /// Cleaner Ram ram target ("first domain to N rams"): the configured value when &gt; 0, otherwise
        /// <see cref="DefaultCleanerRamStrikeTarget"/>. Compared against a DOMAIN's summed CombatPoints.
        /// </summary>
        public int GetCleanerRamStrikeTarget() =>
            cleanerRamStrikeTarget > 0 ? cleanerRamStrikeTarget : DefaultCleanerRamStrikeTarget;

        /// <summary>
        /// Reefbuild round length in seconds: the configured value when &gt; 0, otherwise
        /// <see cref="DefaultReefbuildRoundSeconds"/>. Read by ReefbuildTimeTurnMonitor at StartMonitor on every peer.
        /// </summary>
        public int GetReefbuildRoundSeconds() =>
            reefbuildRoundSeconds > 0 ? reefbuildRoundSeconds : DefaultReefbuildRoundSeconds;

        /// <summary>
        /// Blastline prism target ("first domain to N hostile prisms"): the configured value when &gt; 0, otherwise
        /// <see cref="DefaultBlastlinePrismTarget"/>. Compared against a DOMAIN's summed HostilePrismsDestroyed.
        /// </summary>
        public int GetBlastlinePrismTarget() =>
            blastlinePrismTarget > 0 ? blastlinePrismTarget : DefaultBlastlinePrismTarget;
        /// Snap ring-hit target ("first domain to N ring hits"): the configured value when &gt; 0, otherwise
        /// <see cref="DefaultSnapHitTarget"/>. Compared against a DOMAIN's summed CombatPoints.
        /// </summary>
        public int GetSnapHitTarget() =>
            snapHitTarget > 0 ? snapHitTarget : DefaultSnapHitTarget;

        /// <summary>
        /// Den Builder round length in seconds: the configured value when &gt; 0, otherwise
        /// <see cref="DefaultDenBuilderRoundSeconds"/>. Read by DenBuilderTimeTurnMonitor at StartMonitor on every peer.
        /// </summary>
        public int GetDenBuilderRoundSeconds() =>
            denBuilderRoundSeconds > 0 ? denBuilderRoundSeconds : DefaultDenBuilderRoundSeconds;

        /// <summary>
        /// Riptide prism target ("race to N" hostile prisms destroyed): the configured value when &gt; 0, otherwise
        /// <see cref="DefaultRiptidePrismTarget"/>. Compared against a DOMAIN's summed HostilePrismsDestroyed.
        /// </summary>
        public int GetRiptidePrismTarget() =>
            riptidePrismTarget > 0 ? riptidePrismTarget : DefaultRiptidePrismTarget;

        /// <summary>
        /// Marionette strike target ("first domain to N strikes"): the configured value when &gt; 0, otherwise
        /// <see cref="DefaultMarionetteStrikeTarget"/>. Compared against a DOMAIN's summed CombatPoints.
        /// </summary>
        public int GetMarionetteStrikeTarget() =>
            marionetteStrikeTarget > 0 ? marionetteStrikeTarget : DefaultMarionetteStrikeTarget;

        /// <summary>
        /// Gorge round length in seconds: the configured value when &gt; 0, otherwise
        /// <see cref="DefaultGorgeRoundSeconds"/>. Read by GorgeTimeTurnMonitor at StartMonitor on every peer.
        /// </summary>
        public int GetGorgeRoundSeconds() =>
            gorgeRoundSeconds > 0 ? gorgeRoundSeconds : DefaultGorgeRoundSeconds;

        /// <summary>
        /// Sirocco prism target ("race to N" hostile prisms destroyed): the configured value when
        /// &gt; 0, otherwise <see cref="DefaultSiroccoPrismTarget"/>. Compared against a DOMAIN's
        /// summed destruction count, so teammates pool.
        /// </summary>
        public int GetSiroccoPrismTarget() =>
            siroccoPrismTarget > 0 ? siroccoPrismTarget : DefaultSiroccoPrismTarget;

        /// <summary>
        /// Broadside points PER PILOT: the configured value when &gt; 0, otherwise
        /// <see cref="DefaultBroadsidePointsPerPilot"/>. Feed it to
        /// <see cref="GetBroadsidePointTarget"/> - this is not itself a target.
        /// </summary>
        public int GetBroadsidePointsPerPilot() =>
            broadsidePointsPerPilot > 0 ? broadsidePointsPerPilot : DefaultBroadsidePointsPerPilot;

        /// <summary>
        /// Broadside's actual race target for a domain of <paramref name="teamSize"/> pilots:
        /// <c>perPilot x (1 + <see cref="BroadsideExtraPilotFraction"/> x (teamSize - 1))</c>.
        /// Compared against a DOMAIN's summed CombatPoints, which every hull on the card pays
        /// into through its own weapon class.
        ///
        /// <para>Resolved SERVER-side by BroadsidePointTurnMonitor and replicated as an int, so a
        /// client RECEIVES the target rather than re-deriving it from a roster it may not have
        /// finished building - the same distinction CrystalManager's IntensityScaled count
        /// records. A team size below 1 is clamped, so an empty roster cannot produce a target
        /// of 0 and end the match on the countdown.</para>
        /// </summary>
        public int GetBroadsidePointTarget(int teamSize) =>
            Mathf.RoundToInt(GetBroadsidePointsPerPilot() *
                             (1f + BroadsideExtraPilotFraction * (Mathf.Max(1, teamSize) - 1)));

        /// <summary>Tandava's shatter threshold in percent of the current form's full body: the configured value when
        /// &gt; 0, otherwise <see cref="DefaultTandavaBreakPercent"/>. Read server-side by TandavaController.</summary>
        public int GetTandavaBreakPercent() =>
            tandavaBreakPercent > 0 ? tandavaBreakPercent : DefaultTandavaBreakPercent;

        /// <summary>Halo rings the pilots must break to break Tandava's dance: the configured value when &gt; 0, otherwise
        /// <see cref="DefaultTandavaHaloRingsToBreak"/>. Read server-side by TandavaController, which clamps it to the halo
        /// it built.</summary>
        public int GetTandavaHaloRingsToBreak() =>
            tandavaHaloRingsToBreak > 0 ? tandavaHaloRingsToBreak : DefaultTandavaHaloRingsToBreak;

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
                // Scalar only, as for Cleave below: the real answer is per-intensity
                // (GetRampagePrismTarget(int)).
                GameModes.Rampage                   => rampagePrismTarget > 0 ? rampagePrismTarget : DefaultRampagePrismTarget,
                // Scalar only: this reader answers "what does a match of this mode race to" for
                // editor tooling, and Cleave's real answer is per-intensity (see
                // GetCleavePrismTarget(int)). Reporting rung 1 here would read as the mode's
                // target and be wrong for three quarters of its ladder.
                GameModes.Cleave                   => cleavePrismTarget > 0 ? cleavePrismTarget : DefaultCleavePrismTarget,
                GameModes.WildlifeLiberation        => wildlifeKillTarget > 0 ? wildlifeKillTarget : DefaultWildlifeKillTarget,
                GameModes.DogFight                  => dogFightPointTarget > 0 ? dogFightPointTarget : DefaultDogFightPointTarget,
                GameModes.GrizzlyCharge             => GetGrizzlyChargePointTarget(),
                GameModes.Bends                     => bendsPointTarget > 0 ? bendsPointTarget : DefaultBendsPointTarget,
                GameModes.ScarabScramble            => scarabScrambleGoalTarget > 0 ? scarabScrambleGoalTarget : DefaultScarabScrambleGoalTarget,
                // Mercy-rule cap on a TIMED match: the most goals a match can race to.
                GameModes.AstroLeague               => GetAstroLeagueGoalLimit(),
                GameModes.Salvo                     => salvoPrismTarget > 0 ? salvoPrismTarget : DefaultSalvoPrismTarget,
                GameModes.Switchback                => switchbackGateTarget > 0 ? switchbackGateTarget : DefaultSwitchbackGateTarget,
                GameModes.Breakwater                => GetBreakwaterCrossingTarget(),
                GameModes.Skein                     => skeinRingTarget > 0 ? skeinRingTarget : DefaultSkeinRingTarget,
                GameModes.Headlong                  => headlongGateTarget > 0 ? headlongGateTarget : DefaultHeadlongGateTarget,
                GameModes.Redline                   => redlineGateTarget > 0 ? redlineGateTarget : DefaultRedlineGateTarget,
                GameModes.GrizzlyTime               => GetGrizzlyTimeGateTarget(),
                GameModes.Slingshot                 => GetSlingshotGateTarget(),
                GameModes.Warpline                  => GetWarplineGateTarget(),
                GameModes.ThresherTime              => GetThresherTimeGateTarget(),
                GameModes.WrasseRun                 => GetWrasseRunGateTarget(),
                GameModes.BurrowRun                 => GetBurrowRunGateTarget(),
                GameModes.ScarabRally               => GetScarabRallyGateTarget(),
                GameModes.SerpentTime               => GetSerpentTimeGateTarget(),
                GameModes.BrittleStarTime           => GetBrittleStarTimeGateTarget(),
                GameModes.LanternRun              => GetLanternRunGateTarget(),
                GameModes.Thornline                 => GetThornlineGateTarget(),
                GameModes.Spearline                 => GetSpearlineGateTarget(),
                GameModes.DecoyRun                    => GetDecoyRunGateTarget(),
                GameModes.Breach                      => GetBreachGateTarget(),
                GameModes.SlipLine                    => GetSlipLineGateTarget(),
                GameModes.PerchRun                    => GetPerchRunGateTarget(),
                // GameModes.Waystation has no row: its finish line is the rings the course lays (towers vary with the
                // gaps), auto-calculated from the track like a SkimRace with a 0 count.
                GameModes.Regatta                   => regattaGateTarget > 0 ? regattaGateTarget : DefaultRegattaGateTarget,
                GameModes.Hijack                    => hijackStealTarget > 0 ? hijackStealTarget : DefaultHijackStealTarget,
                GameModes.Tollway                   => tollwayTollTarget > 0 ? tollwayTollTarget : DefaultTollwayTollTarget,
                GameModes.WreckingBall              => wreckingBallPrismTarget > 0 ? wreckingBallPrismTarget : DefaultWreckingBallPrismTarget,
                GameModes.Undertow                  => undertowPointTarget > 0 ? undertowPointTarget : DefaultUndertowPointTarget,
                GameModes.Dustup                    => GetDustupPointTarget(),
                GameModes.SerpentHood              => GetSerpentHoodSnipeTarget(),
                GameModes.Jackstraw                 => GetJackstrawStrikeTarget(),
                GameModes.Riposte                   => GetRiposteStrikeTarget(),
                GameModes.Bullrush                  => GetBullrushGoreTarget(),
                GameModes.Landslide                 => GetLandslidePrismTarget(),
                // Windrow is TIMED (windrowRoundSeconds), Tapestry's shape: no race target, deliberately absent.
                // Scalar only, as for Cleave: the real answer is per-intensity (GetFlatlinePointTarget(int)).
                GameModes.Flatline                  => GetFlatlinePointTarget(),
                GameModes.Kelpwood                  => GetKelpwoodBiteTarget(),
                GameModes.Corona                    => GetCoronaBloomTarget(),
                GameModes.Shockfront                => GetShockfrontRippleTarget(),
                GameModes.Fulmen                    => GetFulmenPrismTarget(),
                GameModes.Railrush                  => GetRailrushHitTarget(),
                GameModes.Standoff                  => GetStandoffBlastTarget(),
                GameModes.Harvest                   => GetHarvestPrismTarget(),
                GameModes.PodHunt                   => GetPodHuntBlastTarget(),
                GameModes.DepthCharge               => GetDepthChargePrismTarget(),
                // Ice Shelf (iceShelfRoundSeconds) is timed, like Glasshouse, and is absent.
                // Scalar only, as for Cleave and Flatline: the real answer is per-intensity (GetShatterplanePrismTarget(int)).
                GameModes.Shatterplane              => GetShatterplanePrismTarget(),
                GameModes.Stingherd                 => GetStingherdStingTarget(),
                GameModes.Trawl                     => GetTrawlPrismTarget(),
                GameModes.Sirocco                   => GetSiroccoPrismTarget(),
                GameModes.Larder                    => GetLarderMissileTarget(),
                GameModes.Marionette                => GetMarionetteStrikeTarget(),
                GameModes.Lancefall                 => GetLancefallPrismTarget(),
                // Thornbush (thornbushRoundSeconds) is timed, Glasshouse's reason, and is absent.
                GameModes.Flitstrike                => GetFlitstrikeStrikeTarget(),
                GameModes.Woodpecker                => GetWoodpeckerPrismTarget(),
                // Honeycomb (honeycombRoundSeconds) is timed, Tapestry's reason, and is absent here too.
                GameModes.CleanerRam                => GetCleanerRamStrikeTarget(),
                // Reefbuild is TIMED (reefbuildRoundSeconds), Tapestry's reason: absent, reads as "no target".
                GameModes.Blastline                 => GetBlastlinePrismTarget(),
                GameModes.Snap                      => GetSnapHitTarget(),
                GameModes.Riptide                   => GetRiptidePrismTarget(),
                // Den Builder (denBuilderRoundSeconds) is timed, Glasshouse's reason, and absent here too.
                // Tapestry is TIMED (tapestryRoundSeconds): it has no race target, so it is
                // deliberately absent here and reads as "no target", like Bloomrush. Glasshouse
                // (glasshouseRoundSeconds), Chum (chumRoundSeconds), Starweave (starweaveRoundSeconds), Haul
                // (haulRoundSeconds), Hedgerow (hedgerowRoundSeconds), Longshot (longshotRoundSeconds) and Gorge
                // (gorgeRoundSeconds) are timed for the same reason and are absent too, and so is Palisade
                // (palisadeRoundSeconds).
                GameModes.Sentry                    => GetSentryZapTarget(),
                GameModes.CaveIn                    => GetCaveInPrismTarget(),
                // Tapestry is TIMED (tapestryRoundSeconds): it has no race target, so it is
                // deliberately absent here and reads as "no target", like Bloomrush. Glasshouse
                // (glasshouseRoundSeconds) and Warren (warrenRoundSeconds) are timed for the same
                // reason and are absent too.
                // Per PILOT, not a total - the race target scales with team size and is only known at
                // runtime, so the editor's authored-target readout shows the rate.
                GameModes.Broadside                 => GetBroadsidePointsPerPilot(),
                // A PERCENT of the final form's body, not a count: the readout shows the threshold.
                GameModes.Tandava                   => GetTandavaBreakPercent(),
                GameModes.TandavaSevering           => GetTandavaBreakPercent(),   // Tandava's fork: the same shatter
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
            SameInts(rampagePrismTargetByIntensity, rampagePrismTargetByIntensityBuild) &&
            cleavePrismTarget == cleavePrismTargetBuild &&
            SameInts(cleavePrismTargetByIntensity, cleavePrismTargetByIntensityBuild) &&
            wildlifeKillTarget == wildlifeKillTargetBuild &&
            dogFightPointTarget == dogFightPointTargetBuild &&
            grizzlyChargePointTarget == grizzlyChargePointTargetBuild &&
            bendsPointTarget == bendsPointTargetBuild &&
            scarabScrambleGoalTarget == scarabScrambleGoalTargetBuild &&
            astroLeagueGoalLimit == astroLeagueGoalLimitBuild &&
            salvoPrismTarget == salvoPrismTargetBuild &&
            switchbackGateTarget == switchbackGateTargetBuild &&
            waystationClusterTarget == waystationClusterTargetBuild &&
            breakwaterStationTarget == breakwaterStationTargetBuild &&
            breakwaterLaps == breakwaterLapsBuild &&
            skeinRingTarget == skeinRingTargetBuild &&
            headlongGateTarget == headlongGateTargetBuild &&
            redlineGateTarget == redlineGateTargetBuild &&
            grizzlyTimeGateTarget == grizzlyTimeGateTargetBuild &&
            slingshotGateTarget == slingshotGateTargetBuild &&
            warplineGateTarget == warplineGateTargetBuild &&
            thresherTimeGateTarget == thresherTimeGateTargetBuild &&
            wrasseRunGateTarget == wrasseRunGateTargetBuild &&
            burrowRunGateTarget == burrowRunGateTargetBuild &&
            scarabRallyGateTarget == scarabRallyGateTargetBuild &&
            serpentTimeGateTarget == serpentTimeGateTargetBuild &&
            brittleStarTimeGateTarget == brittleStarTimeGateTargetBuild &&
            lanternRunGateTarget == lanternRunGateTargetBuild &&
            thornlineGateTarget == thornlineGateTargetBuild &&
            spearlineGateTarget == spearlineGateTargetBuild &&
            decoyRunGateTarget == decoyRunGateTargetBuild &&
            breachGateTarget == breachGateTargetBuild &&
            slipLineGateTarget == slipLineGateTargetBuild &&
            perchRunGateTarget == perchRunGateTargetBuild &&
            regattaGateTarget == regattaGateTargetBuild &&
            hijackStealTarget == hijackStealTargetBuild &&
            tollwayTollTarget == tollwayTollTargetBuild &&
            wreckingBallPrismTarget == wreckingBallPrismTargetBuild &&
            undertowPointTarget == undertowPointTargetBuild &&
            dustupPointTarget == dustupPointTargetBuild &&
            serpentHoodSnipeTarget == serpentHoodSnipeTargetBuild &&
            jackstrawStrikeTarget == jackstrawStrikeTargetBuild &&
            riposteStrikeTarget == riposteStrikeTargetBuild &&
            bullrushGoreTarget == bullrushGoreTargetBuild &&
            windrowRoundSeconds == windrowRoundSecondsBuild &&
            landslidePrismTarget == landslidePrismTargetBuild &&
            flatlinePointTarget == flatlinePointTargetBuild &&
            SameInts(flatlinePointTargetByIntensity, flatlinePointTargetByIntensityBuild) &&
            kelpwoodBiteTarget == kelpwoodBiteTargetBuild &&
            coronaBloomTarget == coronaBloomTargetBuild &&
            shockfrontRippleTarget == shockfrontRippleTargetBuild &&
            fulmenPrismTarget == fulmenPrismTargetBuild &&
            railrushHitTarget == railrushHitTargetBuild &&
            standoffBlastTarget == standoffBlastTargetBuild &&
            harvestPrismTarget == harvestPrismTargetBuild &&
            shatterplanePrismTarget == shatterplanePrismTargetBuild &&
            SameInts(shatterplanePrismTargetByIntensity, shatterplanePrismTargetByIntensityBuild) &&
            stingherdStingTarget == stingherdStingTargetBuild &&
            trawlPrismTarget == trawlPrismTargetBuild &&
            tapestryRoundSeconds == tapestryRoundSecondsBuild &&
            palisadeRoundSeconds == palisadeRoundSecondsBuild &&
            glasshouseRoundSeconds == glasshouseRoundSecondsBuild &&
            chumRoundSeconds == chumRoundSecondsBuild &&
            starweaveRoundSeconds == starweaveRoundSecondsBuild &&
            haulRoundSeconds == haulRoundSecondsBuild &&
            hedgerowRoundSeconds == hedgerowRoundSecondsBuild &&
            longshotRoundSeconds == longshotRoundSecondsBuild &&
            marionetteStrikeTarget == marionetteStrikeTargetBuild &&
            gorgeRoundSeconds == gorgeRoundSecondsBuild &&
            podHuntBlastTarget == podHuntBlastTargetBuild &&
            iceShelfRoundSeconds == iceShelfRoundSecondsBuild &&
            depthChargePrismTarget == depthChargePrismTargetBuild &&
            larderMissileTarget == larderMissileTargetBuild &&
            thornbushRoundSeconds == thornbushRoundSecondsBuild &&
            lancefallPrismTarget == lancefallPrismTargetBuild &&
            flitstrikeStrikeTarget == flitstrikeStrikeTargetBuild &&
            honeycombRoundSeconds == honeycombRoundSecondsBuild &&
            woodpeckerPrismTarget == woodpeckerPrismTargetBuild &&
            sentryZapTarget == sentryZapTargetBuild &&
            warrenRoundSeconds == warrenRoundSecondsBuild &&
            caveInPrismTarget == caveInPrismTargetBuild &&
            cleanerRamStrikeTarget == cleanerRamStrikeTargetBuild &&
            reefbuildRoundSeconds == reefbuildRoundSecondsBuild &&
            blastlinePrismTarget == blastlinePrismTargetBuild &&
            snapHitTarget == snapHitTargetBuild &&
            denBuilderRoundSeconds == denBuilderRoundSecondsBuild &&
            riptidePrismTarget == riptidePrismTargetBuild &&
            siroccoPrismTarget == siroccoPrismTargetBuild &&
            broadsidePointsPerPilot == broadsidePointsPerPilotBuild &&
            tandavaBreakPercent == tandavaBreakPercentBuild &&
            tandavaHaloRingsToBreak == tandavaHaloRingsToBreakBuild;

        /// <summary>Copy the Build baseline onto the Live counts (build → live) - used by the build auto-restore.</summary>
        public void ApplyBuildValues()
        {
            hexRaceCrystalCount = hexRaceCrystalCountBuild;
            crystalCaptureCrystalCount = crystalCaptureCrystalCountBuild;
            joustCount = joustCountBuild;
            maelstromWinTarget = maelstromWinTargetBuild;
            nucleusRushWaveTarget = nucleusRushWaveTargetBuild;
            rampagePrismTarget = rampagePrismTargetBuild;
            rampagePrismTargetByIntensity = CopyInts(rampagePrismTargetByIntensityBuild);
            cleavePrismTarget = cleavePrismTargetBuild;
            cleavePrismTargetByIntensity = CopyInts(cleavePrismTargetByIntensityBuild);
            wildlifeKillTarget = wildlifeKillTargetBuild;
            dogFightPointTarget = dogFightPointTargetBuild;
            grizzlyChargePointTarget = grizzlyChargePointTargetBuild;
            bendsPointTarget = bendsPointTargetBuild;
            scarabScrambleGoalTarget = scarabScrambleGoalTargetBuild;
            astroLeagueGoalLimit = astroLeagueGoalLimitBuild;
            salvoPrismTarget = salvoPrismTargetBuild;
            switchbackGateTarget = switchbackGateTargetBuild;
            waystationClusterTarget = waystationClusterTargetBuild;
            breakwaterStationTarget = breakwaterStationTargetBuild;
            breakwaterLaps = breakwaterLapsBuild;
            skeinRingTarget = skeinRingTargetBuild;
            headlongGateTarget = headlongGateTargetBuild;
            redlineGateTarget = redlineGateTargetBuild;
            grizzlyTimeGateTarget = grizzlyTimeGateTargetBuild;
            slingshotGateTarget = slingshotGateTargetBuild;
            warplineGateTarget = warplineGateTargetBuild;
            thresherTimeGateTarget = thresherTimeGateTargetBuild;
            wrasseRunGateTarget = wrasseRunGateTargetBuild;
            burrowRunGateTarget = burrowRunGateTargetBuild;
            scarabRallyGateTarget = scarabRallyGateTargetBuild;
            serpentTimeGateTarget = serpentTimeGateTargetBuild;
            brittleStarTimeGateTarget = brittleStarTimeGateTargetBuild;
            lanternRunGateTarget = lanternRunGateTargetBuild;
            thornlineGateTarget = thornlineGateTargetBuild;
            spearlineGateTarget = spearlineGateTargetBuild;
            decoyRunGateTarget = decoyRunGateTargetBuild;
            breachGateTarget = breachGateTargetBuild;
            slipLineGateTarget = slipLineGateTargetBuild;
            perchRunGateTarget = perchRunGateTargetBuild;
            regattaGateTarget = regattaGateTargetBuild;
            hijackStealTarget = hijackStealTargetBuild;
            tollwayTollTarget = tollwayTollTargetBuild;
            wreckingBallPrismTarget = wreckingBallPrismTargetBuild;
            undertowPointTarget = undertowPointTargetBuild;
            dustupPointTarget = dustupPointTargetBuild;
            serpentHoodSnipeTarget = serpentHoodSnipeTargetBuild;
            jackstrawStrikeTarget = jackstrawStrikeTargetBuild;
            riposteStrikeTarget = riposteStrikeTargetBuild;
            bullrushGoreTarget = bullrushGoreTargetBuild;
            windrowRoundSeconds = windrowRoundSecondsBuild;
            landslidePrismTarget = landslidePrismTargetBuild;
            flatlinePointTarget = flatlinePointTargetBuild;
            flatlinePointTargetByIntensity = CopyInts(flatlinePointTargetByIntensityBuild);
            kelpwoodBiteTarget = kelpwoodBiteTargetBuild;
            coronaBloomTarget = coronaBloomTargetBuild;
            shockfrontRippleTarget = shockfrontRippleTargetBuild;
            fulmenPrismTarget = fulmenPrismTargetBuild;
            railrushHitTarget = railrushHitTargetBuild;
            standoffBlastTarget = standoffBlastTargetBuild;
            harvestPrismTarget = harvestPrismTargetBuild;
            shatterplanePrismTarget = shatterplanePrismTargetBuild;
            shatterplanePrismTargetByIntensity = CopyInts(shatterplanePrismTargetByIntensityBuild);
            stingherdStingTarget = stingherdStingTargetBuild;
            trawlPrismTarget = trawlPrismTargetBuild;
            tapestryRoundSeconds = tapestryRoundSecondsBuild;
            palisadeRoundSeconds = palisadeRoundSecondsBuild;
            glasshouseRoundSeconds = glasshouseRoundSecondsBuild;
            chumRoundSeconds = chumRoundSecondsBuild;
            starweaveRoundSeconds = starweaveRoundSecondsBuild;
            haulRoundSeconds = haulRoundSecondsBuild;
            hedgerowRoundSeconds = hedgerowRoundSecondsBuild;
            longshotRoundSeconds = longshotRoundSecondsBuild;
            marionetteStrikeTarget = marionetteStrikeTargetBuild;
            gorgeRoundSeconds = gorgeRoundSecondsBuild;
            podHuntBlastTarget = podHuntBlastTargetBuild;
            iceShelfRoundSeconds = iceShelfRoundSecondsBuild;
            depthChargePrismTarget = depthChargePrismTargetBuild;
            larderMissileTarget = larderMissileTargetBuild;
            thornbushRoundSeconds = thornbushRoundSecondsBuild;
            lancefallPrismTarget = lancefallPrismTargetBuild;
            flitstrikeStrikeTarget = flitstrikeStrikeTargetBuild;
            honeycombRoundSeconds = honeycombRoundSecondsBuild;
            woodpeckerPrismTarget = woodpeckerPrismTargetBuild;
            sentryZapTarget = sentryZapTargetBuild;
            warrenRoundSeconds = warrenRoundSecondsBuild;
            caveInPrismTarget = caveInPrismTargetBuild;
            cleanerRamStrikeTarget = cleanerRamStrikeTargetBuild;
            reefbuildRoundSeconds = reefbuildRoundSecondsBuild;
            blastlinePrismTarget = blastlinePrismTargetBuild;
            snapHitTarget = snapHitTargetBuild;
            denBuilderRoundSeconds = denBuilderRoundSecondsBuild;
            riptidePrismTarget = riptidePrismTargetBuild;
            siroccoPrismTarget = siroccoPrismTargetBuild;
            broadsidePointsPerPilot = broadsidePointsPerPilotBuild;
            tandavaBreakPercent = tandavaBreakPercentBuild;
            tandavaHaloRingsToBreak = tandavaHaloRingsToBreakBuild;
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
            rampagePrismTargetByIntensityBuild = CopyInts(rampagePrismTargetByIntensity);
            cleavePrismTargetBuild = cleavePrismTarget;
            cleavePrismTargetByIntensityBuild = CopyInts(cleavePrismTargetByIntensity);
            wildlifeKillTargetBuild = wildlifeKillTarget;
            dogFightPointTargetBuild = dogFightPointTarget;
            grizzlyChargePointTargetBuild = grizzlyChargePointTarget;
            bendsPointTargetBuild = bendsPointTarget;
            scarabScrambleGoalTargetBuild = scarabScrambleGoalTarget;
            astroLeagueGoalLimitBuild = astroLeagueGoalLimit;
            salvoPrismTargetBuild = salvoPrismTarget;
            switchbackGateTargetBuild = switchbackGateTarget;
            waystationClusterTargetBuild = waystationClusterTarget;
            breakwaterStationTargetBuild = breakwaterStationTarget;
            breakwaterLapsBuild = breakwaterLaps;
            skeinRingTargetBuild = skeinRingTarget;
            headlongGateTargetBuild = headlongGateTarget;
            redlineGateTargetBuild = redlineGateTarget;
            grizzlyTimeGateTargetBuild = grizzlyTimeGateTarget;
            slingshotGateTargetBuild = slingshotGateTarget;
            warplineGateTargetBuild = warplineGateTarget;
            thresherTimeGateTargetBuild = thresherTimeGateTarget;
            wrasseRunGateTargetBuild = wrasseRunGateTarget;
            burrowRunGateTargetBuild = burrowRunGateTarget;
            scarabRallyGateTargetBuild = scarabRallyGateTarget;
            serpentTimeGateTargetBuild = serpentTimeGateTarget;
            brittleStarTimeGateTargetBuild = brittleStarTimeGateTarget;
            lanternRunGateTargetBuild = lanternRunGateTarget;
            thornlineGateTargetBuild = thornlineGateTarget;
            spearlineGateTargetBuild = spearlineGateTarget;
            decoyRunGateTargetBuild = decoyRunGateTarget;
            breachGateTargetBuild = breachGateTarget;
            slipLineGateTargetBuild = slipLineGateTarget;
            perchRunGateTargetBuild = perchRunGateTarget;
            regattaGateTargetBuild = regattaGateTarget;
            hijackStealTargetBuild = hijackStealTarget;
            tollwayTollTargetBuild = tollwayTollTarget;
            wreckingBallPrismTargetBuild = wreckingBallPrismTarget;
            undertowPointTargetBuild = undertowPointTarget;
            dustupPointTargetBuild = dustupPointTarget;
            serpentHoodSnipeTargetBuild = serpentHoodSnipeTarget;
            jackstrawStrikeTargetBuild = jackstrawStrikeTarget;
            riposteStrikeTargetBuild = riposteStrikeTarget;
            bullrushGoreTargetBuild = bullrushGoreTarget;
            windrowRoundSecondsBuild = windrowRoundSeconds;
            landslidePrismTargetBuild = landslidePrismTarget;
            flatlinePointTargetBuild = flatlinePointTarget;
            flatlinePointTargetByIntensityBuild = CopyInts(flatlinePointTargetByIntensity);
            kelpwoodBiteTargetBuild = kelpwoodBiteTarget;
            coronaBloomTargetBuild = coronaBloomTarget;
            shockfrontRippleTargetBuild = shockfrontRippleTarget;
            fulmenPrismTargetBuild = fulmenPrismTarget;
            railrushHitTargetBuild = railrushHitTarget;
            standoffBlastTargetBuild = standoffBlastTarget;
            harvestPrismTargetBuild = harvestPrismTarget;
            shatterplanePrismTargetBuild = shatterplanePrismTarget;
            shatterplanePrismTargetByIntensityBuild = CopyInts(shatterplanePrismTargetByIntensity);
            stingherdStingTargetBuild = stingherdStingTarget;
            trawlPrismTargetBuild = trawlPrismTarget;
            tapestryRoundSecondsBuild = tapestryRoundSeconds;
            palisadeRoundSecondsBuild = palisadeRoundSeconds;
            glasshouseRoundSecondsBuild = glasshouseRoundSeconds;
            chumRoundSecondsBuild = chumRoundSeconds;
            starweaveRoundSecondsBuild = starweaveRoundSeconds;
            haulRoundSecondsBuild = haulRoundSeconds;
            hedgerowRoundSecondsBuild = hedgerowRoundSeconds;
            longshotRoundSecondsBuild = longshotRoundSeconds;
            marionetteStrikeTargetBuild = marionetteStrikeTarget;
            gorgeRoundSecondsBuild = gorgeRoundSeconds;
            podHuntBlastTargetBuild = podHuntBlastTarget;
            iceShelfRoundSecondsBuild = iceShelfRoundSeconds;
            depthChargePrismTargetBuild = depthChargePrismTarget;
            larderMissileTargetBuild = larderMissileTarget;
            thornbushRoundSecondsBuild = thornbushRoundSeconds;
            lancefallPrismTargetBuild = lancefallPrismTarget;
            flitstrikeStrikeTargetBuild = flitstrikeStrikeTarget;
            honeycombRoundSecondsBuild = honeycombRoundSeconds;
            woodpeckerPrismTargetBuild = woodpeckerPrismTarget;
            sentryZapTargetBuild = sentryZapTarget;
            warrenRoundSecondsBuild = warrenRoundSeconds;
            caveInPrismTargetBuild = caveInPrismTarget;
            cleanerRamStrikeTargetBuild = cleanerRamStrikeTarget;
            reefbuildRoundSecondsBuild = reefbuildRoundSeconds;
            blastlinePrismTargetBuild = blastlinePrismTarget;
            snapHitTargetBuild = snapHitTarget;
            denBuilderRoundSecondsBuild = denBuilderRoundSeconds;
            riptidePrismTargetBuild = riptidePrismTarget;
            siroccoPrismTargetBuild = siroccoPrismTarget;
            broadsidePointsPerPilotBuild = broadsidePointsPerPilot;
            tandavaBreakPercentBuild = tandavaBreakPercent;
            tandavaHaloRingsToBreakBuild = tandavaHaloRingsToBreak;
        }
    }
}
