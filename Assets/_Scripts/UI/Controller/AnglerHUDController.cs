using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using Reflex.Attributes;
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

        [Inject] GameDataSO _gameData;

        IVesselStatus _status;
        bool _isLocalPilotHud;
        float _lure01;
        int _seeds, _tamed;

        public override void Initialize(IVesselStatus vesselStatus)
        {
            base.Initialize(vesselStatus);
            _status = vesselStatus;

            if (!view) view = GetComponentInChildren<AnglerHUDView>(true);

            // The fleet's pilot gate (ThresherHUDController / StoatHUDController / BrittleStarHUDController).
            _isLocalPilotHud = vesselStatus?.Player != null
                               && !vesselStatus.IsInitializedAsAI
                               && vesselStatus.IsLocalUser;
            _lure01 = 0f;
            _seeds = 0;
            _tamed = 0;
        }

        void Update()
        {
            if (!_isLocalPilotHud || !view || !angler) return;
            float school = Mathf.Max(1, angler.TamedCount);
            view.SetCharge(Mathf.Clamp01(angler.HunterCount / 3f), angler.Erasing);   // spent hunters out (3+ = full)
            view.SetLockOnColor(ResolveDomainSignalColor());
            view.SetLockOn(angler.HunterCount > 0);
            _lure01 = Mathf.MoveTowards(_lure01, angler.Luring ? 1f : 0f, lureEaseRate * Time.unscaledDeltaTime);
            view.SetMass(Mathf.Max(angler.StrongestTame01, _lure01 * 0.15f), angler.Luring);
            view.SetFaunaCount(angler.TamedCount);
            view.SetLureColor(ResolveDomainSignalColor());
            if (angler.TamedCount > _tamed && angler.Luring) view.PlayLureJoined();   // one tamed in the light
            _tamed = angler.TamedCount;
            view.SetLure(angler.Luring);
            view.SetSpace(angler.Hunting / school, angler.Painting && angler.PaintedCount > 0);
            view.SetPaintColors(ResolveDomainSignalColor(), ResolveShieldedColor());
            view.SetPaintGroups(angler.GroupCount(0), angler.GroupCount(1), angler.SendGroup);
            view.SetPaintPrism(angler.PaintedCount > 0);
            view.SetFloraSeeds(angler.FloraSeedCount);
            if (angler.FloraSeedCount > _seeds) view.PlayOmniCrystalCollected();   // a crystal just left a seed
            _seeds = angler.FloraSeedCount;
            view.SetDashColor(ResolveDomainSignalColor());
            bool hasSchool = angler.TamedCount > 0;
            view.SetDash(hasSchool ? angler.DashGroup : 0, hasSchool, angler.Dashing);
        }

        /// <summary>The pilot's SHIELDED colour (Space 5's prism shield); alpha 0 until the palette resolves, as the Serpent's.</summary>
        Color ResolveShieldedColor()
        {
            var theme = _gameData?.ThemeManagerData;
            if (theme == null || _status == null || _status.Domain == Domains.Blue) return new Color(0f, 0f, 0f, 0f);
            return theme.GetShieldedSignalColor(_status.Domain);
        }

        /// <summary>The pilot's domain at full brightness (<c>SO_ColorSet.GetDomainSignalColor</c>), as the Serpent's and the
        /// Rhino's cards read it; alpha 0 until the palette resolves, so the view keeps the colour it has.</summary>
        Color ResolveDomainSignalColor()
        {
            var colorSet = _gameData?.ThemeManagerData?.ColorSet;
            if (colorSet == null || _status == null) return new Color(0f, 0f, 0f, 0f);
            return colorSet.GetDomainSignalColor(_status.Domain);
        }
    }
}
