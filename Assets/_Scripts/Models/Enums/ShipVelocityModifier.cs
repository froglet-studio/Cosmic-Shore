using UnityEngine;

public struct ShipVelocityModifier
{
    public Vector3 initialValue;
    public float duration;
    public float elapsedTime;

    public ShipVelocityModifier(Vector3 initialValue, float duration, float elapsedTime)
    {
        this.initialValue = initialValue;
        this.duration = duration;
        this.elapsedTime = elapsedTime;
    }
}