namespace CosmicShore.Integrations.Architectures.ChainResponsibilities
{
    public class ErrorInfo
    {
        public int ErrorCode { get; set; }
        public string Error { get; set; }
        
        public override string ToString() => $"{ErrorCode} {Error}";
    }
}