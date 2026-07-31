using UnityEngine;
using CosmicShore.Gameplay;
using UnityEngine.Serialization;
using CosmicShore.Data;
using System;


namespace CosmicShore.ScriptableObjects
{
    public struct PrismReturnEventData
    {
        public GameObject SpawnedObject;
    }

    [System.Serializable]
    public class PrismEventData
    {
        [FormerlySerializedAs("OwnTeam")] public Domains ownDomain;
        public Quaternion Rotation;
        public Vector3 SpawnPosition;
        public Vector3 Scale;
        public Vector3 Velocity;

        /// <summary>
        /// Per-impact ceiling on the debris speed, overriding the explosion prefab's own
        /// <c>maxSpeed</c>. 0 = use the prefab value. Exists because that prefab ceiling is a
        /// guard against the legacy <c>impactVector / volume</c> gain, which spans ~100x across
        /// prism sizes; an impact that already hands over a TRUE velocity (pre-multiplied by
        /// Volume so the divide cancels - see PrismEffectHelper.DamageProportional) needs a
        /// ceiling sized to real speeds instead, or the accurate magnitude is clipped away.
        /// </summary>
        public float DebrisSpeedLimit;

        public float Volume;
        public PrismType PrismType;
        public Transform TargetTransform;
        public System.Action OnGrowCompleted;
    }

    [CreateAssetMenu(fileName = "EventChannel_Prism", menuName = "ScriptableObjects/Event Channels/PrismEventChannel")]
    public class PrismEventChannelWithReturnSO : GenericEventChannelWithReturnSO<PrismEventData, PrismReturnEventData>
    {
    }
}