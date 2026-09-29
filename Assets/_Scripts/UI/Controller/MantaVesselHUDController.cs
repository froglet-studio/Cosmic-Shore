using CosmicShore.Data;
using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// Drives the Manta's bomb-bay HUD off <see cref="MantaStingActionExecutor"/>'s instance
    /// events (the overcharge kit this controller used to speak for is deleted). One
    /// symmetric Rebind/Unbind pair, detach-first — a vessel swap re-runs Initialize on live
    /// components — gated for the local human pilot only AFTER the detach, so a re-init that
    /// hands this vessel to an AI or a remote owner can never strand the old handlers.
    ///
    /// The fuse countdown is the one per-frame poll (a burning number has no event to ride);
    /// it early-outs while nothing is planted.
    /// </summary>
    public class MantaVesselHUDController : VesselHUDController
    {
        [Header("View")]
        [SerializeField] private MantaVesselHUDView view;

        [Header("Bomb bay binding")]
        [Tooltip("The Sting executor on this vessel's ShipActions child. Empty resolves at " +
                 "Initialize.")]
        [SerializeField] private MantaStingActionExecutor stingExecutor;

        bool _bound;

        public override void Initialize(IVesselStatus vesselStatus)
        {
            base.Initialize(vesselStatus);

            if (!view)
                view = View as MantaVesselHUDView;
            if (!stingExecutor)
                stingExecutor = GetComponentInChildren<MantaStingActionExecutor>(true);

            Unbind();
            if (vesselStatus.IsInitializedAsAI || !vesselStatus.IsLocalUser) return;
            Rebind();
        }

        void Rebind()
        {
            if (_bound || !stingExecutor) return;
            stingExecutor.OnBayChanged += HandleBayChanged;
            stingExecutor.OnPlantedChanged += HandlePlantedChanged;
            stingExecutor.OnSkimCharged += HandleSkimCharged;
            stingExecutor.OnBombArmed += HandleBombArmed;
            stingExecutor.OnBombPlanted += HandleBombPlanted;
            stingExecutor.OnKabloom += HandleKabloom;
            _bound = true;
            HandleBayChanged();
            HandlePlantedChanged();
        }

        void Unbind()
        {
            if (!_bound || !stingExecutor) { _bound = false; return; }
            stingExecutor.OnBayChanged -= HandleBayChanged;
            stingExecutor.OnPlantedChanged -= HandlePlantedChanged;
            stingExecutor.OnSkimCharged -= HandleSkimCharged;
            stingExecutor.OnBombArmed -= HandleBombArmed;
            stingExecutor.OnBombPlanted -= HandleBombPlanted;
            stingExecutor.OnKabloom -= HandleKabloom;
            _bound = false;
        }

        void OnDisable() => Unbind();

        void Update()
        {
            // The burning-fuse number. Event-driven everywhere else; a countdown has to tick.
            if (!_bound || !view || !stingExecutor) return;
            int planted = stingExecutor.PlantedBombs.Count;
            if (planted > 0)
                view.SetPlantedBoard(planted, stingExecutor.ShortestFuseRemaining);
        }

        // ── Card juice. Each beat flashes the card of the ELEMENT that owns it, so the row
        //    doubles as the feedback surface: Charge for everything the bomb bay does, Space
        //    for the cash-out. PlayPressFlash is the fleet's existing press animation — the
        //    same language every other vessel's abilities already speak.
        void HandleSkimCharged() => Flash(Element.Charge);
        void HandleBombArmed() => Flash(Element.Charge);
        void HandleBombPlanted() => Flash(Element.Charge);
        void HandleKabloom(int cashed) => Flash(Element.Space);

        void Flash(Element element)
        {
            if (View) View.PlayAbilityFlash(element);
        }

        void HandleBayChanged()
        {
            if (view && stingExecutor)
                view.SetBombBay(stingExecutor.ArmedBombs, stingExecutor.Capacity,
                                stingExecutor.Charge);
        }

        void HandlePlantedChanged()
        {
            if (view && stingExecutor)
                view.SetPlantedBoard(stingExecutor.PlantedBombs.Count,
                                     stingExecutor.ShortestFuseRemaining);
        }
    }
}
