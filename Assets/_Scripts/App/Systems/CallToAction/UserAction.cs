[System.Serializable]
public class UserAction
{
    public UserActionType ActionType;
    public int Value;
    public string Label;

    public UserAction(UserActionType actionType, int value=1, string label="")
    {
        ActionType = actionType;
        Value = value;
        Label = label;
     }

    public static string GetGameplayUserActionLabel(MiniGames gameMode, ShipTypes shipType, int intensity)
    {
        return $"{gameMode}_{shipType}_{intensity}";
    }
}