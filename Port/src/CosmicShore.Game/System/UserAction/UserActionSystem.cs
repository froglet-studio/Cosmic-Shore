using CosmicShore.Utility;
using CosmicShore.Data;

namespace CosmicShore.Core
{
    public class UserActionSystem : SingletonPersistent<UserActionSystem>
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
}