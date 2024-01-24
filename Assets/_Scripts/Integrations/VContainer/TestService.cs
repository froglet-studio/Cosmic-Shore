using System;
using UnityEngine;

namespace CosmicShore.Integrations.VContainer
{
    public class TestService : IService, IDisposable
    {
        string _serviceName = "Test Service Name";
        public void Call()
        {
            Debug.Log("TestService.Call() is called.");
        }

        public void Dispose()
        {
            Debug.Log("service name disposed.");
            _serviceName = string.Empty;
        }
    }
}
