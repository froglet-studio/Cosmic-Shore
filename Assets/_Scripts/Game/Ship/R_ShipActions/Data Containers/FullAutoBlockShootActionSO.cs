// FullAutoBlockShootActionSO.cs
using UnityEngine;
using CosmicShore.Core;
using CosmicShore.Game.Projectiles;

namespace CosmicShore.Game
{
    [CreateAssetMenu(
        fileName = "FullAutoBlockShootAction",
        menuName = "ScriptableObjects/Vessel Actions/Full Auto Block Shoot")]
    public class FullAutoBlockShootActionSO : ShipActionSO
    {
        [Header("Cadence & Motion")]
        [SerializeField] private float fireRate = 0.75f;
        [SerializeField] private float blockSpeed = 50f;
        [SerializeField] private float minStopDistance = 90f;
        [SerializeField] private float maxStopDistance = 100f;

        [Header("Block Appearance")]
        [SerializeField] private Vector3 blockScale = new(20f, 2f, 6f);
        [SerializeField] private Vector3 rotationOffsetEuler = new(0f, 90f, 0f); // <— rotate 90° by default

        [Header("Pooling")]
        [SerializeField] private PrismType prismType = PrismType.Sparrow;

        [Header("Collision")]
        [SerializeField] private bool disableCollidersOnLaunch = true;

        public float FireRate => fireRate;
        public float BlockSpeed => blockSpeed;
        public float MinStopDistance => minStopDistance;
        
        public float MaxStopDistance => maxStopDistance;
        public Vector3 BlockScale => blockScale;
        public Vector3 RotationOffsetEuler => rotationOffsetEuler;
        public PrismType PrismType => prismType;
        public bool DisableCollidersOnLaunch => disableCollidersOnLaunch;

        public override void StartAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
            => execs?.Get<FullAutoBlockShootActionExecutor>()?.Begin(this);

        public override void StopAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
            => execs?.Get<FullAutoBlockShootActionExecutor>()?.End();
    }
}