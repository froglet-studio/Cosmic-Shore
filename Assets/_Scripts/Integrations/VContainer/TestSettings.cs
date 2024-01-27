using System;
using UnityEngine;

namespace CosmicShore.Integrations.VContainer
{
    [Serializable]
    public class CameraSettings
    {
        public float MoveSpeed = 10.0f;
        public float DefaultDistance = 5.0f;
        public float ZoomMax = 20.0f;
        public float ZoomIn = 5.0f;
    }

    [Serializable]
    public class ActorSettings
    {
        public float MoveSpeed = 0.5f;
        public float FlyingTime = 2.0f;
        public Vector3 InitialVelocity = Vector3.zero;
    }

    [CreateAssetMenu(fileName = "TestSettings", menuName = "Test/Settings")]
    public class TestSettings : ScriptableObject
    {
        public CameraSettings cameraSettings;
        public ActorSettings actorSettings;
    }
}