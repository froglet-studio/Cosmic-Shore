using System.Collections.Generic;
using UnityEngine;
using CosmicShore.Gameplay;
using CosmicShore.Data;
using CosmicShore.UI;
using CosmicShore.Core;
using CosmicShore.Utility;
using Reflex.Attributes;

namespace CosmicShore.Gameplay
{
    public class SilhouetteController : MonoBehaviour
    {
        [Header("Sources")]
        [SerializeField] private VesselPrismController vesselPrismController;
        [SerializeField] private DriftTrailActionExecutor driftTrailAction;

        [Header("Config")]
        [SerializeField] private SilhouetteConfigSO config;

        [Header("Energy")]
        [SerializeField] private int energyResourceIndex = 0; // index in ResourceSystem

        [Header("View")]
        [SerializeField] private SilhouetteView view; // view

        [Header("Element Bars")]
        [SerializeField] private ElementalBarsView elementBars;

        [Inject] private GameDataSO gameData;

        private IVessel _vessel;
        private IVesselStatus _status;
        private ResourceSystem _resources;
        private float _dot = .9999f;
        private DriftTrailActionExecutor.ChangeDriftAltitude _driftHandler;

        // trail data (pass to view)
        private float _xShift, _wavelength, _sx, _sy, _sz;
        private bool _haveHead;

        void OnEnable()
        {
            if (vesselPrismController)
            {
                vesselPrismController.OnBlockCreated += OnBlockCreated;
                vesselPrismController.OnBlockSpawned += OnBlockSpawned_Color;
            }

            if (driftTrailAction)
            {
                _driftHandler = OnDriftChanged;
                driftTrailAction.OnChangeDriftAltitude += _driftHandler;
            }

            TrySubscribeResources();
            TrySubscribeElementBars();

            //flower explosion
            VesselExplosionByCrystalEffectSO.OnMantaFlowerExplosion += HandleMantaFlowerExplosion;
        }

        void OnDisable()
        {
            if (vesselPrismController)
            {
                vesselPrismController.OnBlockCreated -= OnBlockCreated;
                vesselPrismController.OnBlockSpawned -= OnBlockSpawned_Color;
            }

            if (driftTrailAction != null && _driftHandler != null)
            {
                driftTrailAction.OnChangeDriftAltitude -= _driftHandler;
                _driftHandler = null;
            }

            TryUnsubscribeResources();
            TryUnsubscribeElementBars();

            VesselExplosionByCrystalEffectSO.OnMantaFlowerExplosion -= HandleMantaFlowerExplosion;
        }

        public void Initialize(IVesselStatus status)
        {
            _status = status;
            _vessel = status?.Vessel;
            _resources = status?.ResourceSystem;

            TrySubscribeResources();

            if (_resources != null && energyResourceIndex >= 0 && energyResourceIndex < _resources.Resources.Count)
            {
                var r = _resources.Resources[energyResourceIndex];
                view?.UpdateEnergyUI(r.CurrentAmount, r.MaxAmount);
            }

            InitializeElementBars();
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

        void OnDriftChanged(float dot) => _dot = dot;

        public void SetBlockPrefab(GameObject prefab)
        {
            view?.SetBlockPrefab(prefab);
        }

        public void Clear()
        {
            view?.Clear();
        }

        void LateUpdate()
        {
            if (_status != null && view) view.SyncSilhouetteRotation2D(_status, _dot);

            if (!_haveHead) return;
            view?.ApplyHeadAndConveyor(_xShift, _wavelength, _sx, _sy, _sz, _dot);
        }

        void OnBlockCreated(float xShift, float wavelength, float scaleX, float scaleY, float scaleZ)
        {
            if (_vessel?.VesselStatus == null || _vessel.VesselStatus.AutoPilotEnabled) return;

            _xShift = xShift;
            _wavelength = wavelength;
            _sx = scaleX;
            _sy = scaleY;
            _sz = scaleZ;
            _haveHead = true;

            view?.BuildPoolIfNeeded(scaleY, wavelength);
            view?.ApplyHeadAndConveyor(_xShift, _wavelength, _sx, _sy, _sz, _dot);
        }

        void OnBlockSpawned_Color(Prism prism)
        {
            if (!prism) return;

            Color tint = default;
            var haveTint = false;
            var isDanger = false;
            try { isDanger = prism.prismProperties != null && prism.prismProperties.IsDangerous; } catch { }

            // Domain (and danger) tint now come from the same theme ColorSet the
            // vessels and prisms use (R5) — danger maps to the shared EnvironmentColors.Danger.
            var colorSet = gameData != null && gameData.ThemeManagerData != null
                ? gameData.ThemeManagerData.ColorSet
                : null;
            if (config && config.useDomainPaletteColors && colorSet)
            {
                if (isDanger)
                {
                    tint = colorSet.EnvironmentColors.Danger;
                }
                else
                {
                    var dom = _vessel?.VesselStatus?.Domain ?? Domains.Blue;
                    tint = colorSet.GetDomainUIColor(dom);
                }
                haveTint = true;
            }

            if (!haveTint) return;
            view?.ApplyTintToTrail(tint);
        }

        // --- PLACEHOLDERS FOR FORWARDED CALLS ---
        public void UpdateEnergyUI(float current, float max)
        {
            view?.UpdateEnergyUI(current, max);
        }

        public void SyncSilhouetteRotation2D(IVesselStatus status, float dot)
        {
            view?.SyncSilhouetteRotation2D(status, dot);
        }

        public void BuildPoolIfNeeded(float scaleY, float wavelength)
        {
            view?.BuildPoolIfNeeded(scaleY, wavelength);
        }

        public void ApplyHeadAndConveyor(float xShift, float wavelength, float sx, float sy, float sz)
        {
            view?.ApplyHeadAndConveyor(xShift, wavelength, sx, sy, sz, _dot);
        }

        public void ApplyTintToTrail(Color tint)
        {
            view?.ApplyTintToTrail(tint);
        }

        public void SetDangerVisual(bool dangerEnabled)
        {
            view?.SetDangerVisual(dangerEnabled);
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
