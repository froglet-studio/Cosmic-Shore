using UnityEngine;

namespace CosmicShore.Game
{
    public class VesselHUDController : MonoBehaviour, IVesselHUDController
    {
        private R_VesselActionHandler _actions;
        private VesselHUDView _view;
        private IVesselStatus _status;

        private bool IsLocalOwner => _status is { IsOwnerClient: true };

        [Header("Legacy Silhouette")]
        [SerializeField] private Silhouette silhouette;

        public virtual void Initialize(IVesselStatus vesselStatus, VesselHUDView view)
        {
            _status = vesselStatus;
            _view   = view;

            if (!IsLocalOwner || _status.AutoPilotEnabled)
            {
                if (_view) _view.Hide();
                return;
            }

            _actions = vesselStatus.ActionHandler;
            if (_actions != null)
            {
                _actions.OnInputEventStarted += HandleStart;
                _actions.OnInputEventStopped += HandleStop;
                _actions.ToggleSubscription(true);
            }
            //
            // if (!silhouette || !_view) return;
            // silhouette.Initialize(_status, _view);
            //
            // if (_view.SilhouetteContainer && _view.TrailDisplayContainer)
            //     silhouette.SetHudReferences(_view.SilhouetteContainer, _view.TrailDisplayContainer);
        }

        public void TearDown()
        {
            if (_actions)
            {
                _actions.OnInputEventStarted -= HandleStart;
                _actions.OnInputEventStopped -= HandleStop;
                _actions.ToggleSubscription(false);
                _actions = null;
            }
        }

        private void HandleStart(InputEvents ev) => Toggle(ev, true);
        private void HandleStop (InputEvents ev) => Toggle(ev, false);

        private void Toggle(InputEvents ev, bool on)
        {
            if (!_view || !IsLocalOwner) return;
            for (var i = 0; i < _view.highlights.Count; i++)
                if (_view.highlights[i].input == ev)
                    _view.highlights[i].image.enabled = on;
        }
        
        public void SetBlockPrefab(GameObject prefab)
        {
            if (_view) _view.TrailBlockPrefab = prefab;
            if (silhouette) silhouette.SetBlockPrefab(prefab);
        }
    }
}
