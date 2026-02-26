using CosmicShore.Models.Enums;

namespace CosmicShore.Game.Environment
{
    /// <summary>
    /// A single trail's worth of spawn points plus metadata.
    /// Represents an ordered sequence of positions that form a path
    /// (e.g., a helix, ring, or line of blocks).
    ///
    /// Multiple SpawnTrailData instances can represent multi-trail structures
    /// (e.g., an ellipsoid with three orthogonal rings).
    /// </summary>
    [System.Serializable]
    public class SpawnTrailData
    {
        public SpawnPoint[] Points;
        public bool IsLoop;
        public Domains Domain;

        public SpawnTrailData(SpawnPoint[] points, bool isLoop = false, Domains domain = Domains.Blue)
        {
            Points = points;
            IsLoop = isLoop;
            Domain = domain;
        }
    }
}
