namespace CosmicShore.Data
{
    // Remember folks, only you can prevent Unity from arbitrarily swapping enum values in files.
    // Always assign a static numeric value to your enum types.
    //
    // Bucketed stress level of a Cell based on live prism count. Drives environmental
    // response (fauna spawn rate, cleanup aggression, gyroid growth throttle) so the
    // cell self-regulates toward a sustainable prism load without exploding.
    public enum CellAggressionLevel
    {
        Calm = 0,
        Elevated = 1,
        Stressed = 2,
        Critical = 3,
    }
}
