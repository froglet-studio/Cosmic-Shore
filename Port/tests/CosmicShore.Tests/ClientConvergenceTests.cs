using System;
using CosmicShore.Client;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.Engine;
using CosmicShore.Engine.Networking;
using CosmicShore.Gameplay;
using CosmicShore.UI;

namespace CosmicShore.Tests;

// ─────────────────────────────────────────────────────────────────────────────
// CONVERGENCE RUNG 1 — the playable SkimRace client's simulation runs on the
// REAL ported systems. These tests drive SkimRaceFactory/SkimRaceDirector
// (Port/src/CosmicShore.Client/SkimRaceSim.cs) in-process, window-free:
//   • the rig is the genuine prefab family (VesselController/VesselStatus/
//     VesselTransformer/AIPilot/InputStatus + the CT1 contact rig),
//   • drift goes through VesselTransformer.BeginDrift/EndDrift (two-tier analog),
//   • boost goes through VesselStatus.IsBoosting (BoostActionSO shape),
//   • energy→top-speed goes through the real ThrottleScalerMultiplier hook,
//   • AI rivals claim crystals through the real trigger pass → OmniCrystalImpactor.
// No Silk type is touched — the windowing layer never loads here.
// ─────────────────────────────────────────────────────────────────────────────

public class ClientConvergenceTests : IDisposable
{
    public void Dispose()
    {
        typeof(PlayerDataService).GetProperty("Instance")!.SetValue(null, null);
        typeof(AudioSystem).GetProperty("Instance")!.SetValue(null, null);
        NetworkManager.Singleton = null;
    }

    /// <summary>Stops AI/spawn loops + flushes destroys so each test winds down cleanly.</summary>
    static void Teardown(GameLoop loop, SkimRaceDirector director)
    {
        foreach (var pilot in director.Pilots)
        {
            pilot.Status.VesselPrismController.StopSpawn();
            pilot.Player.ResetForPlay(); // AI path toggles the AIPilot OFF
        }
        loop.Tick(1f / 60f); // end-of-frame destroy flush
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

            director.RestartRace();
            Assert.Equal(RaceState.Countdown, director.State);
            Assert.Equal(0, director.TotalClaims);
            Assert.Equal(-1, director.WinnerPilot);
            foreach (var pilot in director.Pilots)
            {
                Assert.Equal(0, pilot.Stats.CrystalsCollected); // RoundStats.Cleanup ran
                Assert.Empty(pilot.Trail);                      // active force wiped the prismscape
                Assert.True(pilot.Status.IsStationary);         // grid hold until GO
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
}
