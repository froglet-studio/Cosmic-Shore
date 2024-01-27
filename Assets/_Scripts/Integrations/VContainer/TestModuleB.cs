using UnityEngine;
using VContainer.Unity;

namespace CosmicShore.Integrations.VContainer
{
    public class TestModuleB : IStartable
    {
        private readonly ActorSettings _actorSettings;
        private readonly CameraSettings _cameraSettings;
        public TestModuleB(ActorSettings actorSettings, CameraSettings cameraSettings)
        {
            _actorSettings = actorSettings;
            _cameraSettings = cameraSettings;
        }

        private void Configure()
        {
            Debug.LogFormat("TestServiceD get camera settings: " +
                            "move speed {0} " +
                            "default distance {1} " +
                            "zoom max {2} " +
                            "zoom in {3}", 
                _cameraSettings.MoveSpeed,
                _cameraSettings.DefaultDistance,
                _cameraSettings.ZoomMax,
                _cameraSettings.ZoomIn);
            
            Debug.LogFormat("TestServiceD get actor settings: " +
                            "move speed {0} " +
                            "flying time {1} " +
                            "initial velocity {2} ", 
                _actorSettings.MoveSpeed,
                _actorSettings.FlyingTime,
                _actorSettings.InitialVelocity);
        }

        public void Start()
        {
            Configure();
        }
    }
}