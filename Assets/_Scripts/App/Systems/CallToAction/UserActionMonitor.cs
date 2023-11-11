using CosmicShore.Utility.Singleton;
using System;

public class UserActionMonitor : SingletonPersistent<UserActionMonitor>
{
    public delegate void UserActionCompleted(UserAction action);
    public event UserActionCompleted OnUserActionCompleted;

    public void CompleteAction(UserAction action)
    {
        OnUserActionCompleted?.Invoke(action);
    }

    public void CompleteAction(UserActionType actionType)
    {
        CompleteAction(new UserAction(actionType));
    }
}