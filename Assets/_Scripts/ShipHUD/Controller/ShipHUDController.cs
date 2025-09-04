using System;
using System.Collections.Generic;
using CosmicShore.Utilities;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CosmicShore.Game
{
    [RequireComponent(typeof(IShipStatus))]
    public class ShipHUDController : MonoBehaviour, IShipHUDController
    {
        [SerializeField] private ShipClassType shipType;
        [SerializeField] private ShipHUDProfileSO profile;
        [SerializeField] private ShipHUDRefs refs;
        [SerializeField] private SceneNameListSO sceneNameListSO;
        private IShip _ship;
        
        private IShipStatus _status;
        private IShipHUDView _view;

        private readonly List<HudSubscriptionSO> _liveSubs = new();

        private void Awake()
        {
            _ship = GetComponent<IShip>();
            _status = _ship.ShipStatus;
        }
        
        private void Start()
        {
            InitializeShipHUD();
        }

        public void Initialize(IShipStatus status, R_ShipHUDView view)
        {
          
        }

        public void InitializeShipHUD()
        {
            if (SceneManager.GetActiveScene().name == sceneNameListSO.MainMenuScene) return;

            // var view = _status.ShipHUDContainer.InitializeView(this, _status);
            // if (view == null) return;
            //
            // _status.ShipHUDView = view;
            // _view = view;

            var effects = (_view is IHasEffects hasFx) ? hasFx.Effects : null;
            if (profile?.subscriptions == null || profile.subscriptions.Length == 0)
            {
                Debug.LogWarning("[ShipHUDController] No subscriptions in profile."); return;
            }

            foreach (var sub in profile.subscriptions)
            {
                if (!sub) continue;
                var runtime = Instantiate(sub);
                runtime.name = sub.name + " (Runtime)";
                runtime.Initialize(_ship, _status, effects, refs);
                _liveSubs.Add(runtime);
            }
        }

        public void DisposeHUD()
        {
            for (int i = 0; i < _liveSubs.Count; i++)
                if (_liveSubs[i] != null) _liveSubs[i].Dispose();
            _liveSubs.Clear();

            if (_status?.ShipHUDView is not MonoBehaviour mb || !mb) return;
            Destroy(mb.gameObject);
            _status.ShipHUDView = null;
            _view = null;
        }

        public void OnButtonPressed(int buttonNumber)
        {
            _ship?.PerformButtonActions(buttonNumber);
            _view?.OnInputPressed(buttonNumber);
        }
        public void OnButtonReleased(int buttonNumber) => _view?.OnInputReleased(buttonNumber);
    }
}
