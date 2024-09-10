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

        public static string GetGameplayUserActionLabel(GameModes gameMode, ShipTypes shipType, int intensity)
        {
            Debug.Log($"GetGameplayUserActionLabel: {gameMode}_{shipType}_{intensity}");
            return $"{gameMode}_{shipType}_{intensity}";
        }
    }
}