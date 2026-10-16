// Proves the course extraction was a PURE MOVE: for every geometry-built gate race, the new
// RaceCourseSource returns exactly - bit for bit - what the controller's old BuildCourse
// returned, over seeds x intensities. The LEGACY bodies below are the pre-extraction
// GateRaceController subclasses' BuildCourse / LapsPerRace / AuthoredGateTarget, copied verbatim
// from the commit before the move (only `Intensity` became a parameter). Also exercises the two
// arena-built sources' failure path, which the preview hits whenever a card's cell carries no arena.
using System;
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using UnityEngine;

public static class Driver
{
    static int failures;

    static void Check(bool ok, string what)
    {
        if (ok) return;
        failures++;
        if (failures <= 20) Console.Error.WriteLine("FAIL " + what);
    }

    // ── Legacy (verbatim, pre-move) ──────────────────────────────────

    static List<RaceGate> LegacySwitchback(int seed, int gateCount, float inner, float outer, int Intensity, float firstGateDistance)
    {
        var settings = SwitchbackCourseSettings.ForIntensity(Intensity);
        settings.InnerRadius = inner;
        settings.OuterRadius = outer;
        settings.FirstGateDirection = Vector3.up;
        settings.FirstGateDistance = Mathf.Clamp(firstGateDistance, inner, outer);
        int ask = Mathf.Max(2, gateCount);
        while (ask >= 2)
        {
            settings.GateCount = ask;
            var course = SwitchbackCourse.Generate(seed, settings);
            if (course != null && course.Count >= ask) return course;
            if (ask == 2) return null;
            ask = Mathf.Max(2, ask / 2);
        }
        return null;
    }

    static List<RaceGate> LegacyCircuit(HeadlongCircuitSettings settings, int seed, int gateCount, float inner, float outer, int LapsPerRace)
    {
        settings.InnerRadius = inner;
        settings.OuterRadius = outer;
        settings.FirstGateDirection = Vector3.up;
        int perLap = Mathf.Max(3, Mathf.CeilToInt(gateCount / (float)LapsPerRace));
        settings.GateCount = perLap;
        return HeadlongCircuit.Generate(seed, settings);
    }

    static int LegacyBreakwaterStations(int crossings, int LeadInGates, int LapsPerRace) =>
        LeadInGates + Mathf.Max(1, (crossings - LeadInGates)) / Mathf.Max(1, LapsPerRace);

    static int DeriveNextSeed(int seed) => unchecked(seed * 1664525 + 1013904223);

    static List<RaceGate> LegacyBreakwater(int seed, int gateCount, float inner, float outer, int Intensity, int LapsPerRace)
    {
        int LeadInGates = 1;
        int stations = Mathf.Max(3, LegacyBreakwaterStations(gateCount, LeadInGates, LapsPerRace));
        var settings = BreakwaterCourseSettings.ForIntensity(Intensity);
        settings.StationCount = stations;
        settings.InnerRadius = inner;
        settings.OuterRadius = outer;
        int usedSeed = seed;
        List<BreakwaterStation> course = null;
        for (int attempt = 0; ; attempt++)
        {
            course = BreakwaterCourse.Generate(usedSeed, settings);
            if (course != null && course.Count >= settings.StationCount) break;
            course = null;
            if (attempt >= BreakwaterCourse.ReseedAttempts) break;
            usedSeed = DeriveNextSeed(usedSeed);
        }
        if (course == null)
        {
            int ask = settings.StationCount;
            while (ask > 3)
            {
                ask = Mathf.Max(3, ask / 2);
                settings.StationCount = ask;
                course = BreakwaterCourse.Generate(usedSeed, settings);
                if (course != null && course.Count >= ask) break;
                course = null;
            }
        }
        if (course == null) return null;
        var gates = new List<RaceGate>(course.Count);
        for (int i = 0; i < course.Count; i++)
            gates.Add(new RaceGate(course[i].Position, course[i].Axis, course[i].PortRadius));
        return gates;
    }

    static int LegacyWaystationTarget(int Intensity)
    {
        int asked = EndConditionOverridesSO.DefaultWaystationRingTarget;
        int per = WaystationCourseSettings.ForIntensity(Intensity).RingsPerCluster;
        return WaystationCourse.ClusterCount(asked, per) * Mathf.Max(1, per);
    }

    static List<RaceGate> LegacyWaystation(int seed, int gateCount, float inner, float outer, int Intensity)
    {
        var settings = WaystationCourseSettings.ForIntensity(Intensity);
        settings.InnerRadius = inner;
        settings.OuterRadius = outer;
        settings.RingTarget = Mathf.Max(settings.RingsPerCluster, gateCount);
        settings.FirstClusterDirection = Vector3.up;
        var course = WaystationCourse.Generate(seed, settings);
        return course != null && course.Count > 0 ? course : null;
    }

    static void LegacyShell(float nucleus, float factor, float fallback, float outerRadius, out float inner, out float outer)
    {
        inner = nucleus > 0f ? nucleus * factor : fallback;
        outer = Mathf.Max(inner + 120f, outerRadius);
    }

    // ── Comparison ───────────────────────────────────────────────────

    static bool Same(List<RaceGate> a, List<RaceGate> b)
    {
        if (a == null || b == null) return a == null && b == null;
        if (a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++)
        {
            var x = a[i]; var y = b[i];
            if (x.Position.x != y.Position.x || x.Position.y != y.Position.y || x.Position.z != y.Position.z) return false;
            if (x.Axis.x != y.Axis.x || x.Axis.y != y.Axis.y || x.Axis.z != y.Axis.z) return false;
            if (x.Radius != y.Radius) return false;
        }
        return true;
    }

    public static int Main()
    {
        int compared = 0, gates = 0;
        float[] nuclei = { 0f, 392f };   // no cell / the shipped nucleus
        for (int intensity = 1; intensity <= 4; intensity++)
        foreach (float nucleus in nuclei)
        for (int k = 0; k < 40; k++)
        {
            int seed = k == 0 ? 0 : unchecked(k * 2654435761u > int.MaxValue ? (int)(k * 97531) : k * 7919 - 3);
            if (k % 2 == 1) seed = -seed;

            // Switchback
            {
                var src = (SwitchbackCourseSource)RaceCourseSource.For(GameModes.Switchback);
                src.ResolveShell(nucleus, out float i1, out float o1);
                LegacyShell(nucleus, 1.22f, 480f, 1080f, out float i0, out float o0);
                Check(i0 == i1 && o0 == o1, $"switchback shell n={nucleus}");
                int target = src.AuthoredGateTarget(intensity);
                Check(target == EndConditionOverridesSO.DefaultSwitchbackGateTarget, "switchback target");
                var got = src.Build(new RaceCourseRequest(seed, intensity, target, i1, o1, null), out _);
                var want = LegacySwitchback(seed, target, i0, o0, intensity, 620f);
                Check(Same(got, want), $"switchback seed {seed} I{intensity} n={nucleus}");
                Check(src.LapsPerRace(intensity) == 1 && src.LeadInGates == 0, "switchback shape");
                compared++; gates += got?.Count ?? 0;
            }
            // Headlong / Redline
            foreach (var mode in new[] { GameModes.Headlong, GameModes.Redline })
            {
                var src = RaceCourseSource.For(mode);
                src.ResolveShell(nucleus, out float i1, out float o1);
                int target = src.AuthoredGateTarget(intensity);
                int laps = src.LapsPerRace(intensity);
                Check(laps == 3 && src.LeadInGates == 0, $"{mode} shape");
                var settings = mode == GameModes.Headlong
                    ? HeadlongCircuitSettings.ForIntensity(intensity)
                    : RedlineCourse.ForIntensity(intensity);
                var got = src.Build(new RaceCourseRequest(seed, intensity, target, i1, o1, null), out _);
                var want = LegacyCircuit(settings, seed, target, i1, o1, laps);
                Check(Same(got, want), $"{mode} seed {seed} I{intensity} n={nucleus}");
                compared++; gates += got?.Count ?? 0;
            }
            // Breakwater
            {
                var src = RaceCourseSource.For(GameModes.Breakwater);
                src.ResolveShell(nucleus, out float i1, out float o1);
                LegacyShell(nucleus, 1.22f, 420f, 1080f, out float i0, out float o0);
                Check(i0 == i1 && o0 == o1, $"breakwater shell n={nucleus}");
                int laps = src.LapsPerRace(intensity);
                Check(laps == BreakwaterCourseSettings.DefaultLaps && src.LeadInGates == 1, "breakwater shape");
                int target = src.AuthoredGateTarget(intensity);
                var got = src.Build(new RaceCourseRequest(seed, intensity, target, i1, o1, null), out _);
                var want = LegacyBreakwater(seed, target, i0, o0, intensity, laps);
                Check(Same(got, want), $"breakwater seed {seed} I{intensity} n={nucleus}");
                Check(got != null && src.RaceLengthFor(got.Count, intensity) == target,
                      $"breakwater race length {src.RaceLengthFor(got?.Count ?? 0, intensity)} != target {target}");
                compared++; gates += got?.Count ?? 0;
            }
            // Waystation
            {
                var src = RaceCourseSource.For(GameModes.Waystation);
                src.ResolveShell(nucleus, out float i1, out float o1);
                int target = src.AuthoredGateTarget(intensity);
                Check(target == LegacyWaystationTarget(intensity), "waystation target");
                var got = src.Build(new RaceCourseRequest(seed, intensity, target, i1, o1, null), out _);
                var want = LegacyWaystation(seed, target, i1, o1, intensity);
                Check(Same(got, want), $"waystation seed {seed} I{intensity} n={nucleus}");
                compared++; gates += got?.Count ?? 0;
            }
        }

        // Arena-built sources: no config -> a sentence, never a throw, never a course.
        foreach (var mode in new[] { GameModes.Skein, GameModes.Regatta })
        {
            var src = RaceCourseSource.For(mode);
            var got = src.Build(new RaceCourseRequest(1, 1, src.AuthoredGateTarget(1), 0, 0, null), out string why);
            Check(got == null && !string.IsNullOrEmpty(why), $"{mode} null config must report, not build");
        }

        // Every mode the registry claims answers, and nothing else does.
        foreach (var mode in RaceCourseSource.GateRaceModes)
            Check(RaceCourseSource.For(mode) is { } s && s.Mode == mode, $"registry {mode}");
        Check(RaceCourseSource.For(GameModes.Rampage) == null, "registry answers a non-race");

        Console.WriteLine($"{compared} course pairs compared, {gates} gates, {failures} failures.");
        return failures == 0 ? 0 : 1;
    }
}
