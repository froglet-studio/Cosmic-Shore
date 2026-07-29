using UnityEngine;
using CosmicShore.Gameplay;
using CosmicShore.Data;
using CosmicShore.UI;
using CosmicShore.Utility;
using Reflex.Attributes;

namespace CosmicShore.Gameplay
{
    public class SilhouetteController : MonoBehaviour
    {
        [Header("Energy")]
        [SerializeField] private int energyResourceIndex = 0; // index in ResourceSystem

        [Header("View")]
        [SerializeField] private SilhouetteView view; // view

        [Header("Element Bars")]
        [SerializeField] private ElementalBarsView elementBars;

        [Inject] private GameDataSO gameData;

        private IVesselStatus _status;
        private ResourceSystem _resources;

        void OnEnable()
        {
            TrySubscribeResources();
            TrySubscribeElementBars();

            //flower explosion
            VesselExplosionByCrystalEffectSO.OnMantaFlowerExplosion += HandleMantaFlowerExplosion;
        }

        void OnDisable()
        {
            TryUnsubscribeResources();
            TryUnsubscribeElementBars();

            VesselExplosionByCrystalEffectSO.OnMantaFlowerExplosion -= HandleMantaFlowerExplosion;
        }

        public void Initialize(IVesselStatus status)
        {
            _status = status;
            _resources = status?.ResourceSystem;

            TrySubscribeResources();

            if (_resources != null && energyResourceIndex >= 0 && energyResourceIndex < _resources.Resources.Count)
            {
                var r = _resources.Resources[energyResourceIndex];
                view?.UpdateEnergyUI(r.CurrentAmount, r.MaxAmount);
            }

            InitializeElementBars();

            // Holo icon accent: the same per-domain UI accent role the Maelstrom cards use
            // (SO_ColorSet.GetDomainUIAccentColor) - one colour source, S0.1.
            if (view && _status != null)
            {
                var accent = gameData != null && gameData.ThemeManagerData != null
                    ? gameData.ThemeManagerData.GetDomainUIAccentColor(_status.Domain)
                    : Color.white;
                view.ApplyHoloStyle(accent);
            }
        }

        void TrySubscribeResources()
        {
            if (_resources == null) return;
            TryUnsubscribeResources();
            _resources.OnResourceChanged += HandleResourceChanged;
        }

        void TryUnsubscribeResources()
        {
            if (_resources == null) return;
            _resources.OnResourceChanged -= HandleResourceChanged;
        }

        void HandleResourceChanged(int index, float current, float max)
        {
            if (index != energyResourceIndex) return;
            view?.UpdateEnergyUI(current, max);
        }

        void LateUpdate()
        {
            if (_status != null && view) view.SyncSilhouetteRotation2D(_status);
        }

        // --- PLACEHOLDERS FOR FORWARDED CALLS ---
        public void UpdateEnergyUI(float current, float max)
        {
            view?.UpdateEnergyUI(current, max);
        }

        public void SyncSilhouetteRotation2D(IVesselStatus status)
        {
            view?.SyncSilhouetteRotation2D(status);
        }

        private void HandleMantaFlowerExplosion(VesselImpactor vessel)
        {
            view?.ShowMantaFlowerOverlay();
        }

        // --- Element Bars ---
        void TryUnsubscribeElementBars()
        {
            if (_resources != null)
                _resources.OnElementLevelChange -= HandleElementLevelChanged;
        }

        // OnDisable detaches the element-level handler, so OnEnable must re-attach it
        // (mirroring TrySubscribeResources) — otherwise a disable/enable cycle leaves
        // the petal flowers frozen while the energy UI keeps updating. Re-seeds levels
        // because they may have changed while disabled (SetLevel early-outs on no-change).
        void TrySubscribeElementBars()
        {
            if (_resources == null || !elementBars) return;
            TryUnsubscribeElementBars();
            _resources.OnElementLevelChange += HandleElementLevelChanged;
            SeedElementBarLevels();
        }

        void SeedElementBarLevels()
        {
            elementBars.SetLevel(Element.Charge, _resources.GetLevel(Element.Charge));
            elementBars.SetLevel(Element.Mass, _resources.GetLevel(Element.Mass));
            elementBars.SetLevel(Element.Space, _resources.GetLevel(Element.Space));
            elementBars.SetLevel(Element.Time, _resources.GetLevel(Element.Time));
        }

        void InitializeElementBars()
        {
            // The element flower display is a REQUIRED system on every vessel: when the
            // prefab doesn't author an ElementalBarsView, create one on the vessel's HUD
            // canvas. The view self-populates its four default bindings and the shared
            // ElementalBarsConfig stamps the fleet-standard placement, so no per-vessel
            // wiring is needed. Vessels with an authored view (Squirrel, Sparrow) keep it.
            if (!elementBars)
                elementBars = CreateDefaultElementBars();
            if (!elementBars) return;

            elementBars.Build();

            TrySubscribeElementBars();
        }

        ElementalBarsView CreateDefaultElementBars()
        {
            var canvas = GetComponentInChildren<Canvas>(true);
            if (!canvas) return null; // no HUD surface on this vessel — nothing to show on

            CSDebug.LogWarning($"[SilhouetteController] '{name}' has no authored ElementalBarsView - " +
                               "creating one at RUNTIME so the fleet-required display still shows. " +
                               "Author it into the HUD prefab: Tools > Cosmic Shore > Bake Elemental " +
                               "Petal Bars Into All Vessel HUDs, then wire it to elementBars.");
            var go = new GameObject("ElementalBars (auto)", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(canvas.transform, false);
            return go.AddComponent<ElementalBarsView>();
        }

        void HandleElementLevelChanged(Element element, int level)
        {
            elementBars?.SetLevel(element, level);
        }

        /// <summary>
        /// Exposes the ElementalBarsView for the HUD controller to apply juice effects.
        /// </summary>
        public ElementalBarsView ElementBars => elementBars;

    }
}
