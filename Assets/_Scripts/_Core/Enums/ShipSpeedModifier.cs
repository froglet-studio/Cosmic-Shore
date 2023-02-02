public struct ShipSpeedModifier
{
    public float initialValue;
    public float duration;
    public float elapsedTime;

    public ShipSpeedModifier(float initialValue, float duration, float elapsedTime)
    {
        this.initialValue = initialValue;
        this.duration = duration;
        this.elapsedTime = elapsedTime;
    }
}