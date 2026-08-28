namespace CosmicShore.Data
{
    /// <summary>
    /// Render modes for the boot/loading status surface (<c>BootStatusPanel</c>).
    /// Carried by <see cref="BootStatusRequest"/> on the
    /// <c>Event_BootStatusRequest</c> SOAP channel.
    /// </summary>
    [System.Serializable]
    public enum BootStatusMode
    {
        Hide   = 0,
        Status = 1,
        Retry  = 2,
    }
}
