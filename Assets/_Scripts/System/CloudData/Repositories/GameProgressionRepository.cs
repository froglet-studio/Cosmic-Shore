using System.Collections.Generic;
using CosmicShore.Core;

namespace CosmicShore.Core
{
    /// <summary>
    /// Repository for game mode quest chain progression.
    /// Cloud key: "GAME_MODE_PROGRESSION"
    /// </summary>
    public sealed class GameProgressionRepository : CloudDataRepository<GameModeProgressionData>
    {
        public override string CloudKey => UGSKeys.GameModeProgression;

        public GameProgressionRepository(ICloudSaveProvider provider) : base(provider) { }

        protected override void OnAfterLoad(GameModeProgressionData data)
        {
            data.UnlockedModes ??= new List<string>();
            data.CompletedQuests ??= new List<string>();
            data.UnlockedFeatures ??= new List<string>();
            data.BestStats ??= new Dictionary<string, float>();
            data.MaxUnlockedIntensity ??= new Dictionary<string, int>();
            data.IntensityPlayCounts ??= new Dictionary<string, int>();
        }
    }
}
