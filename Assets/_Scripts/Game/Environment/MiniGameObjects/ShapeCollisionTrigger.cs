using CosmicShore.Game;
using CosmicShore.Game.ShapeDrawing;
using UnityEngine;

/// <summary>
/// Attached at runtime to a spawned shape container by SpawnableShapeBase.
/// Detects vessel collision and fires ShapeSignEvents to start shape drawing mode.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class ShapeCollisionTrigger : MonoBehaviour
{
    ShapeDefinition _shapeDefinition;
    bool _triggered;

    public void Initialize(ShapeDefinition shapeDefinition)
    {
        _shapeDefinition = shapeDefinition;
        _triggered = false;
    }

    void OnTriggerEnter(Collider other)
    {
        if (_triggered) return;
        if (_shapeDefinition == null) return;

        if (other.GetComponentInParent<VesselStatus>())
        {
            _triggered = true;
            ShapeSignEvents.RaiseShapeSelected(_shapeDefinition, transform.position);
        }
    }
}
