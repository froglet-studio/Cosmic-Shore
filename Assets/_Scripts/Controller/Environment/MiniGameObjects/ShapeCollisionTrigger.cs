using CosmicShore.Gameplay;
using UnityEngine;

/// <summary>
/// Attached at runtime to a spawned shape container by SpawnableShapeBase.
/// Detects vessel collision and fires ShapeSignEvents to start shape drawing mode.
/// Collision is disabled until SetReady(true) is called (after gradual spawn completes).
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class ShapeCollisionTrigger : MonoBehaviour
{
    ShapeDefinition _shapeDefinition;
    Domains _shapeDomain;
    bool _triggered;
    bool _ready;

    public void Initialize(ShapeDefinition shapeDefinition, Domains shapeDomain = Domains.Blue)
    {
        _shapeDefinition = shapeDefinition;
        _shapeDomain = shapeDomain;
        _triggered = false;
        _ready = false;
    }

    /// <summary>
    /// Enable or disable collision detection.
    /// Called by SpawnableShapeBase after gradual spawning completes.
    /// </summary>
    public void SetReady(bool ready)
    {
        _ready = ready;
    }

    void OnTriggerEnter(Collider other)
    {
        if (_triggered || !_ready) return;
        if (_shapeDefinition == null) return;

        if (other.GetComponentInParent<VesselStatus>())
        {
            _triggered = true;
            ShapeSignEvents.RaiseShapeSelected(_shapeDefinition, transform.position, _shapeDomain);
        }
    }
}
