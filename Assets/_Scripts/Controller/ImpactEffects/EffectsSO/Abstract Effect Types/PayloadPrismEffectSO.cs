namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A prism impact effect run by a released PAYLOAD (Docs/Pilots/ARTIFACTS.md §2) on every
    /// prism its burst reaches. The payload's counterpart of <see cref="ProjectilePrismEffectSO"/>:
    /// the same shape (one effect asset, one prism at a time), so what a payload does to mass is
    /// authored as assets and an artifact changes the payload by naming different ones.
    /// </summary>
    public abstract class PayloadPrismEffectSO : ImpactEffectSO
    {
        public abstract void Execute(in PayloadRelease release, Prism prism);
    }
}
