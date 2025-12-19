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



        void OnEnable()
        {
            if (_status == null) return;
            _status.IsBoosting = true;
            _status.VesselTransformer?.ModifyVelocity(_status.Course, 100f);
            _status.IsStationary = false;


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

            _cts.Cancel();
            _cts.Dispose();
            _cts = null;
        }

        void OnTurnEndOfMiniGame()
        {
            End();
        }
    }
}
