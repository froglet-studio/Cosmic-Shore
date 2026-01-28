using CosmicShore.Game.AI;
using UnityEngine;

namespace CosmicShore.Game.Cinematics
{
    /// <summary>
    /// Handles AI-driven cinematic behaviors for vessels during end-game sequences.
    /// This component is separate from AIPilot to maintain SOLID principles.
    /// </summary>
    public class AICinematicBehavior : MonoBehaviour
    {
        [SerializeField] private float forwardSpeed = 20f;
        [SerializeField] private float loopRadius = 15f;
        [SerializeField] private float loopSpeed = 2f;
        
        private IVesselStatus vesselStatus;
        private AIPilot aiPilot;
        private AICinematicBehaviorType currentBehavior;
        private bool isActive;
        private float behaviorStartTime;
        private Vector3 loopCenter;

        public void Initialize(IVesselStatus status, AIPilot pilot)
        {
            vesselStatus = status;
            aiPilot = pilot;
        }

        /// <summary>
        /// Start a specific cinematic behavior using enum
        /// </summary>
        public void StartCinematicBehavior(AICinematicBehaviorType behaviorType)
        {
            currentBehavior = behaviorType;
            isActive = true;
            behaviorStartTime = Time.time;
            
            Debug.Log($"[AICinematicBehavior] Starting behavior: {behaviorType}");
            
            // Setup behavior-specific initialization
            switch (behaviorType)
            {
                case AICinematicBehaviorType.MoveForward:
                    InitializeMoveForward();
                    break;
                case AICinematicBehaviorType.Loop:
                    InitializeLoop();
                    break;
                case AICinematicBehaviorType.Drift:
                    InitializeDrift();
                    break;
                case AICinematicBehaviorType.Spiral:
                    InitializeSpiral();
                    break;
                case AICinematicBehaviorType.BarrelRoll:
                    Debug.LogWarning("BarrelRoll not yet implemented - using MoveForward");
                    InitializeMoveForward();
                    break;
                case AICinematicBehaviorType.FlyBy:
                    Debug.LogWarning("FlyBy not yet implemented - using MoveForward");
                    InitializeMoveForward();
                    break;
                case AICinematicBehaviorType.HoverSpin:
                    Debug.LogWarning("HoverSpin not yet implemented - using MoveForward");
                    InitializeMoveForward();
                    break;
                default:
                    InitializeMoveForward();
                    break;
            }
        }

        /// <summary>
        /// Stop cinematic behavior
        /// </summary>
        public void StopCinematicBehavior()
        {
            isActive = false;
            Debug.Log($"[AICinematicBehavior] Stopped behavior: {currentBehavior}");
        }

        private void Update()
        {
            if (!isActive || vesselStatus == null)
                return;

            switch (currentBehavior)
            {
                case AICinematicBehaviorType.MoveForward:
                    ExecuteMoveForward();
                    break;
                case AICinematicBehaviorType.Loop:
                    ExecuteLoop();
                    break;
                case AICinematicBehaviorType.Drift:
                    ExecuteDrift();
                    break;
                case AICinematicBehaviorType.Spiral:
                    ExecuteSpiral();
                    break;
                case AICinematicBehaviorType.BarrelRoll:
                case AICinematicBehaviorType.FlyBy:
                case AICinematicBehaviorType.HoverSpin:
                    // Fall back to move forward for unimplemented behaviors
                    ExecuteMoveForward();
                    break;
            }
        }

        #region Move Forward Behavior
        private void InitializeMoveForward()
        {
            // Simple initialization if needed
        }

        private void ExecuteMoveForward()
        {
            // Move vessel forward at constant speed
            if (vesselStatus.InputStatus != null)
            {
                // For dual-stick controls
                if (!vesselStatus.IsSingleStickControls)
                {
                    vesselStatus.InputStatus.XDiff = 1f; // Full throttle
                    vesselStatus.InputStatus.YSum = 0f;
                    vesselStatus.InputStatus.XSum = 0f;
                }
                else
                {
                    // For single-stick, keep centered
                    vesselStatus.InputStatus.EasedLeftJoystickPosition = Vector2.zero;
                }
            }
        }
        #endregion

        #region Loop Behavior
        private void InitializeLoop()
        {
            loopCenter = transform.position + transform.forward * loopRadius;
        }

        private void ExecuteLoop()
        {
            float elapsedTime = Time.time - behaviorStartTime;
            float angle = elapsedTime * loopSpeed;
            
            Vector3 offset = new Vector3(
                Mathf.Sin(angle) * loopRadius,
                0f,
                Mathf.Cos(angle) * loopRadius
            );
            
            Vector3 targetPosition = loopCenter + offset;
            Vector3 direction = (targetPosition - transform.position).normalized;
            
            // Apply steering inputs to AI
            ApplySteeringTowardsDirection(direction);
        }
        #endregion

        #region Drift Behavior
        private void InitializeDrift()
        {
            if (vesselStatus != null && !vesselStatus.IsDrifting)
            {
                // Activate drift
                vesselStatus.Vessel?.PerformShipControllerActions(InputEvents.LeftStickAction);
            }
        }

        private void ExecuteDrift()
        {
            // Continue moving forward while drifting
            if (vesselStatus.InputStatus != null && !vesselStatus.IsSingleStickControls)
            {
                vesselStatus.InputStatus.XDiff = 0.5f;
            }
        }
        #endregion

        #region Spiral Behavior
        private void InitializeSpiral()
        {
            loopCenter = transform.position;
        }

        private void ExecuteSpiral()
        {
            float elapsedTime = Time.time - behaviorStartTime;
            float angle = elapsedTime * loopSpeed;
            float expandingRadius = loopRadius * (1f + elapsedTime * 0.1f);
            
            Vector3 offset = new Vector3(
                Mathf.Sin(angle) * expandingRadius,
                elapsedTime * 2f, // Rise up
                Mathf.Cos(angle) * expandingRadius
            );
            
            Vector3 targetPosition = loopCenter + offset;
            Vector3 direction = (targetPosition - transform.position).normalized;
            
            ApplySteeringTowardsDirection(direction);
        }
        #endregion

        #region Helper Methods
        private void ApplySteeringTowardsDirection(Vector3 targetDirection)
        {
            if (vesselStatus?.InputStatus == null)
                return;

            Vector3 crossProduct = Vector3.Cross(transform.forward, targetDirection);
            Vector3 localCrossProduct = transform.InverseTransformDirection(crossProduct);
            
            float aggressiveness = 50f;
            float angle = Mathf.Asin(Mathf.Clamp(localCrossProduct.magnitude * aggressiveness, -1f, 1f)) * Mathf.Rad2Deg;

            if (vesselStatus.IsSingleStickControls)
            {
                float x = Mathf.Clamp(angle * localCrossProduct.y, -1, 1);
                float y = -Mathf.Clamp(angle * localCrossProduct.x, -1, 1);
                vesselStatus.InputStatus.EasedLeftJoystickPosition = new Vector2(x, y);
            }
            else
            {
                vesselStatus.InputStatus.XSum = Mathf.Clamp(angle * localCrossProduct.y, -1, 1);
                vesselStatus.InputStatus.YSum = Mathf.Clamp(angle * localCrossProduct.x, -1, 1);
                vesselStatus.InputStatus.XDiff = 0.8f;
            }
        }
        #endregion

        #region Future Behaviors (Placeholders)
        
        /// <summary>
        /// TODO: Execute barrel roll animation
        /// </summary>
        void ExecuteBarrelRoll()
        {
            // Will be implemented in future
            Debug.Log("Barrel roll cinematic - To be implemented");
        }

        /// <summary>
        /// TODO: Execute victory fly-by
        /// </summary>
        void ExecuteFlyBy()
        {
            // Will be implemented in future
            Debug.Log("Fly-by cinematic - To be implemented");
        }

        /// <summary>
        /// TODO: Execute hover and spin
        /// </summary>
        void ExecuteHoverSpin()
        {
            // Will be implemented in future
            Debug.Log("Hover spin cinematic - To be implemented");
        }

        #endregion
    }
}