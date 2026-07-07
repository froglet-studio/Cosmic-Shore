using System;
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Client;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.Engine;
using CosmicShore.Engine.Networking;
using CosmicShore.Engine.Services;
using CosmicShore.Engine.UI;
using CosmicShore.Gameplay;
using CosmicShore.UI;
using CosmicShore.Utility;
using Object = CosmicShore.Engine.Object;

namespace CosmicShore.Tests;

// ─────────────────────────────────────────────────────────────────────────────
// CONVERGENCE RUNGS 1+2 — the playable SkimRace client's simulation runs on the
// REAL ported systems. These tests drive SkimRaceFactory/SkimRaceDirector
// (Port/src/CosmicShore.Client/SkimRaceSim.cs) in-process, window-free:
//   • the rig is the genuine prefab family (VesselController/VesselStatus/
//     VesselTransformer/AIPilot/InputStatus + the CT1 contact rig),
//   • drift goes through VesselTransformer.BeginDrift/EndDrift (two-tier analog),
//   • boost goes through VesselStatus.IsBoosting (BoostActionSO shape),
//   • energy→top-speed goes through the real ThrottleScalerMultiplier hook,
//   • AI rivals claim crystals through the real trigger pass → OmniCrystalImpactor,
//   • (rung 2) trails are REAL Prisms spawned by the real VesselPrismController
//     (SkimRacePrismFactory answers the spawn channel) — conserved mass, no decay,
//   • (rung 2) trail-skim energy flows through the real skimmer contact pipeline:
//     trigger SphereCollider on the near-field Skimmer → engine TriggerPass →
//     SkimmerImpactor.AcceptImpactee → SkimRaceTrailSkimEnergyEffectSO,
//   • (rung 3) the whole CrystalImpactor family runs the course: Omni stations
//     claim through OmniCrystalImpactor (vessel contact), Team stations through
//     TeamCrystalImpactor (domain-locked), elemental stations through
//     ElementalCrystalImpactor (skimmer claim, consumed via the real fly-to-vessel
//     collection). Element levels move ONLY through the impactor-side effect SOs
//     (the real SkimmerAdjustElementLevelByCrystalEffectSO /
//     VesselIncrementLevelByCrystalEffectSO + the SkimRace race-rule effects), and
//     crystal lifetime runs the real Crystal.Respawn() → CrystalManager chain
//     (SkimRaceCrystalManager),
//   • (rung 4) scoring is the REAL game's: the ported NetworkCrystalCollisionTurnMonitor
//     + TurnMonitorController end the race through HexRaceScoringRuleSO's
//     domain-aggregated objective (ScoringMetrics.SumByDomain over the shared
//     GameDataSO RoundStats — teammates share their domain total), the rule assigns
//     golf scores (winners = finish time, losers = 10000 + domain deficit) and builds
//     the ranked Results, and the ported ElementalComebackSystem buffs trailing
//     DOMAINS through the elementals fundamental.
// No Silk type is touched — the windowing layer never loads here.
// ─────────────────────────────────────────────────────────────────────────────

public class ClientConvergenceTests : IDisposable
{
    public ClientConvergenceTests()
    {
        ClearProcessGlobals();
    }

    public void Dispose()
    {
        ClearProcessGlobals();
    }

    static void ClearProcessGlobals()
    {
        typeof(PlayerDataService).GetProperty("Instance")!.SetValue(null, null);
        typeof(AudioSystem).GetProperty("Instance")!.SetValue(null, null);
        // Prism spawning auto-creates these Singleton<T> statics, which survive
        // GameLoop disposal (same reset as PrismTests/SkimmerLayerTests).
        typeof(Singleton<PrismTimerManager>).GetProperty("Instance")!.SetValue(null, null);
        typeof(Singleton<PrismSpatialIndex>).GetProperty("Instance")!.SetValue(null, null);
        NetworkManager.Singleton = null;
    }

    /// <summary>Stops AI/spawn loops + flushes destroys so each test winds down cleanly.</summary>
    static void Teardown(GameLoop loop, SkimRaceDirector director)
    {
        director.Shutdown();  // async-void spawn loops + AI off — deterministic wind-down
        loop.Tick(1f / 60f);  // end-of-frame destroy flush
        loop.Dispose();
    }

    [Fact]
    public void Factory_BuildsTheRealRig_ForEveryPilot()
    {
        var (loop, director) = SkimRaceFactory.Create(seed: 7, trackCrystals: 24, rivalCount: 3);
        try
        {
            Assert.Equal(4, director.Pilots.Count);
            Assert.Equal(24, director.Track.Crystals.Count);
            Assert.Equal(24, director.WinTarget);

            var human = director.HumanPilot;
            Assert.False(human.Player.IsInitializedAsAI);
            for (int i = 1; i < director.Pilots.Count; i++)
                Assert.True(director.Pilots[i].Player.IsInitializedAsAI);

            foreach (var pilot in director.Pilots)
            {
                // The real vessel rig — flight, AI, contact, resources all genuine.
                var vesselGo = ((VesselController)pilot.Player.Vessel).gameObject;
                Assert.NotNull(vesselGo.GetComponent<VesselTransformer>());
                Assert.NotNull(vesselGo.GetComponent<AIPilot>());
                Assert.NotNull(vesselGo.GetComponent<ResourceSystem>());
                Assert.NotNull(vesselGo.GetComponent<VesselImpactor>());
                Assert.NotNull(vesselGo.GetComponent<ImpactCollider>());
                Assert.NotNull(vesselGo.GetComponent<SphereCollider>()); // CT1 contact bubble
                Assert.Equal(VesselClassType.Squirrel, pilot.Status.VesselType);

                // The real input path: Player → InputController → the V7 InputStatus.
                Assert.IsType<InputStatus>(pilot.Input);
            }

            // The course is live: every station has a real Crystal with a trigger rig.
            for (int i = 0; i < director.Track.Crystals.Count; i++)
                Assert.True(director.IsStationActive(i));
        }
        finally
        {
            Teardown(loop, director);
        }
    }

    [Fact]
    public void AIRivals_ClaimCrystals_ThroughTheRealContactPipeline()
    {
        var (loop, director) = SkimRaceFactory.Create(seed: 42, trackCrystals: 30, rivalCount: 3);
        try
        {
            director.SkipCountdown();
            loop.Tick(1f / 60f);
            Assert.Equal(RaceState.Racing, director.State);

            int frames = 1;
            const int maxFrames = 60 * 120; // 2 simulated minutes, fail-loud cap
            while (frames < maxFrames && director.TotalClaims < 3)
            {
                loop.Tick(1f / 60f);
                frames++;
            }

            Assert.True(director.TotalClaims >= 3,
                $"AI field claimed only {director.TotalClaims} crystals in {frames} frames.");

            // Claims landed on RoundStats through the StatsManager-shaped bookkeeping.
            int statTotal = 0;
            foreach (var pilot in director.Pilots)
                statTotal += pilot.Stats.CrystalsCollected;
            Assert.Equal(director.TotalClaims, statTotal);
        }
        finally
        {
            Teardown(loop, director);
        }
    }

    [Fact]
    public void DriftButtonEvents_DriveTheRealTwoTierAnalogDrift()
    {
        var (loop, director) = SkimRaceFactory.Create(seed: 7, trackCrystals: 12, rivalCount: 1);
        try
        {
            director.SkipCountdown();
            loop.Tick(1f / 60f);

            var human = director.HumanPilot;
            var input = human.Input;
            var transformer = human.Status.VesselTransformer;
            input.ActiveInputDevice = InputDeviceType.Gamepad; // analog triggers, no easing

            // Single trigger (tier 1): the strategy raises OnlyLeftStickAction.
            input.LeftTriggerAnalog = 1f;
            input.OnButtonPressed.Raise(InputEvents.OnlyLeftStickAction);
            Assert.True(human.Status.IsDrifting);
            Assert.True(transformer.IsDriftActive);

            loop.Tick(1f / 60f); // ApplyAnalogDrift scales rotation by the trigger sum
            Assert.Equal(130f * 1.5f, transformer.YawScaler, 3);   // DriftActionSO Mult default
            Assert.Equal(130f * 1.5f, transformer.PitchScaler, 3);
            Assert.Equal(1f, human.DriftAmount, 3);                // HUD gauge mirrors the sum

            // Second trigger joins (sharp tier): OnlyLeft releases, BothSticks presses.
            input.RightTriggerAnalog = 1f;
            input.OnButtonReleased.Raise(InputEvents.OnlyLeftStickAction);
            input.OnButtonPressed.Raise(InputEvents.BothSticksAction);
            Assert.True(human.Status.IsDrifting);
            loop.Tick(1f / 60f);
            Assert.Equal(130f * 1.5f, transformer.YawScaler, 3);   // same class-default Mult at sum=2

            // Release everything: gamepad path restores the base immediately.
            input.LeftTriggerAnalog = 0f;
            input.RightTriggerAnalog = 0f;
            input.OnButtonReleased.Raise(InputEvents.BothSticksAction);
            Assert.False(human.Status.IsDrifting);
            Assert.False(transformer.IsDriftActive);
            Assert.Equal(130f, transformer.YawScaler, 3);
            Assert.Equal(130f, transformer.RollScaler, 3);
        }
        finally
        {
            Teardown(loop, director);
        }
    }

    [Fact]
    public void EnergyAndBoost_DriveTheRealTransformerHooks()
    {
        var (loop, director) = SkimRaceFactory.Create(seed: 7, trackCrystals: 12, rivalCount: 1);
        try
        {
            director.SkipCountdown();
            loop.Tick(1f / 60f);

            var human = director.HumanPilot;
            var transformer = human.Status.VesselTransformer;

            // Energy → top speed through the REAL ThrottleScalerMultiplier ElementalFloat.
            human.Resources.SetResourceAmount(0, 0.5f);
            loop.Tick(1f / 60f);
            float energy = human.Resources.Resources[0].CurrentAmount;
            Assert.InRange(transformer.ThrottleScalerMultiplier.Value,
                1f + 0.6f * energy - 0.05f, 1f + 0.6f * energy + 0.05f);

            // Boost — BoostActionSO shape: Button1Action sets the intent, the director
            // gates VesselStatus.IsBoosting on energy and drains the bar.
            human.Input.OnButtonPressed.Raise(InputEvents.Button1Action);
            float before = human.Resources.Resources[0].CurrentAmount;
            loop.Tick(1f / 60f);
            Assert.True(human.Status.IsBoosting);
            Assert.True(human.Resources.Resources[0].CurrentAmount < before, "boost should drain energy");

            human.Input.OnButtonReleased.Raise(InputEvents.Button1Action);
            loop.Tick(1f / 60f);
            Assert.False(human.Status.IsBoosting);

            // The drained bar empties the boost gate too.
            human.Input.OnButtonPressed.Raise(InputEvents.Button1Action);
            human.Resources.SetResourceAmount(0, 0f);
            loop.Tick(1f / 60f);
            Assert.False(human.Status.IsBoosting); // no energy → no boost
        }
        finally
        {
            Teardown(loop, director);
        }
    }

    [Fact]
    public void RestartRace_ResetsTheField_ThroughTheRealResetPath()
    {
        var (loop, director) = SkimRaceFactory.Create(seed: 11, trackCrystals: 12, rivalCount: 2);
        try
        {
            director.SkipCountdown();
            int frames = 0;
            while (frames < 60 * 120 && director.TotalClaims < 1)
            {
                loop.Tick(1f / 60f);
                frames++;
            }
            Assert.True(director.TotalClaims >= 1, "expected at least one claim before the restart");

            // Snapshot a surviving prism so the restart's active force is observable.
            Prism survivor = null;
            foreach (var pilot in director.Pilots)
                if (survivor == null && pilot.TrailPrisms.Count > 0)
                    survivor = pilot.TrailPrisms[0];

            director.RestartRace();
            Assert.Equal(RaceState.Countdown, director.State);
            Assert.Equal(0, director.TotalClaims);
            Assert.Equal(-1, director.WinnerPilot);
            foreach (var pilot in director.Pilots)
            {
                Assert.Equal(0, pilot.Stats.CrystalsCollected); // RoundStats.Cleanup ran
                Assert.Empty(pilot.TrailPrisms);                // active force wiped the prismscape
                Assert.True(pilot.Status.IsStationary);         // grid hold until GO
            }
            if (survivor != null)
            {
                loop.Tick(1f / 60f); // end-of-frame destroy flush
                Assert.True(survivor.IsDestroyed); // the prism GameObjects are gone, not just unlisted
            }
            for (int i = 0; i < director.Track.Crystals.Count; i++)
                Assert.True(director.IsStationActive(i));       // fresh course

            // The countdown releases the field again.
            director.SkipCountdown();
            loop.Tick(1f / 60f);
            Assert.Equal(RaceState.Racing, director.State);
            foreach (var pilot in director.Pilots)
                Assert.False(pilot.Status.IsStationary);
        }
        finally
        {
            Teardown(loop, director);
        }
    }

    // ── rung 2: real prism trails ────────────────────────────────────────────

    [Fact]
    public void Prisms_SpawnThroughTheRealController_WhileTheFieldFlies()
    {
        var (loop, director) = SkimRaceFactory.Create(seed: 7, trackCrystals: 12, rivalCount: 2);
        try
        {
            director.SkipCountdown();

            // VesselPrismController.StartSpawn waits startDelay (2.1s) then spawns while
            // Speed > 3 — fly 8 simulated seconds and the whole field lays real prisms.
            for (int frame = 0; frame < 60 * 8; frame++) loop.Tick(1f / 60f);

            foreach (var pilot in director.Pilots)
            {
                int midCount = pilot.TrailPrisms.Count;
                Assert.True(midCount > 3,
                    $"{pilot.Player.Name} spawned only {midCount} prisms after 8s of flight.");

                // The blocks are REAL prisms carrying the pilot's identity + domain.
                var prism = pilot.TrailPrisms[0];
                Assert.Equal(pilot.Player.Name, prism.ownerID);
                Assert.Equal(pilot.Domain, prism.Domain);
                Assert.NotNull(prism.GetComponent<PrismImpactor>()); // contact rig present
                Assert.NotNull(prism.GetComponent<BoxCollider>());
            }

            // …and the count keeps growing while they keep flying (live spawn loop).
            var before = new int[director.Pilots.Count];
            for (int i = 0; i < director.Pilots.Count; i++)
                before[i] = director.Pilots[i].TrailPrisms.Count;
            for (int frame = 0; frame < 60 * 4; frame++) loop.Tick(1f / 60f);
            for (int i = 0; i < director.Pilots.Count; i++)
                Assert.True(director.Pilots[i].TrailPrisms.Count > before[i],
                    $"{director.Pilots[i].Player.Name}'s trail stopped growing.");
        }
        finally
        {
            Teardown(loop, director);
        }
    }

    [Fact]
    public void Prisms_AreConservedMass_NothingDecaysThem()
    {
        var (loop, director) = SkimRaceFactory.Create(seed: 11, trackCrystals: 12, rivalCount: 1);
        try
        {
            director.SkipCountdown();
            for (int frame = 0; frame < 60 * 8; frame++) loop.Tick(1f / 60f);

            // Freeze production (spawn loops off) but keep the world ticking.
            foreach (var pilot in director.Pilots)
                pilot.Status.VesselPrismController.StopSpawn();
            loop.Tick(1f / 60f);

            var counts = new int[director.Pilots.Count];
            for (int i = 0; i < director.Pilots.Count; i++)
                counts[i] = director.Pilots[i].TrailPrisms.Count;
            Assert.True(counts[0] + counts[1] > 10, "expected a laid prismscape before the soak");

            // Two simulated minutes of pure time. Mass is conserved: no aging, no decay,
            // no culler — every prism survives untouched.
            for (int frame = 0; frame < 240; frame++) loop.Tick(0.5f);

            for (int i = 0; i < director.Pilots.Count; i++)
            {
                var pilot = director.Pilots[i];
                Assert.Equal(counts[i], pilot.TrailPrisms.Count);
                foreach (var prism in pilot.TrailPrisms)
                {
                    Assert.True((bool)prism, "a prism GameObject vanished without an active force");
                    Assert.False(prism.destroyed, "a prism was destroyed without an active force");
                }
            }
        }
        finally
        {
            Teardown(loop, director);
        }
    }

    // ── rung 2: real skimmer contact ─────────────────────────────────────────

    [Fact]
    public void SkimmerTriggerEnter_OnARivalsPrism_GrantsEnergy()
    {
        var (loop, director) = SkimRaceFactory.Create(seed: 5, trackCrystals: 8, rivalCount: 1);
        try
        {
            director.SkipCountdown();
            loop.Tick(1f / 60f);

            // Let the rival lay real trail, then find a block whose collider has armed
            // (waitTillOutsideSkimmer keeps a fresh block off until its spawner clears it).
            var rival = director.Pilots[1];
            int frames = 0;
            while (frames < 60 * 60 && rival.TrailPrisms.Count < 12)
            {
                loop.Tick(1f / 60f);
                frames++;
            }
            Assert.True(rival.TrailPrisms.Count >= 12, "rival laid no trail to skim");

            Prism target = null;
            foreach (var prism in rival.TrailPrisms)
            {
                if (!prism || prism.destroyed) continue;
                if (prism.GetComponent<BoxCollider>().enabled) { target = prism; break; }
            }
            Assert.NotNull(target);

            // Park the human's rig on the rival's prism: the near-field skimmer's trigger
            // sphere overlaps the prism's BoxCollider, so the next TriggerPass dispatches
            // a REAL OnTriggerEnter into SkimmerImpactor → the skim-energy effect.
            var human = director.HumanPilot;
            human.Status.IsStationary = true; // hold position — contact, not flight, under test
            human.Player.Vessel.SetPose(new Pose
            {
                position = target.transform.position,
                rotation = Quaternion.identity,
            });
            human.Resources.SetResourceAmount(0, 0.2f);
            float before = human.Resources.Resources[0].CurrentAmount;

            loop.Tick(1f / 60f); // trigger pass fires the enter(s)
            loop.Tick(1f / 60f); // director mirrors tracker state into the HUD readouts

            float gained = human.Resources.Resources[0].CurrentAmount - before;
            // Passive regen over 2 frames is ~0.0013; one real prism enter grants ≥0.025
            // (the rung-4 energy balance — see SkimRaceTrailSkimEnergyEffectSO.GainPerPrism).
            Assert.True(gained >= 0.02f,
                $"expected the real skimmer contact to charge the bar, gained {gained:F4}");
            Assert.True(human.SkimTracker.IsSkimming, "tracker should hold live prism contact");
            Assert.True(human.IsSkimming, "director should mirror the live skim state");
            Assert.True(human.SkimStrength > 0f);
        }
        finally
        {
            Teardown(loop, director);
        }
    }

    // ── rung 3: the real CrystalImpactor family runs the course ─────────────

    static int FindStation(SkimRaceDirector director, StationKind kind)
    {
        for (int i = 0; i < director.Track.Crystals.Count; i++)
            if (director.Crystals.KindOf(i) == kind)
                return i;
        throw new InvalidOperationException($"track has no {kind} station");
    }

    static int[] Levels(SkimRacePilot pilot) => new[]
    {
        pilot.Resources.GetLevel(Element.Charge),
        pilot.Resources.GetLevel(Element.Mass),
        pilot.Resources.GetLevel(Element.Space),
        pilot.Resources.GetLevel(Element.Time),
    };

    static readonly Element[] FourElements =
        { Element.Charge, Element.Mass, Element.Space, Element.Time };

    /// <summary>Park the pilot's rig exactly on a world position (contact, not flight, under test).</summary>
    static void Park(SkimRacePilot pilot, Vector3 position)
    {
        pilot.Status.IsStationary = true;
        pilot.Player.Vessel.SetPose(new Pose
        {
            position = position,
            rotation = Quaternion.identity,
        });
    }

    [Fact]
    public void ElementalCrystalClaim_RaisesExactlyItsElement_ThroughTheRealImpactorPipeline()
    {
        var (loop, director) = SkimRaceFactory.Create(seed: 7, trackCrystals: 16, rivalCount: 1);
        try
        {
            director.SkipCountdown();
            loop.Tick(1f / 60f);

            // An elemental station: skimmer-claimed, uncommitted (Blue) domain.
            int station = FindStation(director, StationKind.Elemental);
            var element = director.Track.StationElements[station];
            Assert.NotEqual(Element.Omni, element);
            var crystal = director.Crystals.GetStationCrystal(station);

            var human = director.HumanPilot;
            Park(human, crystal.transform.position); // skimmer trigger overlaps the crystal trigger
            var before = Levels(human);
            human.Resources.SetResourceAmount(0, 0.2f); // headroom so the energy kicker is observable
            float energyBefore = human.Resources.Resources[0].CurrentAmount;

            loop.Tick(1f / 60f); // trigger pass → ElementalCrystalImpactor.AcceptImpactee
            loop.Tick(1f / 60f);

            // EXACTLY the crystal's element rose, by exactly one integer level — granted
            // by the real SkimmerAdjustElementLevelByCrystalEffectSO inside the impactor
            // dispatch (scale 1 × 0.1/unit = one level), nothing director-side.
            var after = Levels(human);
            for (int i = 0; i < FourElements.Length; i++)
                Assert.Equal(before[i] + (FourElements[i] == element ? 1 : 0), after[i]);

            // StatsManager-shaped bookkeeping followed the claim report.
            Assert.Equal(1, human.Stats.CrystalsCollected);
            Assert.Equal(1, human.Stats.ElementalCrystalsCollected);
            Assert.Equal(0, human.Stats.OmniCrystalsCollected);
            // The race-rule energy kicker ran inside the same chain (≥0.15 per elemental
            // claim; passive regen over 2 frames is ~0.0013).
            Assert.True(human.Resources.Resources[0].CurrentAmount >= energyBefore + 0.14f);

            // The real collection: the station goes dark and the crystal flies to its
            // claimer, destroying itself after the flight (consumed — never relocated).
            Assert.False(director.IsStationActive(station));
            Assert.False(crystal.IsDestroyed); // mid-flight to the vessel
            for (int frame = 0; frame < 70; frame++) loop.Tick(1f / 60f); // 0.9s flight + slack
            Assert.True(crystal.IsDestroyed);

            // Respawn semantics for consumed crystals: a FRESH crystal takes the station
            // after the respawn window, carrying the station's element.
            Park(human, crystal.transform.position + new Vector3(0f, 500f, 0f)); // clear of the course
            int frames2 = 0;
            while (frames2 < 60 * 20 && !director.IsStationActive(station))
            {
                loop.Tick(1f / 60f);
                frames2++;
            }
            Assert.True(director.IsStationActive(station), "consumed elemental station must respawn");
            var fresh = director.Crystals.GetStationCrystal(station);
            Assert.NotSame(crystal, fresh);
            Assert.Equal(element, fresh.crystalProperties.Element);
        }
        finally
        {
            Teardown(loop, director);
        }
    }

    [Fact]
    public void OmniCrystalClaim_SurvivesAndRelights_ThroughTheRealRespawnChain()
    {
        var (loop, director) = SkimRaceFactory.Create(seed: 7, trackCrystals: 16, rivalCount: 1);
        try
        {
            director.SkipCountdown();
            loop.Tick(1f / 60f);

            int station = FindStation(director, StationKind.Omni);
            var crystal = director.Crystals.GetStationCrystal(station);

            var human = director.HumanPilot;
            Park(human, crystal.transform.position); // vessel contact bubble in the claim surface
            var before = Levels(human);

            loop.Tick(1f / 60f); // trigger pass → OmniCrystalImpactor.AcceptImpactee
            loop.Tick(1f / 60f);

            // The omni surge ran through the impactor pipeline: all four elements +1.
            var after = Levels(human);
            for (int i = 0; i < FourElements.Length; i++)
                Assert.Equal(before[i] + 1, after[i]);
            Assert.Equal(1, human.Stats.CrystalsCollected);
            Assert.Equal(1, human.Stats.OmniCrystalsCollected);

            // The REAL respawn semantics (Crystal.Respawn → CrystalManager): the SAME
            // crystal survives the claim — dark (not claimable), never destroyed.
            Assert.False(director.IsStationActive(station));
            Assert.False(crystal.IsDestroyed);
            Assert.False(crystal.GetComponent<SphereCollider>().enabled);

            // …and relights in place after the respawn window, claimable again.
            Park(human, crystal.transform.position + new Vector3(0f, 500f, 0f)); // clear of the course
            int frames = 0;
            while (frames < 60 * 20 && !director.IsStationActive(station))
            {
                loop.Tick(1f / 60f);
                frames++;
            }
            Assert.True(director.IsStationActive(station), "claimed omni station must relight");
            Assert.Same(crystal, director.Crystals.GetStationCrystal(station));
            Assert.True(crystal.GetComponent<SphereCollider>().enabled);
            Assert.True(frames >= (int)(SkimRaceDirector.CrystalRespawnSeconds * 60) - 240,
                "the dark window must hold for roughly the respawn time");

            // Claimable again: park back on it and take it a second time.
            Park(human, crystal.transform.position);
            loop.Tick(1f / 60f);
            loop.Tick(1f / 60f);
            Assert.Equal(2, human.Stats.CrystalsCollected);
            Assert.False(director.IsStationActive(station));
        }
        finally
        {
            Teardown(loop, director);
        }
    }

    [Fact]
    public void TeamCrystals_AreDomainLocked_InCoursesAndClaims()
    {
        var (loop, director) = SkimRaceFactory.Create(seed: 7, trackCrystals: 24, rivalCount: 3);
        try
        {
            // Team slots at stations 3/10/17, locked to the racing domains in order.
            Assert.Equal(StationKind.Team, director.Crystals.KindOf(3));
            Assert.Equal(StationKind.Team, director.Crystals.KindOf(10));
            Assert.Equal(StationKind.Team, director.Crystals.KindOf(17));
            Assert.Equal(Domains.Jade, director.Crystals.StationDomain(3));
            Assert.Equal(Domains.Ruby, director.Crystals.StationDomain(10));
            Assert.Equal(Domains.Gold, director.Crystals.StationDomain(17));

            director.SkipCountdown();
            loop.Tick(1f / 60f);

            // The crystals carry the domain (the real Crystal.ChangeDomain entry), and
            // every pilot's course includes exactly the crystals it may claim
            // (Crystal.CanBeCollected — Blue for everyone, team locks for the match).
            foreach (var pilot in director.Pilots)
            {
                foreach (int station in new[] { 3, 10, 17 })
                {
                    var crystal = director.Crystals.GetStationCrystal(station);
                    Assert.Equal(director.Crystals.StationDomain(station), crystal.ownDomain);
                    Assert.Equal(crystal.CanBeCollected(pilot.Domain),
                        pilot.Course.Crystals.Contains(crystal));
                }
            }

            // Mismatched domain cannot claim: the Jade human parks on the Ruby station —
            // TeamCrystalImpactor.IsDomainMatching rejects it through the real dispatch.
            var human = director.HumanPilot;
            var rubyCrystal = director.Crystals.GetStationCrystal(10);
            Park(human, rubyCrystal.transform.position);
            for (int frame = 0; frame < 5; frame++) loop.Tick(1f / 60f);
            Assert.True(director.IsStationActive(10));
            Assert.Equal(0, human.Stats.CrystalsCollected);

            // The matching domain claims it, and the REAL
            // VesselIncrementLevelByCrystalEffectSO grants exactly the crystal's element.
            var jadeCrystal = director.Crystals.GetStationCrystal(3);
            var element = director.Track.StationElements[3];
            var before = Levels(human);
            Park(human, jadeCrystal.transform.position);
            for (int frame = 0; frame < 5; frame++) loop.Tick(1f / 60f);

            Assert.False(director.IsStationActive(3));
            Assert.Equal(1, human.Stats.CrystalsCollected);
            var after = Levels(human);
            for (int i = 0; i < FourElements.Length; i++)
                Assert.Equal(before[i] + (FourElements[i] == element ? 1 : 0), after[i]);
        }
        finally
        {
            Teardown(loop, director);
        }
    }

    // ── rung 4: real scoring — the rule's domain-aggregated objective ───────

    [Fact]
    public void RaceEnd_FiresThroughTheDomainAggregatedScoringRule_NotADirectorCount()
    {
        var (loop, director) = SkimRaceFactory.Create(seed: 7, trackCrystals: 8, rivalCount: 3);
        try
        {
            // 4 pilots balanced over the ACTIVE domains (the GetBalancedDomain shape):
            // Jade (human), Ruby, Gold, and a Jade TEAMMATE for the human.
            Assert.Equal(Domains.Jade, director.Pilots[0].Domain);
            Assert.Equal(Domains.Ruby, director.Pilots[1].Domain);
            Assert.Equal(Domains.Gold, director.Pilots[2].Domain);
            Assert.Equal(Domains.Jade, director.Pilots[3].Domain);

            director.SkipCountdown();
            loop.Tick(1f / 60f); // BeginRacing → StartTurn → TurnMonitorController arms the monitor
            Assert.Equal(RaceState.Racing, director.State);

            // The REAL monitor published the crystal target into the shared GameDataSO.
            Assert.Equal(director.WinTarget, director.GameData.CrystalTargetCount);

            // A few racing frames so the finish time is non-zero.
            for (int frame = 0; frame < 5; frame++) loop.Tick(1f / 60f);

            // Split the target across the two Jade teammates — NO individual reaches it,
            // so a per-pilot director count could never end this race. The end must come
            // from HexRaceScoringRuleSO.IsObjectiveReached over ScoringMetrics.SumByDomain.
            director.Pilots[0].Stats.CrystalsCollected = director.WinTarget - 3;
            director.Pilots[3].Stats.CrystalsCollected = 3;

            loop.Tick(1f / 60f); // TurnMonitorController.Update → rule objective → turn end
            loop.Tick(1f / 60f);

            Assert.Equal(RaceState.Finished, director.State);
            Assert.Equal(0, director.TotalClaims); // proves no claim/director count was involved
            Assert.False(director.GameData.IsTurnRunning);
            Assert.Equal(Domains.Jade, director.GameData.WinnerDomain);

            // Representative winner = best contributor on the winning domain (rank 1 of Results).
            Assert.Equal(director.Pilots[0].Player.Name, director.GameData.WinnerName);
            Assert.Equal(0, director.WinnerPilot);
            Assert.NotEmpty(director.GameData.Results);

            // Both Jade teammates carry the same finish time; finish times are real (non-sentinel).
            float finishTime = director.Pilots[0].Stats.Score;
            Assert.True(finishTime > 0f, "finish time accrues from the racing ticks");
            Assert.True(GolfScoreSentinels.IsFinishTime(finishTime));
            Assert.Equal(finishTime, director.Pilots[3].Stats.Score);
        }
        finally
        {
            Teardown(loop, director);
        }
    }

    [Fact]
    public void GolfStandings_WinnersCarryFinishTime_LosersTieOnDomainDeficit()
    {
        var (loop, director) = SkimRaceFactory.Create(seed: 9, trackCrystals: 10, rivalCount: 4);
        try
        {
            // 5 pilots over 3 active domains: Jade (human), Ruby, Gold, Jade, Ruby.
            var jadeA = director.Pilots[0];
            var ruby1 = director.Pilots[1];
            var gold = director.Pilots[2];
            var jadeB = director.Pilots[3];
            var ruby2 = director.Pilots[4];
            Assert.Equal(Domains.Ruby, ruby2.Domain);
            Assert.Equal(Domains.Jade, jadeB.Domain);

            director.SkipCountdown();
            loop.Tick(1f / 60f);
            for (int frame = 0; frame < 5; frame++) loop.Tick(1f / 60f);

            ruby1.Stats.CrystalsCollected = 2;                     // Ruby sum 3 → deficit 7
            ruby2.Stats.CrystalsCollected = 1;
            jadeA.Stats.CrystalsCollected = 1;                     // Jade sum 1 → deficit 9
            gold.Stats.CrystalsCollected = director.WinTarget;     // Gold wins alone

            loop.Tick(1f / 60f);
            loop.Tick(1f / 60f);

            Assert.Equal(RaceState.Finished, director.State);
            Assert.Equal(Domains.Gold, director.GameData.WinnerDomain);

            // Winning-domain players carry the finish time (a real time, not a sentinel).
            float finishTime = gold.Stats.Score;
            Assert.True(GolfScoreSentinels.IsFinishTime(finishTime));
            Assert.True(finishTime > 0f);

            // Losing-domain players carry 10000 + the TEAM deficit — and tie within a domain.
            Assert.Equal(GolfScoreSentinels.EncodeHexRaceLoserScore(7), ruby1.Stats.Score);
            Assert.Equal(ruby1.Stats.Score, ruby2.Stats.Score);
            Assert.Equal(GolfScoreSentinels.EncodeHexRaceLoserScore(9), jadeA.Stats.Score);
            Assert.Equal(jadeA.Stats.Score, jadeB.Stats.Score);

            // The scoreboard order IS gameData.RoundStatsList — golf-sorted ascending
            // by the real SortRoundStats(golfRules: true) after the rule's AssignScores.
            var list = director.GameData.RoundStatsList;
            for (int i = 1; i < list.Count; i++)
                Assert.True(list[i - 1].Score <= list[i].Score, "RoundStatsList must be golf-sorted");
            Assert.Equal(Domains.Gold, list[0].Domain);

            // The ranked Results agree (rule.BuildResults → gameData.SetResults).
            Assert.Equal(list.Count, director.GameData.Results.Count);
            Assert.Equal(1, director.GameData.Results[0].Rank);
            Assert.Equal(gold.Player.Name, director.GameData.Results[0].Name);
            Assert.Equal(gold.Player.Name, director.GameData.WinnerName);
        }
        finally
        {
            Teardown(loop, director);
        }
    }

    // ── rung 4: the real comeback system buffs trailing DOMAINS ─────────────

    [Fact]
    public void ComebackSystem_BuffsTrailingDomains_NeverTheLeader()
    {
        var (loop, director) = SkimRaceFactory.Create(seed: 13, trackCrystals: 30, rivalCount: 2);
        try
        {
            director.SkipCountdown();
            loop.Tick(1f / 60f); // BeginRacing → StartTurn → comeback captures baselines

            var human = director.HumanPilot;      // Jade — solo on its domain here
            var ruby = director.Pilots[1];        // Ruby — will trail
            human.Stats.CrystalsCollected = 4;    // Jade leads; target 30 keeps the race running

            // The REAL turn-monitor display channel reflects the local domain's deficit.
            Assert.Equal(director.WinTarget - 4, director.RemainingToTarget);

            // Cross the comeback system's 1s update interval.
            for (int frame = 0; frame < 70; frame++) loop.Tick(1f / 60f);

            // Trailing Ruby rises through the elementals fundamental: Space/Time levels
            // grow with the TEAM deficit × the real HexRaceComebackProfile Squirrel
            // weights (3 levels per crystal of deficit; Mass/Charge weights are 0).
            Assert.True(ruby.Resources.GetLevel(Element.Space) >= 5,
                $"trailing domain should be Space-buffed, got {ruby.Resources.GetLevel(Element.Space)}");
            Assert.True(ruby.Resources.GetLevel(Element.Time) >= 5,
                $"trailing domain should be Time-buffed, got {ruby.Resources.GetLevel(Element.Time)}");

            // The LEADING domain gets nothing — the deficit is the team's, not personal.
            Assert.Equal(0, human.Resources.GetLevel(Element.Space));
            Assert.Equal(0, human.Resources.GetLevel(Element.Time));
        }
        finally
        {
            Teardown(loop, director);
        }
    }

    // ── rung 4: the whole race still plays on the real systems ──────────────

    [Fact]
    public void FullShortRace_ReachesFinished_ThroughTheRealScoringPipeline()
    {
        var (loop, director) = SkimRaceFactory.Create(seed: 42, trackCrystals: 8, rivalCount: 1);
        try
        {
            director.SkipCountdown();
            loop.Tick(1f / 60f);
            director.EngageHumanAutopilot(); // both rigs race via the real AIPilot

            int frames = 0;
            const int maxFrames = 60 * 600; // 10 simulated minutes, fail-loud cap
            while (frames < maxFrames && director.State != RaceState.Finished)
            {
                loop.Tick(1f / 60f);
                frames++;
            }

            Assert.Equal(RaceState.Finished, director.State);

            var gameData = director.GameData;
            // The objective is the rule's domain aggregate against the monitor's target.
            Assert.Equal(director.WinTarget, gameData.CrystalTargetCount);
            Assert.True(GameDataSO.IsActiveDomain(gameData.WinnerDomain, gameData.RequestedDomainCount));
            Assert.True(gameData.SumCrystalsCollectedByDomain(gameData.WinnerDomain) >= director.WinTarget);

            // Golf scores: winning-domain rows carry the finish time, losers the
            // 10000+deficit sentinel; RoundStatsList is the sorted scoreboard.
            float finishTime = gameData.RoundStatsList[0].Score;
            Assert.True(GolfScoreSentinels.IsFinishTime(finishTime));
            Assert.InRange(finishTime, 0f, frames / 60f + 1f);
            foreach (var stats in gameData.RoundStatsList)
            {
                if (stats.Domain == gameData.WinnerDomain)
                    Assert.Equal(finishTime, stats.Score);
                else
                    Assert.True(GolfScoreSentinels.IsHexRaceLoserScore(stats.Score));
            }

            // Results mirror the golf order with 1-based ranks; the representative
            // winner is a real pilot of the winning domain.
            Assert.Equal(gameData.RoundStatsList.Count, gameData.Results.Count);
            Assert.Equal(1, gameData.Results[0].Rank);
            Assert.Equal(gameData.WinnerName, gameData.Results[0].Name);
            Assert.InRange(director.WinnerPilot, 0, director.Pilots.Count - 1);
            Assert.Equal(gameData.WinnerDomain, director.Pilots[director.WinnerPilot].Domain);

            // The race was run on a REAL prismscape the whole way.
            int totalPrisms = 0;
            foreach (var pilot in director.Pilots) totalPrisms += pilot.TrailPrisms.Count;
            Assert.True(totalPrisms > 100, $"expected a substantial prismscape, got {totalPrisms}");
        }
        finally
        {
            Teardown(loop, director);
        }
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// FREESTYLE (lava-lamp) MODE — the client's Menu_Main counterpart, window-free:
//   • the local vessel spawns through the REAL initializer chain
//     (Player.OnNetworkSpawn → NetcodeHooks → ServerPlayerVesselInitializer →
//     ClientPlayerVesselInitializer.InitializePair →
//     MenuServerPlayerVesselInitializer.ActivateAutopilot) — autopilot default ON,
//   • ONE living Cell wired the standard way (CellConfigDataSO + SpawnProfileSO +
//     CapsuleMembrane + SnowChanger cytoplasm) seeds flora + fauna through the
//     REAL spawner — the Cell owns the environment,
//   • the REAL ToyboxController places the painting / vessel-changer /
//     domain-changer toys on the membrane ring, gated on the freestyle events,
//   • domain changes flow through Player.RequestSetDomain_ServerRpc (server-write
//     NetDomain → Domain mirror — the theme reads the live mirror),
//   • conserved mass: no trail caps, no TTLs, no cullers — the soak proves that
//     with every active force removed, populations never change at all.
// ─────────────────────────────────────────────────────────────────────────────

public class FreestyleConvergenceTests : IDisposable
{
    public FreestyleConvergenceTests()
    {
        ClearProcessGlobals();
    }

    public void Dispose()
    {
        ClearProcessGlobals();
    }

    static void ClearProcessGlobals()
    {
        typeof(PlayerDataService).GetProperty("Instance")!.SetValue(null, null);
        typeof(AudioSystem).GetProperty("Instance")!.SetValue(null, null);
        typeof(Singleton<PrismTimerManager>).GetProperty("Instance")!.SetValue(null, null);
        typeof(Singleton<PrismSpatialIndex>).GetProperty("Instance")!.SetValue(null, null);
        AuthenticationService.Reset();
        NetworkManager.Singleton = null;
    }

    /// <summary>Freestyle wind-down: director Shutdown (spawn loops, cell coroutines,
    /// initializer CTS, AI) + destroy-queue flush before the loop goes away.</summary>
    static void Teardown(GameLoop loop, FreestyleDirector director)
    {
        director.Shutdown();
        loop.Tick(1f / 60f);
        loop.Dispose();
    }

    static void RunSeconds(GameLoop loop, float seconds, float dt = 1f / 30f)
    {
        int frames = (int)MathF.Ceiling(seconds / dt) + 1;
        for (int i = 0; i < frames; i++) loop.Tick(dt);
    }

    /// <summary>Direct children of a transform (Transform is non-generic IEnumerable).</summary>
    static List<Transform> Children(Transform t)
    {
        var list = new List<Transform>();
        foreach (Transform child in t) list.Add(child);
        return list;
    }

    static string LabelText(Transform toyRoot) =>
        toyRoot.gameObject.GetComponentInChildren<TMP_Text>(includeInactive: true)?.text;

    /// <summary>Park the local vessel exactly on a world position (contact under test).</summary>
    static void Park(FreestyleDirector director, Vector3 position)
    {
        var status = director.VesselStatus;
        status.IsStationary = true;
        director.LocalPlayer.Vessel.SetPose(new Pose
        {
            position = position,
            rotation = Quaternion.identity,
        });
    }

    // ── (a) the factory builds the whole scene on the real systems ──────────

    [Fact]
    public void Factory_BuildsFreestyleScene_WithLiveCellAndInitializerChainVessel()
    {
        var (loop, director) = FreestyleFactory.Create(seed: 7);
        try
        {
            // Run the networked spawn chain (pre/postSpawnDelay ≈ 400ms) + ecology seeding.
            RunSeconds(loop, 4f);

            // The vessel arrived through the REAL initializer chain.
            var player = Assert.IsType<Player>(director.LocalPlayer);
            var vessel = Assert.IsType<VesselController>(player.Vessel);
            Assert.Equal(VesselClassType.Squirrel, vessel.VesselStatus.VesselType);
            Assert.True(vessel.IsSpawned);
            Assert.Equal(vessel.NetworkObjectId, player.NetVesselId.Value);
            Assert.Same(player, director.GameData.LocalPlayer);

            // Menu semantics: autopilot ON (the lava lamp), domain reset to Jade
            // server-side, input paused (the window is the single input driver).
            Assert.True(vessel.VesselStatus.AutoPilotEnabled);
            Assert.True(director.AutopilotEnabled);
            Assert.False(director.IsFreestyle);
            Assert.Equal(Domains.Jade, player.NetDomain.Value);
            Assert.Equal(Domains.Jade, director.CurrentDomain);
            Assert.True(player.InputStatus.Paused);

            // The living Cell: membrane read, cytoplasm shards, seeded flora + fauna.
            Assert.NotNull(director.Cell);
            Assert.True(director.Cell.MembraneRadius > 100f);
            Assert.True(director.FloraCount >= 1, $"expected seeded flora, got {director.FloraCount}");
            Assert.True(director.FaunaCount >= 1, $"expected seeded fauna, got {director.FaunaCount}");
            Assert.True(director.LifeFormPrismCount >= 1, "lifeforms should carry HealthPrism bodies");
            var cytoplasm = Object.FindFirstObjectByType<SnowChanger>();
            Assert.True(cytoplasm, "cytoplasm SnowChanger should be spawned");

            // No domain asymmetry: the whole seeded batch wears the cell's ONE controlling
            // color at seed time — never a mix, never Blue. (ControllingDomain is a LIVE
            // read that can flip as flora/trail mass lands after seeding, so assert batch
            // uniformity rather than equality against the current read.)
            Assert.NotEmpty(director.Cell.LiveFauna);
            var batchDomain = director.Cell.LiveFauna[0].Domain;
            Assert.NotEqual(Domains.Blue, batchDomain);
            foreach (var fauna in director.Cell.LiveFauna)
                Assert.Equal(batchDomain, fauna.Domain);

            // The toybox placed the four built-in toys on the membrane ring.
            var toyboxRoot = Children(director.Toybox.transform).Single(t => t.name == "FreestyleToybox");
            var names = Children(toyboxRoot).Select(t => t.name).ToArray();
            Assert.Equal(4, names.Length);
            Assert.Contains("Toy_painting", names);
            Assert.Contains("ToySet_vessel_changer", names);
            Assert.Contains("ToySet_domain_changer", names);
            Assert.Contains("Toy_conveyor", names);
            Assert.True(director.CountToys() >= 4);

            // The autopilot vessel lays REAL prisms — the lava-lamp trail.
            Assert.True(director.TrailPrismCount > 0, "autopilot should lay real trail prisms");
        }
        finally
        {
            Teardown(loop, director);
        }
    }

    // ── (b) the autopilot ↔ freestyle toggle swaps control ──────────────────

    [Fact]
    public void AutopilotToggle_SwapsControl_AndRaisesTheFreestyleEvents()
    {
        var (loop, director) = FreestyleFactory.Create(seed: 7);
        try
        {
            RunSeconds(loop, 2f);
            Assert.True(director.AutopilotEnabled);

            int enterCount = 0, exitCount = 0;
            director.FreestyleEvents.OnGameStateTransitionStart.OnRaised += () => enterCount++;
            director.FreestyleEvents.OnMenuStateTransitionStart.OnRaised += () => exitCount++;

            // Take the stick: AI off, freestyle events raised (ToyboxController arms on these).
            director.ToggleControl();
            Assert.True(director.IsFreestyle);
            Assert.False(director.AutopilotEnabled);
            Assert.Equal(1, enterCount);
            Assert.Equal(0, exitCount);

            // The rig's InputController stays paused — the window drives the ported
            // input strategy itself (single input driver, SkimRace precedent).
            loop.Tick(1f / 30f);
            Assert.True(director.LocalPlayer.InputStatus.Paused);

            // Hand it back: AI on, menu events raised.
            director.ToggleControl();
            Assert.False(director.IsFreestyle);
            Assert.True(director.AutopilotEnabled);
            Assert.Equal(1, enterCount);
            Assert.Equal(1, exitCount);
        }
        finally
        {
            Teardown(loop, director);
        }
    }

    // ── (c) domain-toy flythrough changes the pilot's domain ────────────────

    [Fact]
    public void DomainToyFlythrough_ChangesPilotDomain_AndThemeTintSource()
    {
        var (loop, director) = FreestyleFactory.Create(seed: 7);
        try
        {
            RunSeconds(loop, 4f); // spawn chain + toy placement + blooms (1.2s) complete

            var player = (Player)director.LocalPlayer;
            Assert.Equal(Domains.Jade, player.Domain);

            var toyboxRoot = Children(director.Toybox.transform).Single(t => t.name == "FreestyleToybox");
            var domainHost = Children(toyboxRoot).Single(t => t.name == "ToySet_domain_changer");

            // The flip-set shows the two domains you are NOT: Ruby + Gold, never Jade.
            var slots = Children(domainHost);
            Assert.Equal(2, slots.Count);
            var labels = slots.Select(LabelText).ToArray();
            Assert.Contains("Ruby", labels);
            Assert.Contains("Gold", labels);
            Assert.DoesNotContain("Jade", labels);
            var rubyRoot = slots.Single(r => LabelText(r) == "Ruby");

            // Autopilot lava-lamp: toys are visible but INERT — a drift-through does nothing.
            Park(director, rubyRoot.position);
            RunSeconds(loop, 0.5f);
            Assert.Equal(Domains.Jade, player.Domain);

            // Take control, leave, re-arm, fly back through the Ruby toy.
            Park(director, Vector3.zero);
            director.ToggleControl();
            RunSeconds(loop, 1f); // re-arm window
            Park(director, rubyRoot.position);
            RunSeconds(loop, 0.3f);

            // Server-authoritative write landed on NetDomain and mirrored into
            // Player.Domain — the LIVE mirror the theme tint reads.
            Assert.Equal(Domains.Ruby, player.NetDomain.Value);
            Assert.Equal(Domains.Ruby, player.Domain);
            Assert.Equal(Domains.Ruby, director.CurrentDomain);

            // The hull tint source follows: the theme resolves the NEW domain's colors.
            var theme = director.GameData.ThemeManagerData;
            Assert.NotEqual(theme.GetDomainUIColor(Domains.Jade), theme.GetDomainUIColor(director.CurrentDomain));
            Assert.NotNull(theme.TeamMaterialSets[director.CurrentDomain].ShipMaterial);

            // The used toy flipped in place to the domain you just left (set = universe \ current).
            RunSeconds(loop, 0.2f);
            Assert.Equal("Jade", LabelText(rubyRoot));
            var after = Children(domainHost).Select(LabelText).ToArray();
            Assert.Contains("Jade", after);
            Assert.Contains("Gold", after);
            Assert.DoesNotContain("Ruby", after);
        }
        finally
        {
            Teardown(loop, director);
        }
    }

    // ── (d) 2-sim-minute soak: populations move ONLY through active forces ──

    [Fact]
    public void FreestyleSoak_TwoSimMinutes_PopulationsMoveOnlyThroughActiveForces()
    {
        // Remove every active force: fauna never starve, cannot move, and cannot eat
        // (consume radius 0 — an immobile fauna could otherwise still graze a leaf that
        // seeded within its reach, which IS an active force and legitimately kills the
        // flora). With no consumption, no predation, no ability, and no imposed clock,
        // NOTHING may leave the world for the whole soak.
        var tuning = new FreestyleTuning
        {
            FaunaStarvationSeconds = 0f,
            FaunaMinSpeed = 0f,
            FaunaMaxSpeed = 0f,
            FaunaConsumeRadius = 0f,
        };
        var (loop, director) = FreestyleFactory.Create(seed: 11, tuning);
        try
        {
            RunSeconds(loop, 6f); // seed the ecology + let the autopilot lay some trail

            int faunaBefore = director.FaunaCount;
            int floraBefore = director.FloraCount;
            int trailBefore = director.TrailPrismCount;
            Assert.True(faunaBefore >= 1, "soak needs a seeded fauna population");
            Assert.True(floraBefore >= 1, "soak needs seeded flora");
            Assert.True(trailBefore > 0, "soak needs a laid trail");
            var seededFauna = new List<Fauna>(director.Cell.LiveFauna);
            var seededTrail = new List<GameObject>();
            foreach (var go in director.PrismFactory.Live)
                if (go) seededTrail.Add(go);

            // Freeze production too (the spawner may keep seeding/planting — allowed,
            // but the CONSERVATION assertions below are about removal, so stop the
            // vessel's spawn loop to make the trail side exact).
            director.VesselStatus.VesselPrismController.StopSpawn();
            loop.Tick(1f / 30f);
            int trailFrozen = director.TrailPrismCount;

            // Two simulated minutes of pure time.
            RunSeconds(loop, 120f, dt: 0.1f);

            // No imposed death: every seeded fauna is still alive, the count never fell.
            Assert.True(director.FaunaCount >= faunaBefore,
                $"fauna shrank with no active force: {faunaBefore} → {director.FaunaCount}");
            foreach (var fauna in seededFauna)
                Assert.True(fauna, "a seeded fauna vanished with no active force — imposed-death regression");

            // Flora only grow (their growth coroutines add prisms; nothing eats them).
            Assert.True(director.FloraCount >= floraBefore,
                $"flora shrank with no active force: {floraBefore} → {director.FloraCount}");

            // Conserved mass: every trail prism survives untouched — no aging, no decay,
            // no culler, anywhere. (The menu trail cap is a cheat; there is no cap.)
            Assert.Equal(trailFrozen, director.TrailPrismCount);
            foreach (var go in seededTrail)
            {
                Assert.True(go, "a trail prism GameObject vanished without an active force");
                var prism = go.GetComponent<Prism>();
                Assert.False(prism.destroyed, "a trail prism was destroyed without an active force");
            }
        }
        finally
        {
            Teardown(loop, director);
        }
    }
}
