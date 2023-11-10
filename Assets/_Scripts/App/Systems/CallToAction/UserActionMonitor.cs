using StarWriter.Utility.Singleton;

public class UserActionMonitor : SingletonPersistent<UserActionMonitor>
{
    public delegate void UserActionCompleted(UserAction action);
    public event UserActionCompleted OnUserActionCompleted;

    public void CompleteAction(UserAction action)
    {
        OnUserActionCompleted?.Invoke(action);
    }
}