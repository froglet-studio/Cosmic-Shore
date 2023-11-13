using UnityEngine;

namespace CosmicShore.App.Systems.UserActions
{
    [System.Serializable]
    public class UserAction
    {
        public UserActionType ActionType;
        public int Value;
        public string Label;

        public UserAction(UserActionType actionType, int value = 1, string label = "")
        {
            ActionType = actionType;
            Value = value;
            Label = string.IsNullOrEmpty(label) ? ActionType.ToString() : label;
        }

        public static string GetGameplayUserActionLabel(MiniGames gameMode, ShipTypes shipType, int intensity)
        {
            Debug.LogWarning($"GetGameplayUserActionLabel: {gameMode}_{shipType}_{intensity}");
            return $"{gameMode}_{shipType}_{intensity}";
        }
    }
}