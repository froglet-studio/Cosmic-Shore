public struct ShipThrottleModifier
{
    public float initialValue;
    public float duration;
    public float elapsedTime;

    public ShipThrottleModifier(float initialValue, float duration, float elapsedTime)
    {
        this.initialValue = initialValue;
        this.duration = duration;
        this.elapsedTime = elapsedTime;
    }
}