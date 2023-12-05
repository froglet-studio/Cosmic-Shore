using UnityEngine;

namespace CosmicShore.App.Systems.UserActions
{
    public class UserActionTrigger : MonoBehaviour
    {
        [SerializeField] UserActionType type;
        [SerializeField] int Value;
        [SerializeField] string Label;

        public void TriggerAction()
        {
            UserActionSystem.Instance.CompleteAction(new UserAction(type, Value, Label));
        }

        public void TriggerActionType()
        {
            UserActionSystem.Instance.CompleteAction(type);
        }
    }
}