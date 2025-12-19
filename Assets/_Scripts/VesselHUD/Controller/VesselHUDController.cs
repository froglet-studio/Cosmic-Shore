using System;
using UnityEngine;

namespace CosmicShore.Game
{
    public class VesselHUDController : MonoBehaviour, IVesselHUDController
    {
        private R_VesselActionHandler _actions;
        private VesselHUDView _view;

        [Header("Legacy Silhouette")]
        [SerializeField] private SilhouetteController silhouette;

        private void OnDestroy()
        {
            UnsubscribeFromEvents();
        }

        public virtual void Initialize(IVesselStatus vesselStatus, VesselHUDView view)
        {
            _view   = view;
            _actions = vesselStatus.ActionHandler;
        }

        public void SubscribeToEvents()
        {
            _actions.OnInputEventStarted += HandleStart;
            _actions.OnInputEventStopped += HandleStop;
        }

        public void UnsubscribeFromEvents()
        {
            _actions.OnInputEventStarted -= HandleStart;
            _actions.OnInputEventStopped -= HandleStop;
        }

        private void HandleStart(InputEvents ev) => Toggle(ev, true);
        private void HandleStop (InputEvents ev) => Toggle(ev, false);

        private void Toggle(InputEvents ev, bool on)
        {
            for (var i = 0; i < _view.highlights.Count; i++)
                if (_view.highlights[i].input == ev)
                    _view.highlights[i].image.enabled = on;
        }
        
        public void SetBlockPrefab(GameObject prefab)
        {
            if (prefab == null)
            {
                Debug.LogError("Block prefab is NULL!");
                return;
            }

            if (silhouette == null)
            {
                Debug.LogError("HUD blockContainer is NULL!");
                return;
            }


            _view.TrailBlockPrefab = prefab;
            silhouette.SetBlockPrefab(prefab);
        }
    }
}
