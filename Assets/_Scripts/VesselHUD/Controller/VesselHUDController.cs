using UnityEngine;

namespace CosmicShore.Game
{
    public class VesselHUDController : MonoBehaviour, IVesselHUDController
    {
        private R_VesselActionHandler _actions;
        private VesselHUDView _view;
        private IVesselStatus _status;

        private bool IsLocalOwner => _status is { IsOwnerClient: true };

        public virtual void Initialize(IVesselStatus vesselStatus, VesselHUDView view)
        {
            _status = vesselStatus;
            _view   = view;

            if (!IsLocalOwner)
            {
                if (_view) _view.gameObject.SetActive(false);
                return;
            }

            if (_status.AutoPilotEnabled)
            {
                view.gameObject.SetActive(false);
                return;
            }

            _actions = vesselStatus.ActionHandler;
            if (_actions != null)
            {
                _actions.OnInputEventStarted += HandleStart;
                _actions.OnInputEventStopped += HandleStop;
                _actions.ToggleSubscription(true);
            }
            else
            {
                Debug.LogWarning("[VesselHUDController] ActionHandler is null.");
            }
            _view?.Silhouette?.Initialize(_status);
            _view?.TrailUI?.Initialize(_status);
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

            _view?.Silhouette?.TearDown();
            _view?.TrailUI?.TearDown();
        }

        private void HandleStart(InputEvents ev) => Toggle(ev, true);
        private void HandleStop(InputEvents ev)  => Toggle(ev, false);

        private void Toggle(InputEvents ev, bool on)
        {
            if (!_view || !IsLocalOwner) return;

            for (var i = 0; i < _view.highlights.Count; i++)
            {
                if (_view.highlights[i].input == ev)
                    _view.highlights[i].image.enabled = on;
            }
        }
        
        public void SetBlockPrefab(GameObject prefab)
        {
            _view?.TrailUI?.SetBlockPrefab(prefab);
        }

    }
}
