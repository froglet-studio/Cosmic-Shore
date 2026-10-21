using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// Binds the Angler's live state to <see cref="AnglerHUDView"/>, one card per element of the approved map (design:
    /// <c>R_VesselActions/ANGLER.md</c> §7).
    ///
    /// <para>Every signal is POLLED per frame from this vessel's OWN executor — a serialized reference on its prefab, never
    /// reached for by type through the hierarchy (vessel rule 14), so an unwired HUD shows in the inspector.</para>
    ///
    /// <para>The pilot gate runs every <see cref="Initialize"/>, above anything that depends on it, so a re-init that hands
    /// this vessel to an AI or a remote owner stops driving the HUD (rule 5).</para>
    /// </summary>
    public class AnglerHUDController : VesselHUDController
    {
        [Header("Angler")]
        [SerializeField] AnglerHUDView view;
        [Tooltip("This vessel's command executor: the paint, the eraser and its lock, the lure and the dash.")]
        [SerializeField] AnglerCommandExecutor angler;
        [Tooltip("How fast the Lure card eases on and off (1/s).")]
        [SerializeField, Min(0f)] float lureEaseRate = 6f;
        [Tooltip("The Squad card's label while one kind is picked (X). Tokens: {squad} the kind's name, {count} how many of it " +
                 "the school holds. Empty shows nothing. The whole school (no kind picked) shows no label.")]
        [SerializeField] string squadLabelTemplate = "{squad} x{count}";

        bool _isLocalPilotHud;
        float _lure01;
        int _seeds;
        int _squadCount = -2;   // the squad label last written (-1 = the whole school)
        string _squadName;

        public override void Initialize(IVesselStatus vesselStatus)
        {
            base.Initialize(vesselStatus);

            if (!view) view = GetComponentInChildren<AnglerHUDView>(true);

            // The fleet's pilot gate (ThresherHUDController / StoatHUDController / BrittleStarHUDController).
            _isLocalPilotHud = vesselStatus?.Player != null
                               && !vesselStatus.IsInitializedAsAI
                               && vesselStatus.IsLocalUser;
            _lure01 = 0f;
            _seeds = 0;
            _squadCount = -2;   // forces the first write
            _squadName = null;
        }

        void Update()
        {
            if (!_isLocalPilotHud || !view || !angler) return;
            float school = Mathf.Max(1, angler.TamedCount);
            view.SetCharge(Mathf.Clamp01(angler.HunterCount / 3f), angler.Erasing);   // spent hunters out (3+ = full)
            _lure01 = Mathf.MoveTowards(_lure01, angler.Luring ? 1f : 0f, lureEaseRate * Time.unscaledDeltaTime);
            view.SetMass(Mathf.Max(angler.StrongestTame01, _lure01 * 0.15f), angler.Luring);
            view.SetFaunaCount(angler.TamedCount);
            int squad = angler.SquadPicked ? angler.SquadCount : -1;
            if (squad != _squadCount || (squad >= 0 && angler.SquadName != _squadName))   // the label is rebuilt only on a change
            {
                _squadCount = squad;
                _squadName = squad >= 0 ? angler.SquadName : null;
                view.SetSquad(squad >= 0 ? TemplateText.Fill(squadLabelTemplate, "squad", _squadName, "count", squad.ToString()) : "");
            }
            view.SetSpace(angler.Hunting / school, angler.Painting && angler.PaintedCount > 0);
            view.SetFloraSeeds(angler.FloraSeedCount);
            if (angler.FloraSeedCount > _seeds) view.PlayOmniCrystalCollected();   // a crystal just left a seed
            _seeds = angler.FloraSeedCount;
            view.SetTime(angler.DashReady ? 1f : 0f, angler.DashReady);
        }
    }
}
