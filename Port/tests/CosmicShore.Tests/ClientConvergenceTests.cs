using System;
using CosmicShore.Client;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.Engine;
using CosmicShore.Engine.Networking;
using CosmicShore.Gameplay;
using CosmicShore.UI;
using CosmicShore.Utility;

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
//     SkimmerImpactor.AcceptImpactee → SkimRaceTrailSkimEnergyEffectSO.
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
        typeof(Singleton<PrismAOERegistry>).GetProperty("Instance")!.SetValue(null, null);
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
            // Passive regen over 2 frames is ~0.0013; one real prism enter grants ≥0.045.
            Assert.True(gained >= 0.04f,
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

    // ── rung 2: the whole race still plays on the real systems ──────────────

    [Fact]
    public void FullShortRace_ReachesFinished_WithGolfScoredWinner()
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
            Assert.InRange(director.WinnerPilot, 0, director.Pilots.Count - 1);

            var winner = director.Pilots[director.WinnerPilot];
            Assert.True(winner.Stats.CrystalsCollected >= director.WinTarget);
            Assert.True(winner.Stats.Score > 0f, "golf scoring: winner's Score is the race time");
            Assert.InRange(winner.Stats.Score, 0f, frames / 60f + 1f);

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
