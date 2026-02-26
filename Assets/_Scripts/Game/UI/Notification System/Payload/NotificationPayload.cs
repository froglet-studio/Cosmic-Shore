namespace CosmicShore.Game.UI
{
    [System.Serializable]
    public struct NotificationPayload
    {
        public string Header;
        public string Title;
        public NotificationPayload(string header, string title)
        { Header = header; Title = title; }
    }
}