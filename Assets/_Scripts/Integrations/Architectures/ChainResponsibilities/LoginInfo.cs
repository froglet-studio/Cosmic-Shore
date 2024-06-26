namespace CosmicShore.Integrations.Architectures.ChainResponsibilities
{
    public class LoginInfo
    {
        public string Id { get; set; }
        public string Username { get; set; }

        public override string ToString() => $"{Id} {Username}";
    }
}