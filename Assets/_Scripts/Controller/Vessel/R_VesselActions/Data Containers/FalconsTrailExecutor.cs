using CosmicShore.Game.Projectiles;
using Cysharp.Threading.Tasks;
using Obvious.Soap;
using System.Threading;
using UnityEngine;
//using static Unity.Cinemachine.InputAxisControllerBase<T>;

namespace CosmicShore.Game
{
    
    public class FalconsTrailExecutor : ShipActionExecutorBase
    {

        private IVesselStatus _status;
        public ScriptableEventNoParam OnMiniGameTurnEnd;
        private CancellationTokenSource _cts;
        [SerializeField] VesselPrismController controller;

        void OnEnable()
        {
            Debug.LogError("Trail Started");
            OnMiniGameTurnEnd.OnRaised += OnTurnEndOfMiniGame;
        }

        void OnDisable()
        {
            End();
            OnMiniGameTurnEnd.OnRaised -= OnTurnEndOfMiniGame;
        }

        public override void Initialize(IVesselStatus shipStatus)
        {
            _status = shipStatus;
            if (controller == null)
                controller = shipStatus?.VesselPrismController;



        }


        public void Begin(FalconTrailSO so)
        {
            Debug.LogError("Trail Started");
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
