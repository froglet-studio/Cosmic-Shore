using System;
using UnityEngine;

namespace CosmicShore.Integrations.VContainer
{
    public class TestServiceA : IServiceA, IDisposable
    {
        
        public string Message { get; set; } = "Message from A";
        public IModel TestModelA { get; private set; }
        private IModelB _testModelB;

        public TestServiceA(IModel testModelA)
        {
            TestModelA = testModelA;
            _testModelB = new TestModelB();
        }
        public void Call()
        {
            Debug.Log("TestServiceA.Call() is called.");
            Debug.Log($"TestServiceA.Call() is having test model a - " +
                      $"id: {TestModelA.Id} " +
                      $"name: {TestModelA.Name} " +
                      $"start date: {TestModelA.StartDate}");
        }

        public IModelB ProvideModelB()
        {
            return _testModelB;
        }
        
        public void Dispose()
        {
            Message = string.Empty;
            TestModelA = null;
        }
    }
}