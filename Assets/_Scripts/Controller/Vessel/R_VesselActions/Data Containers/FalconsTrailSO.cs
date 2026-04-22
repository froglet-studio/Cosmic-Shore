//using CosmicShore.Game.Projectiles;
//using Cysharp.Threading.Tasks;
//using Obvious.Soap;
//using System.Threading;
//using UnityEngine;

//namespace CosmicShore.Game
//{

//    public class FalconsTrailExecutor : ShipActionExecutorBase
//    {

//        private IVesselStatus _status;
//        public ScriptableEventNoParam OnMiniGameTurnEnd;
//        private CancellationTokenSource _cts;

//        void OnEnable()
//        {
//            Debug.Log("Trail Started");
//            OnMiniGameTurnEnd.OnRaised += OnTurnEndOfMiniGame;
//        }

//        void OnDisable()
//        {
//            End();
//            OnMiniGameTurnEnd.OnRaised -= OnTurnEndOfMiniGame;
//        }

//        public override void Initialize(IVesselStatus shipStatus)
//        {
//            _status = shipStatus;
        

//        }


//        public void Begin(FullAutoActionSO so)
//        {

//        }

//        public void End()
//        {
//            if (_cts == null) return;

//            _cts.Cancel();
//            _cts.Dispose();
//            _cts = null;
//        }

//        void OnTurnEndOfMiniGame()
//        {
//            End();
//        }




//    }
//}
