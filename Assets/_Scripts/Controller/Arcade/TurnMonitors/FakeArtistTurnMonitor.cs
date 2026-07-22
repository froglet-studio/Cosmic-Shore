using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Turn monitor for Fake Artist. The WIN target - the point total a player must
    /// reach to take the gallery (default 8) - is resolved at <see cref="StartMonitor"/>
    /// from <see cref="EndConditionOverridesSO"/> (Tools &gt; Cosmic Shore &gt; End Game
    /// Conditions; never a per-scene field), synced via NetworkVariable, and published
    /// to <see cref="GameDataSO.GoalTargetCount"/>.
    ///
    /// A Fake Artist TURN is one whole round (draw → vote → reveal), owned by the
    /// controller's server-side phase machine - so the end check simply asks the
    /// controller whether the round is resolved. The first-to-N-points check runs in
    /// the controller's OnTurnEndedCustom, not here. The display channel mirrors the
    /// controller's phase text (drawing countdown / VOTE / reveal) to every peer.
    /// </summary>
    public class FakeArtistTurnMonitor : TurnMonitor
    {
        [Header("Fake Artist")]
        [Tooltip("The scene's FakeArtistController - the monitor ends the turn when its round resolves.")]
        [SerializeField] FakeArtistController controller;

        readonly NetworkVariable<int> _netWinTarget = new(0);

        void OnEnable()
        {
            _netWinTarget.OnValueChanged += OnWinTargetSynced;
        }

        void OnDisable()
        {
            _netWinTarget.OnValueChanged -= OnWinTargetSynced;
        }

        void OnWinTargetSynced(int previousValue, int newValue)
        {
            if (newValue > 0)
                gameData.GoalTargetCount = newValue;
        }

        public override void StartMonitor()
        {
            base.StartMonitor();

            if (IsServer)
            {
                var overrides = EndConditionOverridesSO.Instance;
                int target = overrides != null
                    ? overrides.GetFakeArtistWinTarget()
                    : EndConditionOverridesSO.DefaultFakeArtistWinTarget;

                _netWinTarget.Value = target;
                gameData.GoalTargetCount = target;

                CSDebug.Log($"[FakeArtistTurnMonitor] Server set win target: {target}");
            }
            else if (_netWinTarget.Value > 0)
            {
                gameData.GoalTargetCount = _netWinTarget.Value;
            }
        }

        public override bool CheckForEndOfTurn()
        {
            if (!IsServer) return false;
            return controller != null && controller.IsRoundResolved;
        }

        protected override void RestrictedUpdate()
        {
            // 1s server tick pushing the phase display to every peer's HUD countdown
            // text - no per-stat subscriptions to leak (B15 class).
            if (!IsServer || controller == null) return;
            UpdateDisplay_ClientRpc(new FixedString32Bytes(controller.PhaseDisplayText));
        }

        [ClientRpc]
        void UpdateDisplay_ClientRpc(FixedString32Bytes message)
        {
            if (onUpdateTurnMonitorDisplay)
                onUpdateTurnMonitorDisplay.Raise(message.ToString());
        }
    }
}
