using Unity.Entities;
using Unity.Mathematics;

namespace CosmicShore.Game.ECS.Components
{
    /// <summary>
    /// Core prism data for ECS migration. Maps to the MonoBehaviour Prism + PrismProperties.
    /// When fully migrated, each prism becomes an entity with these components
    /// instead of a GameObject with 5-6 MonoBehaviours.
    /// </summary>
    public struct PrismData : IComponentData
    {
        public float3 Position;
        public quaternion Rotation;
        public float3 Scale;
        public float3 TargetScale;
        public float GrowthRate;
        public int Domain; // Maps to Domains enum
        public float Volume;
        public float TimeCreated;
        public byte IsShielded;
        public byte IsSuperShielded;
        public byte IsDangerous;
        public byte IsTransparent;
        public byte Destroyed;
        public byte Devastated;
    }

    /// <summary>
    /// Scale animation state. Only present on actively animating prisms.
    /// Replaces PrismScaleAnimator + PrismScaleManager Jobs pipeline.
    /// Use IEnableableComponent to toggle without structural changes.
    /// </summary>
    public struct ScaleAnimation : IComponentData, IEnableableComponent
    {
        public float3 CurrentScale;
        public float3 TargetScale;
        public float3 MinScale;
        public float3 MaxScale;
        public float GrowthRate;
    }

    /// <summary>
    /// Material color/spread animation state. Only present during theme transitions.
    /// Replaces MaterialPropertyAnimator + MaterialStateManager Jobs pipeline.
    /// </summary>
    public struct MaterialAnimation : IComponentData, IEnableableComponent
    {
        public float4 StartBrightColor;
        public float4 TargetBrightColor;
        public float4 StartDarkColor;
        public float4 TargetDarkColor;
        public float3 StartSpread;
        public float3 TargetSpread;
        public float Progress;
        public float Duration;
    }

    /// <summary>
    /// Timer for shield deactivation. Replaces coroutine-based timing.
    /// When Time.ElapsedTime >= EndTime, the system deactivates shields.
    /// </summary>
    public struct ShieldTimer : IComponentData, IEnableableComponent
    {
        public float EndTime;
    }

    /// <summary>
    /// Explosion VFX state. Attached to explosion effect entities.
    /// Replaces PrismExplosion MonoBehaviour + PrismEffectsManager.
    /// </summary>
    public struct ExplosionEffect : IComponentData
    {
        public float3 InitialPosition;
        public float3 Velocity;
        public float Speed;
        public float Elapsed;
        public float MaxDuration;
    }

    /// <summary>
    /// Implosion VFX state. Attached to implosion effect entities.
    /// Replaces PrismImplosion MonoBehaviour + PrismEffectsManager.
    /// </summary>
    public struct ImplosionEffect : IComponentData
    {
        public float3 TargetPosition;
        public float Elapsed;
        public float MaxDuration;
        public float Progress;
        public byte IsGrowing;
        public float GrowDelayRemaining;
    }
}
