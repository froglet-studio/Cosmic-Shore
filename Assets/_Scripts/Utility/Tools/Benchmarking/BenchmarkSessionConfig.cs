#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Utility.Tools.Benchmarking
{
    /// <summary>
    /// What scene/flow to benchmark in an automated session.
    /// </summary>
    public enum BenchmarkTarget
    {
        /// <summary>Benchmark a gameplay scene launched via the Arcade flow.</summary>
        Game = 0,
        /// <summary>Benchmark the Main Menu scene directly (no game launch).</summary>
        MainMenu = 1,
    }

    /// <summary>
    /// Persists automated benchmark session state across play mode transitions
    /// and domain reloads via EditorPrefs JSON.
    /// </summary>
    [Serializable]
    public class BenchmarkSessionConfig
    {
        const string EditorPrefsKey = "CosmicShore_BenchmarkSession";

        // ── User Configuration ───────────────────────────────────────────────
        public string Label = "Session";
        public int Iterations = 3;
        public float DurationSeconds = 20f;

        // ── Benchmark Target ─────────────────────────────────────────────────
        public BenchmarkTarget Target = BenchmarkTarget.Game;

        // ── Main Menu Configuration ──────────────────────────────────────────
        /// <summary>Seconds to wait for Menu_Main to fully initialize before sampling.</summary>
        public float MenuInitDelaySeconds = 5f;

        // ── Game Launch Configuration ────────────────────────────────────────
        public GameModes GameMode = GameModes.Freestyle;
        public VesselClassType Vessel = VesselClassType.Squirrel;
        public int Intensity = 1;

        // ── Deterministic ────────────────────────────────────────────────────
        public bool Deterministic = true;
        public int Seed = 42;
        public int WarmupFrames = 120;
        public float FixedDt = 0.02f;

        // ── Runtime State ────────────────────────────────────────────────────
        public bool IsRunning;
        public int CurrentIteration;
        public List<string> CompletedReportPaths = new();

        /// <summary>
        /// Tracks whether we've already launched the game via Arcade this iteration.
        /// Reset to false at start of each iteration; set true once Arcade.LaunchArcadeGame runs.
        /// </summary>
        public bool GameLaunched;

        // ── Persistence ──────────────────────────────────────────────────────

        public void Save()
        {
            EditorPrefs.SetString(EditorPrefsKey, JsonUtility.ToJson(this));
        }

        public static BenchmarkSessionConfig Load()
        {
            string json = EditorPrefs.GetString(EditorPrefsKey, "");
            if (string.IsNullOrEmpty(json))
                return new BenchmarkSessionConfig();

            try
            {
                return JsonUtility.FromJson<BenchmarkSessionConfig>(json);
            }
            catch
            {
                return new BenchmarkSessionConfig();
            }
        }

        public static void Clear()
        {
            EditorPrefs.DeleteKey(EditorPrefsKey);
        }
    }
}

#endif
