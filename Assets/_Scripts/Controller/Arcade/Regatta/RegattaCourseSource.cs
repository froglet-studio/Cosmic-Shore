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
