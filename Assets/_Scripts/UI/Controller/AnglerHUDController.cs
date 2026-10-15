using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// Binds the Angler's live state to <see cref="AnglerHUDView"/> (design: <c>R_VesselActions/ANGLER.md</c> §4).
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
        [Tooltip("This vessel's command executor: the lure, the school, the paint and the squad.")]
        [SerializeField] AnglerCommandExecutor angler;
        [Tooltip("How fast the Lure card eases on and off (1/s).")]
        [SerializeField, Min(0f)] float lureEaseRate = 6f;

        bool _isLocalPilotHud;
        float _lure01;

        public override void Initialize(IVesselStatus vesselStatus)
        {
            base.Initialize(vesselStatus);

            if (!view) view = GetComponentInChildren<AnglerHUDView>(true);

            // The fleet's pilot gate (ThresherHUDController / StoatHUDController / BrittleStarHUDController).
            _isLocalPilotHud = vesselStatus?.Player != null
                               && !vesselStatus.IsInitializedAsAI
                               && vesselStatus.IsLocalUser;
            _lure01 = 0f;
        }

        void Update()
        {
            if (!_isLocalPilotHud || !view || !angler) return;
            view.SetCommand(angler.TamedCount > 0 ? (float)angler.Hunting / Mathf.Max(1, angler.TamedCount) : 0f,
                            angler.Painting && angler.PaintedCount > 0);
            view.SetLeash(angler.SquadCap > 0 ? (float)angler.TamedCount / angler.SquadCap : 0f);
            _lure01 = Mathf.MoveTowards(_lure01, angler.Luring ? 1f : 0f, lureEaseRate * Time.unscaledDeltaTime);
            view.SetLure(_lure01, angler.Luring);
            view.SetTame(angler.StrongestTame01);
        }
    }
}
