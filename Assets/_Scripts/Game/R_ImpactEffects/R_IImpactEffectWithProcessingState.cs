namespace CosmicShore.Game
{
    /// <summary>
    /// Optional extension for effects that need to influence the pipeline
    /// (e.g., consume default crystal impact). They get a pre‑pass hook.
    /// </summary>
    public interface R_IImpactEffectWithProcessingState : R_IImpactEffect
    {
        void ExecutePrePass(R_IImpactor impactor, R_ImpactorBase impactee, ImpactProcessingState processingState);
    }
}