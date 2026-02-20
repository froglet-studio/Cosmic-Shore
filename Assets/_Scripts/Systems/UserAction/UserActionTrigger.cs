using UnityEngine;

namespace CosmicShore.App.Systems.UserActions
{
    public class UserActionTrigger : MonoBehaviour
    {
        [Tooltip("Required")]
        [SerializeField] UserActionType type;
        [Tooltip("Optional - Must be set if using 'TriggerAction'")]
        [SerializeField] int Value;
        [Tooltip("Optional - Must be set if using 'TriggerAction'")]
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