using System.Collections.Generic;
using UnityEngine;
using CosmicShore.Game;
using CosmicShore.Core;

namespace CosmicShore
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

        private IVessel _vessel;
        private IVesselStatus _status;
        private ResourceSystem _resources;
        private float _dot = .9999f;
        private DriftTrailActionExecutor.ChangeDriftAltitude _driftHandler;

        // trail data (pass to view)
        private float _xShift, _wavelength, _sx, _sy, _sz;
        private bool _haveHead;
        private bool _dangerActive;

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

            if (isDanger && config && config.useDomainPaletteColors && config.domainPalette)
            {
                tint = config.domainPalette.danger;
                haveTint = true;
            }
            else if (config && config.useDomainPaletteColors && config.domainPalette)
            {
                var dom = _vessel?.VesselStatus?.Domain ?? Domains.Unassigned;
                tint = config.domainPalette.Get(dom);
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
            _dangerActive = dangerEnabled;
            view?.SetDangerVisual(dangerEnabled);
        }

        private void HandleMantaFlowerExplosion(VesselImpactor vessel)
        {
            view?.ShowMantaFlowerOverlay();
        }

    }
}
