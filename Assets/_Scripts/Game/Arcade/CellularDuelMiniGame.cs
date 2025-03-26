using UnityEngine;

namespace CosmicShore.Game.Arcade
{
    public class CellularDuelMiniGame : MiniGame 
    {
        [SerializeField] Player aiPlayerPrefab;
        [SerializeField] string aiPlayerName = "HostileOne";

        protected override void Start()
        {
            base.Start();

            initializeAIPlayer();
        }

        void initializeAIPlayer()
        {
            var aiPlayerClone = Instantiate(aiPlayerPrefab);
            aiPlayerClone.TryGetComponent(out IPlayer aiPlayer);
            aiPlayerClone.name = aiPlayerName;
            if (aiPlayer == null)
            {
                Debug.LogError($"Non player prefab provided to aiPlayerPrefab");
                return;
            }

            IPlayer.InitializeData data = new()
            {
                DefaultShipType = ShipTypes.Random,
                Team = Teams.Ruby,
                PlayerName = aiPlayerName,
                PlayerUUID = aiPlayerName,
                Name = aiPlayerName
            };
            aiPlayer.Initialize(data);
            aiPlayer.ToggleGameObject(true);
            aiPlayer.ToggleActive(true);
            aiPlayerClone.Ship.ShipStatus.AIPilot.SkillLevel = .4f + IntensityLevel * .15f;
        }
    }
}