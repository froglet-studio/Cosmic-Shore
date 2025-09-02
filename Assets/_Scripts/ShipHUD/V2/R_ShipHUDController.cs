using System.Linq;
using UnityEngine;

namespace CosmicShore.Game
{
    public class R_ShipHUDController : MonoBehaviour, IShipHUDController
    {
        private R_ShipActionHandler _actions;
        private R_ShipHUDView _view;

        public virtual void Initialize(IShipStatus shipStatus, R_ShipHUDView view)
        {
            if (shipStatus.AutoPilotEnabled)
            {
                view.gameObject.SetActive(false);
                return;
            }
            
            _actions = shipStatus.ActionHandler;
            _view = view;
            
            if (_actions == null) return;
            _actions.OnInputEventStarted += HandleStart;
            _actions.OnInputEventStopped += HandleStop;

        }

        public void InitializeShipHUD()
        {
        }

        public void OnButtonPressed(int buttonNumber)
        {
        }

        public void OnButtonReleased(int buttonNumber)
        {
        }

        private void OnDestroy()
        {
            if (_actions == null) return;
            _actions.OnInputEventStarted -= HandleStart;
            _actions.OnInputEventStopped -= HandleStop;
        }

        private void HandleStart(InputEvents ev) => Toggle(ev, true);
        private void HandleStop (InputEvents ev) => Toggle(ev, false);

        private void Toggle(InputEvents ev, bool on)
        {
            for (var i = 0; i < _view.highlights.Count; i++)
            {
                if (_view.highlights[i].input == ev && _view.highlights[i].image != null)
                    _view.highlights[i].image.enabled = on;
            }
        }
    }
}