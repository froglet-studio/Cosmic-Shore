using System;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace CosmicShore.Integrations.VContainer
{
    public class TestRuntimePresenter
    {
        private readonly Func<int, TestModelA> _factory;

        public TestRuntimePresenter(Func<int, TestModelA> factory)
        {
            _factory = factory;
        }

        public void Start()
        {
            var modelA = _factory(100);
            Debug.Log($"TestRuntimeModel.Start() has model c : {modelA.Id}");
        }
    }
}