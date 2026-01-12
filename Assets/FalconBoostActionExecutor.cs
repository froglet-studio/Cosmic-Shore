using CosmicShore.Game;
using Obvious.Soap;
using System.Diagnostics;
using System.Threading;
using UnityEngine;


namespace CosmicShore
{
    public class FalconBoostActionExecutor : ShipActionExecutorBase
    {
        private IVesselStatus _status;
        public ScriptableEventNoParam OnMiniGameTurnEnd;
        private CancellationTokenSource _cts;

        [SerializeField] private FullAutoActionExecutor FullAutoActionExecutor;
        [SerializeField] private Transform[] muzzlesMain;
        [SerializeField] private Transform[] muzzlesSecondary;


        void OnEnable()
        {
            if (_status == null) return;
            _status.IsBoosting = true;
            _status.VesselTransformer?.ModifyVelocity(_status.Course * 100.0f, 1000);
            _status.IsStationary = false;
            ChooseGuns();

            OnMiniGameTurnEnd.OnRaised += OnTurnEndOfMiniGame;
        }

        void OnDisable()
        {
            if (_status == null) return;
            _status.IsBoosting = false;
            OnMiniGameTurnEnd.OnRaised -= OnTurnEndOfMiniGame;
        }

        public override void Initialize(IVesselStatus shipStatus)
        {

            base.Initialize(_status);


        }


        public void Begin(FalconBoostSO so)
        {
            //Debug.LogError("Boost Started");
        }

        public void End()
        {
            if (_cts == null) return;

            ChooseGuns();
            _cts.Cancel();
            _cts.Dispose();
            _cts = null;

        }

        void OnTurnEndOfMiniGame()
        {
            End();
        }

        void ChooseGuns()
        {
         
            if (_status.IsBoosting)
            {
                FullAutoActionExecutor.setGuns(muzzlesSecondary);
            }
            else
            {
                FullAutoActionExecutor.setGuns(muzzlesMain);
            }
            
        }
    }
}
