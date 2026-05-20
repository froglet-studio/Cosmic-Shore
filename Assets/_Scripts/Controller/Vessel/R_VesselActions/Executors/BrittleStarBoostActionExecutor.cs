using Obvious.Soap;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    public class BrittleStarBoostActionExecutor : ShipActionExecutorBase
    {
        [SerializeField] public ScriptableEventNoParam OnMiniGameTurnEnd;
        [SerializeField] private FullAutoActionExecutor fullAutoActionExecutor;
        [SerializeField] private Transform[] muzzlesMain;
        [SerializeField] private Transform[] muzzlesSecondary;

        private IVesselStatus _status;
        private bool _boosting;

        void OnEnable()
        {
            if (OnMiniGameTurnEnd != null)
                OnMiniGameTurnEnd.OnRaised += OnTurnEndOfMiniGame;
        }

        void OnDisable()
        {
            End();
            if (OnMiniGameTurnEnd != null)
                OnMiniGameTurnEnd.OnRaised -= OnTurnEndOfMiniGame;
        }

        public override void Initialize(IVesselStatus shipStatus)
        {
            _status = shipStatus;
        }

        public void Begin(BrittleStarBoostSO so)
        {
            if (_status == null) return;

            _boosting = true;
            _status.IsBoosting = true;
            _status.VesselTransformer?.ModifyVelocity(_status.Course * 100.0f, 1000);
            _status.IsStationary = false;
            ChooseGuns();
        }

        public void End()
        {
            if (!_boosting) return;

            _boosting = false;
            if (_status != null)
                _status.IsBoosting = false;
            ChooseGuns();
        }

        void OnTurnEndOfMiniGame() => End();

        void ChooseGuns()
        {
            if (fullAutoActionExecutor == null) return;

            if (_boosting)
                fullAutoActionExecutor.SetGuns(muzzlesSecondary);
            else
                fullAutoActionExecutor.SetGuns(muzzlesMain);
        }
    }
}