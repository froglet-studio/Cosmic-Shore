using CosmicShore.Gameplay;
using CosmicShore.Utility;
using Obvious.Soap;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    public sealed class ShardToggleActionExecutor : ShipActionExecutorBase
    {
        [Header("Bus (required)")]
        [SerializeField] private ShardFieldBus shardFieldBus;

        [SerializeField] private CellRuntimeDataSO cellData;

        [Header("Events")]
        [SerializeField] private ScriptableEventNoParam OnMiniGameTurnEnd;

        bool _redirectActive;

        void OnEnable()
        {
            OnMiniGameTurnEnd.OnRaised += OnTurnEndOfMiniGame;
        }

        void OnDisable()
        {
            OnMiniGameTurnEnd.OnRaised -= OnTurnEndOfMiniGame;
        }

        void OnTurnEndOfMiniGame() => End();

        public override void Initialize(IVesselStatus shipStatus) { }

        public void Toggle(ShardToggleActionSO so,  IVesselStatus status)
        {
            if (!cellData)
            {
                CSDebug.LogError("No Cell data found!");
                return;
            }
        
            if (!_redirectActive)
            {
                var cell = cellData.Cell;
                Vector3 highDensityPosition = cell.GetExplosionTarget(so.Domain);
                shardFieldBus.BroadcastPointAtPosition(highDensityPosition);
                _redirectActive = true;
            }
            else
            {
                shardFieldBus.BroadcastRestoreToCrystal();
                _redirectActive = false;
            }
        }

         void End()
         {
             if (!_redirectActive) return;
             shardFieldBus.BroadcastRestoreToCrystal();
             _redirectActive = false;
         }
    }
}
