using System;
using UnityEngine;

namespace CosmicShore.Integrations.VContainer
{
    public class TestServiceA : IServiceA, IDisposable
    {
        
        public string Message { get; set; } = "Message from A";
        public IModelA TestModelAA { get; private set; }
        private IModelB _testModelB;

        public TestServiceA(IModelA testModelAA)
        {
            TestModelAA = testModelAA;
            _testModelB = new TestModelB();
        }
        public void Call()
        {
            Debug.Log("TestServiceA.Call() is called.");
            Debug.Log($"TestServiceA.Call() is having test model a - " +
                      $"id: {TestModelAA.Id} " +
                      $"name: {TestModelAA.Name} " +
                      $"start date: {TestModelAA.StartDate}");
        }

        public IModelB ProvideModelB()
        {
            return _testModelB;
        }
        
        public void Dispose()
        {
            Message = string.Empty;
            TestModelAA = null;
        }
    }
}