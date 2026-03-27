namespace CosmicShore.DialogueSystem.Models
{
    /// <summary>
    /// Preset UI element positions where tutorial arrows can point.
    /// Expand this enum as new UI elements are added to the main menu.
    /// </summary>
    [System.Flags]
    public enum ArrowTarget
    {
        None            = 0,
        ArcadeButton    = 1 << 0,
        SettingsButton  = 1 << 1,
        TopNav          = 1 << 2,
        BottomNav       = 1 << 3,
        PlayButton      = 1 << 4,
        ProfileButton   = 1 << 5,
        StoreButton     = 1 << 6,
        FreestyleCard   = 1 << 7,
        BackButton      = 1 << 8,
        MissionPanel    = 1 << 9,
    }
}
