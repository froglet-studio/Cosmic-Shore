using UnityEngine;

namespace CosmicShore.Integrations.VContainer
{
    public class TestServiceA : IServiceA
    {
        public void Call()
        {
            Debug.Log("TestServiceA.Call() is called.");
        }
    }
}