using CosmicShore.Utility;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A creature whose prefab carries a SPECIES DATA asset (a LightFauna's LightFaunaDataSO, a
    /// worm colony's WormColonyConfigSO, a substrate population's SubstrateSpeciesSO, a builder
    /// colony's BuilderColonyConfigSO) and that will swap it for the one its
    /// <see cref="FaunaConfigurationSO.SpeciesDataOverride"/> names.
    ///
    /// Why a hook and not <c>Fauna.AssignLineage</c>: the spawner runs <c>Initialize</c> BEFORE
    /// <c>AssignLineage</c> (CellLifeSpawnerBase.SpawnFaunaWithDomain), and a colony builds its
    /// chain, a substrate claims its block and a builder seeds its lattice inside Initialize -
    /// so the data has to be bound before that. <c>SpawnFaunaWithDomain</c> is the one Instantiate
    /// every producer reaches, which is where this is called from. A class that also reproduces
    /// through <c>Fauna.SpawnOffspring</c> (LightFauna) re-reads the override from
    /// <c>SourceConfig</c> at AssignLineage, so offspring inherit it.
    ///
    /// Config-only per-world tuning is what the Ark escort's five worlds are built on
    /// (Docs/Voyages/ARK_ESCORT.md): the asset a world names is a COPY under its own
    /// Cell Configs/ folder, never the shared one under _SO_Assets/Lifeforms or Light Fauna Data.
    /// </summary>
    public interface ISpeciesDataOverride
    {
        /// <summary>
        /// Bind the config's <see cref="FaunaConfigurationSO.SpeciesDataOverride"/> when it is of
        /// the type this creature reads; anything else (null included) keeps the prefab's asset.
        /// Called once per spawn, before <c>Fauna.Initialize</c>.
        /// </summary>
        void BindSpeciesData(FaunaConfigurationSO config);
    }
}
