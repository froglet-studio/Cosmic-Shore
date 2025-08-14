using System;
using UnityEngine;

namespace CosmicShore.Game
{
    /// <summary>
    /// On StartAction: redirect all SnowChanger shards via the shared bus.
    /// On StopAction: restore normal (look-at-crystal) behavior.
    /// </summary>
    public class ShardToggleAction : ShipAction
    {
        public enum RedirectMode
        {
            PointAtTransform,
            PointAtPosition,
            AlignToAxis
        }

        [Header("Bus")]
        [SerializeField] ShardFieldBus shardFieldBus;

        [Header("Mode & Params")]
        [SerializeField] RedirectMode mode = RedirectMode.PointAtPosition;

        [SerializeField] Transform targetTransform; // for PointAtTransform
        [SerializeField] Vector3   targetPosition;  // for PointAtPosition
        [SerializeField] Vector3   axis = Vector3.up; // for AlignToAxis
        [SerializeField] bool axisLookAtReject = true;

        public override void StartAction()
        {
            if (shardFieldBus == null)
            {
                Debug.LogWarning("[ShardToggleAction] No ShardFieldBus assigned!");
                return;
            }

            Debug.Log($"[ShardToggleAction] StartAction triggered. Mode={mode}");

            switch (mode)
            {
                case RedirectMode.PointAtTransform:
                    Debug.Log($"[ShardToggleAction] Redirecting shards to Transform: {targetTransform?.name}");
                    shardFieldBus.BroadcastPointAtTransform(targetTransform);
                    break;

                case RedirectMode.PointAtPosition:
                    Debug.Log($"[ShardToggleAction] Redirecting shards to Position: {targetPosition}");
                    shardFieldBus.BroadcastPointAtPosition(targetPosition);
                    break;

                case RedirectMode.AlignToAxis:
                    Debug.Log($"[ShardToggleAction] Aligning shards to Axis: {axis} (lookAt={axisLookAtReject})");
                    shardFieldBus.BroadcastAlignToAxis(axis, axisLookAtReject);
                    break;
            }
        }

        public override void StopAction()
        {
            if (shardFieldBus == null)
            {
                Debug.LogWarning("[ShardToggleAction] No ShardFieldBus assigned!");
                return;
            }

            Debug.Log("[ShardToggleAction] StopAction triggered. Restoring shards to original crystal orientation.");
            shardFieldBus.BroadcastRestoreToCrystal();
        }
    }
}