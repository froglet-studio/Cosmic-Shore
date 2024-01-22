using System;
using UnityEngine;

namespace CosmicShore.Integrations.VContainer
{
    public class TestServiceA : IServiceA, IDisposable
    {
        public string Message { get; set; } = "Message from A";

        public void Call()
        {
            Debug.Log("TestServiceA.Call() is called.");
        }
        
        public void Dispose()
        {
            Message = string.Empty;
        }
    }
}