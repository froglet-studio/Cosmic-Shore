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

    /// <summary>
    /// One prism spawn/effect request. A STRUCT, deliberately: this is the payload of
    /// the highest-frequency channel in the game — every prism laid and every prism
    /// death raises one — and as a class it was a managed allocation per event, i.e.
    /// per kill in a mass-death burst (a 2,400-death AOE frame allocated 2,400 of
    /// them). The channel is <see cref="GenericEventChannelWithReturnSO{T,Y}"/>, whose
    /// Func&lt;T,Y&gt; is generic over T, so a struct payload passes by value with no
    /// boxing. Nothing retains one beyond the synchronous raise except PrismFactory's
    /// deferred queues, which copy.
    ///
    /// Consequence for callers: there is no null payload any more. A handler must not
    /// test the argument against null (it will not compile), and every field a spawner
    /// reads must be assigned by the raiser — an omitted field is default, not an NRE.
    /// </summary>
    [System.Serializable]
    public struct PrismEventData
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

        /// <summary>
        /// Which prism TIER this event is about - the state the prism was wearing, orthogonal to
        /// <see cref="ownDomain"/>. Death visuals (Explosion / Implosion) key their palette off it
        /// so debris carries the colours of the mass it came from, and danger additionally gets a
        /// harder detonation. Default <see cref="PrismKind.Plain"/>, so producers that do not set
        /// it behave exactly as before.
        /// </summary>
        public PrismKind Kind;
        public Transform TargetTransform;
        public System.Action OnGrowCompleted;

        /// <summary>
        /// PrismType.Grow only: seconds the reverse-suction build-up runs. 0 (the
        /// default) keeps the pool prefab's authored duration. Exists because the
        /// Sparrow's turret rides the grow effect as its FLIGHT visual, so the effect
        /// must last exactly one bullet lifetime, not the fauna-consumption 2s.
        /// </summary>
        public float GrowDuration;
    }

    [CreateAssetMenu(fileName = "EventChannel_Prism", menuName = "ScriptableObjects/Event Channels/PrismEventChannel")]
    public class PrismEventChannelWithReturnSO : GenericEventChannelWithReturnSO<PrismEventData, PrismReturnEventData>
    {
    }
}