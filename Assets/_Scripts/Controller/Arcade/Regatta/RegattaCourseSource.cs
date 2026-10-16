using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Regatta's course: the rings the rails are braided through, read off the ARENA authored on
    /// the cell config. The request's seed and shell are IGNORED by design, as for Skein.
    /// </summary>
    public sealed class RegattaCourseSource : RaceCourseSource
    {
        public override GameModes Mode => GameModes.Regatta;

        /// <summary>The scene's start-line standoff (Regatta lines up further back than the
        /// platform default, 260 against 220).</summary>
        public const float SceneStartLineStandoff = 260f;

        public RegattaCourseSource() => StartLineStandoff = SceneStartLineStandoff;

        /// <summary>Laps = authored gate threadings / the arena's rings per lap. ONE authority.</summary>
        public override int LapsPerRace(int intensity) =>
            Mathf.Max(1, AuthoredGateTarget(intensity) / RegattaCourse.RingsPerLap);

        public override int AuthoredGateTarget(int intensity)
        {
            var overrides = EndConditionOverridesSO.Instance;
            return overrides != null
                ? overrides.GetRegattaGateTarget()
                : EndConditionOverridesSO.DefaultRegattaGateTarget;
        }

        public override List<RaceGate> Build(in RaceCourseRequest request, out string failure)
        {
            if (!TryResolveArena(request.Config, out var arena, out failure)) return null;

            if (request.GateCount % RegattaCourse.RingsPerLap != 0)
                CSDebug.LogWarning($"[Regatta] Authored target {request.GateCount} is not a whole number of " +
                                   $"{RegattaCourse.RingsPerLap}-ring laps; racing " +
                                   $"{LapsPerRace(request.Intensity)} lap(s). Author a multiple of " +
                                   $"{RegattaCourse.RingsPerLap}.");

            return arena.BuildGatesNow();
        }

        /// <summary>
        /// The rails as LAID this match: the object whose lanes hold the live rail prisms. The Cell builds its
        /// environment by calling <c>Spawn()</c> on the config's <c>EnvironmentPrefab</c> ASSET, which lays the prisms
        /// into a plain container and fills that asset's own lane list - no <see cref="SpawnableRegattaRails"/> is ever
        /// instantiated in the scene. So <c>FindAnyObjectByType&lt;SpawnableRegattaRails&gt;()</c> finds nothing, and
        /// every reader that used it (the Skim Race pilot's Regatta course, the controller's own-lane aim, the human
        /// flight recorder) silently had no rail: the Squirrel AI flew ring to ring straight through the braid
        /// (Yash, 2026-10-16: a recorded flight said "no Regatta rails in the scene"). This asks the cells' configs,
        /// and still takes a scene instance first where one is authored.
        /// </summary>
        public static bool TryResolveLiveRails(out SpawnableRegattaRails rails, out string why)
        {
            rails = Object.FindAnyObjectByType<SpawnableRegattaRails>();
            if (rails && HasLaidLanes(rails)) { why = null; return true; }

            var cells = Object.FindObjectsByType<Cell>(FindObjectsSortMode.None);
            for (int i = 0; i < cells.Length; i++)
            {
                if (!cells[i]) continue;
                if (cells[i].Config && cells[i].Config.EnvironmentPrefab is SpawnableRegattaRails live && HasLaidLanes(live))
                { rails = live; why = null; return true; }
                if (cells[i].ExpectedConfig && cells[i].ExpectedConfig.EnvironmentPrefab is SpawnableRegattaRails expected && HasLaidLanes(expected))
                { rails = expected; why = null; return true; }
            }
            rails = null;
            why = cells.Length == 0 ? "no Cell in the scene" : "no cell config's Regatta rails have laid lanes yet";
            return false;
        }

        static bool HasLaidLanes(SpawnableRegattaRails rails)
        {
            var trails = rails.GetTrails();
            if (trails == null) return false;
            for (int i = 0; i < trails.Count; i++)
                if (trails[i]?.TrailList is { Count: > 0 }) return true;
            return false;
        }

        /// <summary>The arena prefab, off the cell's EXPECTED config.</summary>
        public static bool TryResolveArena(CellConfigDataSO config, out SpawnableRegattaRails arena,
                                           out string why)
        {
            arena = null;
            if (config == null)
            {
                why = "No Cell whose config is knowable - the circuit's seed lives on the cell " +
                      "config's arena prefab. A Regatta cell must be IntensityWise.";
                return false;
            }

            arena = config.EnvironmentPrefab as SpawnableRegattaRails;
            if (arena == null)
            {
                why = $"The cell config '{config.name}' authors no SpawnableRegattaRails " +
                      "EnvironmentPrefab, so there are no rails to hang rings on.";
                return false;
            }

            why = null;
            return true;
        }
    }
}
