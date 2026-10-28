using CosmicShore.ScriptableObjects;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Voyage toy in the world (<see cref="VoyageToyDefinitionSO"/>): fly it to set out (the squad deploys and the
    /// job board stands), fly it again to give the voyage up. The run itself is <see cref="VoyageRun"/>, a sibling under
    /// the toybox root so the toy's bloom never scales the Ark or the board.
    /// </summary>
    public sealed class VoyageToy : Toy
    {
        VoyageToyDefinitionSO _def;
        VoyageRun _run;

        public void Configure(VoyageToyDefinitionSO definition) => _def = definition;

        protected override void OnActivated(IVesselStatus localVessel)
        {
            EnsureRun();
            if (_run.IsActive) _run.GiveUp("the pilot flew the voyage toy");
            else _run.Embark(localVessel);
            Rebloom();
        }

        void EnsureRun()
        {
            if (_run) return;
            var go = new UnityEngine.GameObject("VoyageRun");
            go.transform.SetParent(transform.parent, false);
            _run = go.AddComponent<VoyageRun>();
            _run.Configure(_def, Context, transform.parent, Rebloom);
        }
    }
}
