using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using System;
using Obvious.Soap;
using CosmicShore.Core;

namespace CosmicShore.Game.AI
{
    [Serializable]
    public class AIAbility
    {
        public ShipAction Ability;
        public float Duration;
        public float Cooldown;
    }

    public class AIPilot : MonoBehaviour
    {
        public float SkillLevel { get; private set; }

        [SerializeField] float defaultThrottleHigh = .6f;
        [SerializeField] float defaultThrottleLow  = .6f;

        [SerializeField] float defaultAggressivenessHigh = .035f;
        [SerializeField] float defaultAggressivenessLow  = .035f;

        [SerializeField] float throttleIncreaseHigh = .001f;
        [SerializeField] float throttleIncreaseLow  = .001f;

        [SerializeField] float avoidanceHigh = 2.5f;
        [SerializeField] float avoidanceLow = 2.5f;

        [SerializeField] float aggressivenessIncreaseHigh = .001f;
        [SerializeField] float aggressivenessIncreaseLow  = .001f;

        float throttle;
        float aggressiveness;

        public float X;
        public float Y;

        public float defaultThrottle => Mathf.Lerp(defaultThrottleLow, defaultThrottleHigh, SkillLevel);
        public float defaultAggressiveness => Mathf.Lerp(defaultAggressivenessLow, defaultAggressivenessHigh, SkillLevel);
        float throttleIncrease => Mathf.Lerp(throttleIncreaseLow, throttleIncreaseHigh, SkillLevel);
        float avoidance => Mathf.Lerp(avoidanceLow, avoidanceHigh, SkillLevel);
        float aggressivenessIncrease => Mathf.Lerp(aggressivenessIncreaseLow, aggressivenessIncreaseHigh, SkillLevel);

        float targetUpdateFrequencySeconds = 2f;

        [SerializeField] float raycastHeight;
        [SerializeField] float raycastWidth;

        [SerializeField] bool LookingAtCrystal;
        [SerializeField] bool ram;
        [SerializeField] bool drift;

        [SerializeField] List<AIAbility> abilities;

        [SerializeField]
        ScriptableEventNoParam OnCellItemsUpdated;

        enum Corner 
        {
            TopRight,
            BottomRight,
            BottomLeft,
            TopLeft,
        };
        public bool AutoPilotEnabled { get; private set; }

        IShip _ship;
        IShipStatus _shipStatus => _ship.ShipStatus;
        IInputStatus _inputStatus => _shipStatus.InputStatus;

        float _lastPitchTarget;
        float _lastYawTarget;
        float _lastRollTarget;

        RaycastHit _hit;
        float _maxDistance = 50f;
        float _maxDistanceSquared;

        Vector3 _targetPosition;
        Vector3 _distance;

        [HideInInspector] public FlowFieldData flowFieldData;

        Dictionary<Corner, AvoidanceBehavior> CornerBehaviors;

        #region Avoidance Stuff
        const float Clockwise = -1;
        const float CounterClockwise = 1;
        struct AvoidanceBehavior
        {
            public float width;
            public float height;
            public float spin;
            public Vector3 direction;

            public AvoidanceBehavior(float width, float height, float spin, Vector3 direction)
            {
                this.width = width;
                this.height = height;
                this.spin = spin;
                this.direction = direction;
            }
        }
        #endregion


        private void OnEnable()
        {
            OnCellItemsUpdated.OnRaised += UpdateCellContent;
        }

        private void OnDisable()
        {
            OnCellItemsUpdated.OnRaised -= UpdateCellContent;
        }


        void UpdateCellContent()
        {
            //Debug.Log($"NodeContentUpdated - transform.position: {transform.position}");
            var activeCell = CellControlManager.Instance.GetCellByPosition(transform.position);

            if (activeCell == null)
                activeCell = CellControlManager.Instance.GetNearestCell(transform.position);

            var cellItems = activeCell.GetItems();
            float MinDistance = Mathf.Infinity;
            CellItem closestItem = null;

            foreach (var item in cellItems.Values)
            {
                // Debuffs are disguised as desireable to the other team
                // So, if it's good, or if it's bad but made by another team, go for it
                if (item.ItemType != ItemType.Buff &&
                    (item.ItemType != ItemType.Debuff || item.OwnTeam == _shipStatus.Team)) continue;
                var distance = Vector3.SqrMagnitude(item.transform.position - transform.position);
                if (distance < (MinDistance * MinDistance))
                {
                    closestItem = item;
                    MinDistance = distance;
                }
            }

            _targetPosition = closestItem == null ? activeCell.transform.position : closestItem.transform.position;
        }


        /*public void AssignShip(IShip ship)
        {
            _ship = ship;
        }*/

        public void Initialize(bool enableAutoPilot, IShip ship)
        {
            AutoPilotEnabled = enableAutoPilot;
            _ship = ship;

            // Debug.Log($"AutoPilotStatus {AutoPilotEnabled}");

            // TODO - Even a player can have autopilot mode enabled. So we initialize anyway.
            /*if (!AutoPilotEnabled)
                return;*/

            _maxDistanceSquared = _maxDistance * _maxDistance;
            aggressiveness = defaultAggressiveness;
            throttle = defaultThrottle;

            CornerBehaviors = new Dictionary<Corner, AvoidanceBehavior>() {
                { Corner.TopRight, new AvoidanceBehavior (raycastWidth, raycastHeight, Clockwise, Vector3.zero ) },
                { Corner.BottomRight, new AvoidanceBehavior (raycastWidth, -raycastHeight, CounterClockwise, Vector3.zero ) },
                { Corner.BottomLeft, new AvoidanceBehavior (-raycastWidth, -raycastHeight, Clockwise, Vector3.zero ) },
                { Corner.TopLeft, new AvoidanceBehavior (-raycastWidth, raycastHeight, CounterClockwise, Vector3.zero ) }
            };

            foreach
                (var ability in abilities)
            {
                StartCoroutine(UseAbilityCoroutine(ability));
            }
        }


        void Update()
        {
            if (!AutoPilotEnabled)
                return;

            _distance = _targetPosition - transform.position;
            Vector3 desiredDirection = _distance.normalized;

            LookingAtCrystal = Vector3.Dot(desiredDirection, _shipStatus.Course) >= .9f;
            if (LookingAtCrystal && drift && !_shipStatus.Drifting)
            {
                _shipStatus.Course = desiredDirection;
                _ship.PerformShipControllerActions(InputEvents.LeftStickAction);
                desiredDirection *= -1;
            }
            else if (LookingAtCrystal && _shipStatus.Drifting) desiredDirection *= -1;
            else if (_shipStatus.Drifting) _ship.StopShipControllerActions(InputEvents.LeftStickAction);


            if (_distance.magnitude < float.Epsilon) // Avoid division by zero
                return;

            Vector3 combinedLocalCrossProduct = Vector3.zero;
            float sqrMagnitude = _distance.sqrMagnitude;
            Vector3 crossProduct = Vector3.Cross(transform.forward, desiredDirection);
            Vector3 localCrossProduct = transform.InverseTransformDirection(crossProduct);
            combinedLocalCrossProduct += localCrossProduct;

            aggressiveness = 100f;  // Multiplier to mitigate vanishing cross products that cause aimless drift
            float angle = Mathf.Asin(Mathf.Clamp(combinedLocalCrossProduct.sqrMagnitude * aggressiveness / Mathf.Min(sqrMagnitude, _maxDistance), -1f, 1f)) * Mathf.Rad2Deg;

            if (_shipStatus.SingleStickControls)
            {
                _inputStatus.XSum = Mathf.Clamp(angle * combinedLocalCrossProduct.y, -1, 1);
                _inputStatus.YSum = -Mathf.Clamp(angle * combinedLocalCrossProduct.x, -1, 1);
            }
            else
            {
                _inputStatus.YSum = Mathf.Clamp(angle * combinedLocalCrossProduct.x, -1, 1);
                _inputStatus.XSum = Mathf.Clamp(angle * combinedLocalCrossProduct.y, -1, 1);
                _inputStatus.YDiff = Mathf.Clamp(angle * combinedLocalCrossProduct.y, -1, 1);
                _inputStatus.XDiff = (LookingAtCrystal && ram) ? 1 : Mathf.Clamp(throttle, 0, 1);
            }

            //aggressiveness += aggressivenessIncrease * Time.deltaTime;
            throttle += throttleIncrease * Time.deltaTime;
        }
        Vector3 ShootLaser(Vector3 position)
        {
            if (Physics.Raycast(transform.position + position, transform.forward, out _hit, _maxDistance))
            {
                Debug.DrawLine(transform.position + position, _hit.point, Color.red);
                return _hit.point - (transform.position + position);
            }

            Debug.DrawLine(transform.position + position, transform.position + position + transform.forward * _maxDistance, Color.green);
            return transform.forward * _maxDistance - (transform.position + position);
        }

        


        IEnumerator UseAbilityCoroutine(AIAbility action) 
        {
            yield return new WaitForSeconds(3);
            while (true)
            {
                action.Ability.StartAction();
                yield return new WaitForSeconds(action.Duration);
                action.Ability.StopAction();
                yield return new WaitForSeconds(action.Cooldown);
            }
        }


        /*IEnumerator SetTargetCoroutine()
        {
            // TODO - these lists if needed, should be specified separate.
            List<ShipClassType> aggressiveShips = new List<ShipClassType> 
            { 
                ShipClassType.Rhino, 
                ShipClassType.Sparrow,
            };

            var rand = new System.Random();

            // Assume activeNode can't change.
            var activeCell = CellControlManager.Instance.GetCellByPosition(transform.position);  
            if (activeCell == null)
                activeCell = CellControlManager.Instance.GetNearestCell(transform.position);

            while (true)
            {
                if (activeCell != null && 
                    // TODO - Commented out as aggressive 
                    // aggressiveShips.Contains(_ship.ShipStatus.ShipType) && 
                    activeCell.ControllingTeam != Teams.None)
                {
                    if ((_shipStatus.Team == activeCell.ControllingTeam) || (rand.NextDouble() < 0.5))  // Your team is winning.
                    {
                        _targetPosition = _crystalPosition;
                    }
                    else
                    {
                        _targetPosition = activeCell.GetExplosionTarget(activeCell.ControllingTeam);  // Block centroid belonging to the winning team
                    }
                }
                else
                {
                    _targetPosition = _crystalPosition;
                }
                yield return new WaitForSeconds(targetUpdateFrequencySeconds);
            }
        }*/

        #region Unused Methods

        float CalculateRollAdjustment(Dictionary<Corner, Vector3> obstacleDirections)
        {
            float rollAdjustment = 0f;

            // Example logic: If top right and bottom left corners detect obstacles, induce a roll.
            if (obstacleDirections[Corner.TopRight].magnitude > 0 && obstacleDirections[Corner.BottomLeft].magnitude > 0)
                rollAdjustment -= 1; // Roll left
            if (obstacleDirections[Corner.TopLeft].magnitude > 0 && obstacleDirections[Corner.BottomRight].magnitude > 0)
                rollAdjustment += 1; // Roll right

            return rollAdjustment;
        }

        float SigmoidResponse(float input)
        {
            float output = 2 * (1 / (1 + Mathf.Exp(-0.1f * input)) - 0.5f);
            return output;
        }

        #endregion
    }
}