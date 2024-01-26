using UnityEngine;
using VContainer;

namespace CosmicShore.Integrations.VContainer
{
    public class TestComponentA : MonoBehaviour
    {
        private string _message;
        private void Start()
        {
            Debug.Log("TestComponentA starts.");
        }

        [Inject]
        public void Construct(IServiceA serviceA)
        {
            _message = serviceA.Message;
            Debug.Log($"TestComponentA Initializes message from Service A :{_message}");
        }
        
        
    }
}