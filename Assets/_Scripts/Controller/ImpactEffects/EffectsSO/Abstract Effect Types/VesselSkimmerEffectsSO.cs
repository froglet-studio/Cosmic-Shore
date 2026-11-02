
namespace CosmicShore.Gameplay
{
    public abstract class VesselSkimmerEffectsSO : ImpactEffectSO
    {
        public abstract void Execute(VesselImpactor impactor, SkimmerImpactor impactee);

        /// <summary>
        /// True for an effect that runs again for as long as a hull stays inside the skimmer, not only
        /// when it enters. A skimmer dispatches its vessel effects on ENTRY; one that also wants the
        /// standing contact calls <see cref="SkimmerImpactor.ReapplyVesselEffectsToOverlapping"/> every
        /// frame, which re-runs only the effects that answer true here (the Tiger Moth's dust reporter,
        /// whose per-victim window then paces the shred). Must pace itself: it is called every frame.
        /// </summary>
        public virtual bool RepeatsWhileInside => false;
    }
}
