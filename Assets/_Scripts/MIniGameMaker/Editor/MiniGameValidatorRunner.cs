using System.Collections.Generic;

namespace CosmicShore.Tools.MiniGameMaker
{
    public static class MiniGameValidatorRunner
    {
        static readonly List<IValidator> _validators = new()
        {
            new DependencySpawnerValidator(),
            new GameRootValidator(),
            new SpawnPointsValidator(),

        };

        public static IEnumerable<IValidator> GetAll() => _validators;
    }
}